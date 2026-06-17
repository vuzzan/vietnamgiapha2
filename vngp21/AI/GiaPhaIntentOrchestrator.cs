using System;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Điều phối: parse intent → fact hoặc sửa gia phả. LLM local khi rule không hiểu.</summary>
    public sealed class GiaPhaIntentOrchestrator
    {
        private readonly LocalLlamaIntentParser _llmParser = new LocalLlamaIntentParser();

        /// <summary>Thực thi lệnh sửa trên UI thread — AiChatDialog gán qua Dispatcher.</summary>
        public Func<GiaPhaIntent, GiaPhaParseSource, Task<GiaPhaQueryResult>> EditActionHandler { get; set; }

        public async Task<GiaPhaQueryResult> AskAsync(
            string question,
            GiaPhaQueryEngine engine,
            AiSettings settings,
            FamilyViewModel currentFamily,
            FamilyViewModel fileRoot,
            IProgress<string> progress,
            CancellationToken ct = default)
        {
            if (engine == null)
            {
                return GiaPhaQueryResult.Fail("⚠️ Engine tra cứu chưa được khởi tạo. Hãy mở file gia phả trước.");
            }

            if (!engine.IsReady)
            {
                return GiaPhaQueryResult.Fail("⚠️ Chưa có dữ liệu gia phả. Hãy mở file gia phả trước.");
            }

            GiaPhaIntentTraceLog.LogAskStart(question, settings, currentFamily);

            GiaPhaParseResult parse = engine.TryParseRule(question, currentFamily);
            GiaPhaIntentTraceLog.LogRuleParse(parse);

            // Chào hỏi / stopword — không gọi Qwen (tránh bịa intent từ ngữ cảnh GĐ #…)
            if (!parse.IsParsed && GiaPhaRuleEngineLoader.MatchesStopwordQuestion(question))
            {
                GiaPhaIntentTraceLog.LogQwenSkipped("stopword — không gọi Qwen");
                string greet = "Xin chào! Tôi tra cứu và sửa gia phả. Hãy hỏi cụ thể, ví dụ:\n"
                    + GetHelpExamples();
                GiaPhaIntentTraceLog.LogFailed("stopword", greet);
                return GiaPhaQueryResult.Fail(greet);
            }

            // Rule strict → fact ngay; rule weak / fail → Qwen (nếu bật)
            bool ruleStrictOk = parse.IsParsed && parse.IsStrict;
            bool canUseLlm = settings != null
                && AiBackendModes.IsLocalLlama(settings.BackendMode)
                && settings.UseLlmForIntentParse;
            bool tryLlm = canUseLlm && !ruleStrictOk;

            if (ruleStrictOk)
            {
                GiaPhaIntentTraceLog.LogQwenSkipped("rule strict — không cần Qwen");
                GiaPhaQueryResult strictResult = await ExecuteResolvedIntentAsync(
                    parse.Intent, parse.Source, engine, currentFamily).ConfigureAwait(false);
                GiaPhaIntentTraceLog.LogExecute(strictResult);
                return strictResult;
            }

            if (parse.IsParsed && !parse.IsStrict)
            {
                GiaPhaIntentTraceLog.LogQwenSkipped("rule weak — chuyển Qwen");
            }

            GiaPhaParseResult finalParse = parse;
            if (tryLlm)
            {
                GiaPhaParseResult llmParse = await _llmParser.TryParseAsync(
                    settings,
                    question,
                    currentFamily,
                    fileRoot,
                    progress,
                    ct).ConfigureAwait(false);

                if (llmParse.IsParsed)
                {
                    finalParse = llmParse;
                }
            }
            else if (!parse.IsParsed)
            {
                GiaPhaIntentTraceLog.LogQwenSkipped(
                    "rule fail, không gọi Qwen (backend="
                    + (settings?.BackendMode ?? "?")
                    + ", useLlm=" + (settings?.UseLlmForIntentParse == true) + ")");
            }
            else if (!parse.IsStrict)
            {
                GiaPhaIntentTraceLog.LogQwenSkipped(
                    "rule weak nhưng không gọi Qwen (backend="
                    + (settings?.BackendMode ?? "?") + ")");
            }

            if (!finalParse.IsParsed)
            {
                string hint = tryLlm
                    ? "Không hiểu câu hỏi (rule + Qwen). Hãy hỏi cụ thể hơn."
                    : "Không hiểu câu hỏi. Hãy hỏi cụ thể hơn.";
                GiaPhaIntentTraceLog.LogFailed("parse", hint);
                return GiaPhaQueryResult.Fail(hint + "\n\n" + GetHelpExamples());
            }

            // Rule weak không được chạy fact — chỉ chấp nhận intent từ Qwen
            if (finalParse.Source == GiaPhaParseSource.Rule && !finalParse.IsStrict)
            {
                string hint = canUseLlm
                    ? "Qwen không hiểu câu hỏi. Hãy hỏi cụ thể hơn."
                    : "Câu chưa khớp rule chặt. Bật Qwen local hoặc hỏi gọn (vd. \"tìm Nghĩa\", \"con của X\").";
                GiaPhaIntentTraceLog.LogFailed("rule-weak", hint);
                return GiaPhaQueryResult.Fail(hint + "\n\n" + GetHelpExamples());
            }

            GiaPhaQueryResult result = await ExecuteResolvedIntentAsync(
                finalParse.Intent, finalParse.Source, engine, currentFamily).ConfigureAwait(false);

            GiaPhaIntentTraceLog.LogExecute(result);
            return result;
        }

        private async Task<GiaPhaQueryResult> ExecuteResolvedIntentAsync(
            GiaPhaIntent intent,
            GiaPhaParseSource source,
            GiaPhaQueryEngine engine,
            FamilyViewModel currentFamily)
        {
            if (intent != null && GiaPhaIntentKinds.IsEditAction(intent.Kind))
            {
                if (EditActionHandler == null)
                {
                    return GiaPhaQueryResult.Fail("⚠️ Chưa kết nối thao tác sửa gia phả.");
                }

                GiaPhaQueryResult editResult = await EditActionHandler(intent, source).ConfigureAwait(false);
                if (editResult != null && string.IsNullOrEmpty(editResult.StatusHint))
                {
                    editResult.StatusHint = source == GiaPhaParseSource.LocalLlm
                        ? "Qwen hiểu lệnh → sửa gia phả"
                        : "Rule hiểu lệnh → sửa gia phả";
                }

                return editResult;
            }

            GiaPhaQueryResult queryResult = engine.Execute(intent, currentFamily, source);
            if (source == GiaPhaParseSource.LocalLlm)
            {
                queryResult.StatusHint = "Qwen hiểu câu hỏi → fact engine";
            }
            else if (source == GiaPhaParseSource.Rule)
            {
                queryResult.StatusHint = "Rule hiểu câu hỏi → fact engine";
            }

            return queryResult;
        }

        private static string GetHelpExamples()
        {
            return "Tra cứu:\n"
                + "• \"Con của Nguyễn Văn Chính\"\n"
                + "• \"Đời 4 có ai?\"\n"
                + "• \"Lương Văn A và Trần Thị B có quan hệ gì?\"\n\n"
                + "Sửa gia phả (chọn gia đình trên cây trước):\n"
                + "• \"Thêm người vào gia đình\"\n"
                + "• \"Thêm gia đình con\"\n"
                + "• \"Thêm người tên Nguyễn Văn A\"\n"
                + "• \"Đổi tên Nguyễn Văn B thành Nguyễn Văn C\"";
        }
    }
}
