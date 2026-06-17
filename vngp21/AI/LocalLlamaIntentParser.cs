using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Qwen local chỉ parse câu hỏi → GiaPhaIntent (JSON), không trả lời fact.</summary>
    public sealed class LocalLlamaIntentParser
    {
        private readonly LocalLlamaChatService _llama = new LocalLlamaChatService();

        public async Task<GiaPhaParseResult> TryParseAsync(
            AiSettings settings,
            string question,
            FamilyViewModel currentFamily,
            FamilyViewModel fileRoot,
            IProgress<string> progress,
            CancellationToken ct = default)
        {
            if (settings == null)
            {
                return FailParse(question, "Chưa có cài đặt AI.");
            }

            string systemPrompt = GiaPhaIntentLlmGuide.BuildSystemPrompt();
            string userPrompt = BuildUserPrompt(question, currentFamily, fileRoot);

            try
            {
                GiaPhaIntentTraceLog.LogQwenRequest(userPrompt);
                progress?.Report("Qwen đang hiểu câu hỏi…");
                string raw = await _llama.AskPlainAsync(
                    settings, systemPrompt, userPrompt, progress, ct).ConfigureAwait(false);

                string extractedJson;
                GiaPhaIntent intent = MapJsonToIntent(raw, question, out extractedJson);
                intent = GiaPhaIntentLlmGuide.NormalizeAfterParse(intent, question);
                bool parsedOk = GiaPhaIntentKinds.IsKnown(intent.Kind);
                GiaPhaIntentTraceLog.LogQwenResponse(
                    raw,
                    extractedJson,
                    intent,
                    parsedOk,
                    parsedOk ? null : "kind không hợp lệ hoặc Unknown");

                if (!parsedOk)
                {
                    return FailParse(question, "LLM intent không hợp lệ.");
                }

                return new GiaPhaParseResult
                {
                    Intent = intent,
                    Source = GiaPhaParseSource.LocalLlm,
                    Note = "local-llm"
                };
            }
            catch (Exception ex)
            {
                GiaPhaIntentTraceLog.LogQwenResponse(null, null, null, false, ex.Message);
                return FailParse(question, ex.Message);
            }
        }

        private static string BuildUserPrompt(
            string question,
            FamilyViewModel currentFamily,
            FamilyViewModel fileRoot)
        {
            var sb = new StringBuilder(512);
            string header = GiaPhaIntentPromptLoader.LoadUserPromptHeader();
            if (!string.IsNullOrEmpty(header))
            {
                sb.AppendLine(header);
            }
            if (fileRoot != null)
            {
                string summary = FamilyContextSerializer.SerializeTreeSummary(fileRoot);
                if (summary.Length > 400)
                {
                    summary = summary.Substring(0, 400) + "…";
                }
                sb.AppendLine(summary);
            }

            if (currentFamily?.familyInfo != null)
            {
                sb.AppendLine("Đang chọn: GĐ #" + currentFamily.familyInfo.FamilyId
                    + ", đời " + currentFamily.familyInfo.FamilyLevel);
            }

            sb.AppendLine();
            sb.AppendLine("CÂU HỎI:");
            sb.AppendLine(question ?? "");
            return sb.ToString();
        }

        private static GiaPhaIntent MapJsonToIntent(string raw, string question, out string extractedJson)
        {
            extractedJson = ExtractJsonObject(raw);
            if (string.IsNullOrWhiteSpace(extractedJson))
            {
                return GiaPhaIntent.Unknown(question);
            }

            string json = extractedJson;

            try
            {
                var dto = JsonSerializer.Deserialize<GiaPhaIntentJsonDto>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto == null || string.IsNullOrWhiteSpace(dto.Kind))
                {
                    return GiaPhaIntent.Unknown(question);
                }

                var intent = new GiaPhaIntent
                {
                    Kind = dto.Kind.Trim(),
                    Generation = dto.Generation,
                    Year = dto.Year,
                    IsBirthYear = dto.IsBirthYear,
                    AncestorLevels = dto.AncestorLevels,
                    UseCurrentFamily = dto.UseCurrentFamily,
                    SpouseRole = dto.SpouseRole,
                    NewPersonName = dto.NewPersonName,
                    RawQuestion = question
                };

                if (dto.Names != null)
                {
                    foreach (string n in dto.Names)
                    {
                        if (!string.IsNullOrWhiteSpace(n))
                        {
                            intent.Names.Add(n.Trim());
                        }
                    }
                }

                return intent;
            }
            catch
            {
                return GiaPhaIntent.Unknown(question);
            }
        }

        private static string ExtractJsonObject(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string text = raw.Trim();
            var fence = Regex.Match(text, @"\{[\s\S]*\}");
            if (fence.Success)
            {
                return fence.Value;
            }

            int start = text.IndexOf('{');
            int end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return text.Substring(start, end - start + 1);
            }

            return null;
        }

        private static GiaPhaParseResult FailParse(string question, string note)
        {
            return new GiaPhaParseResult
            {
                Intent = GiaPhaIntent.Unknown(question),
                Source = GiaPhaParseSource.Failed,
                Note = note
            };
        }

        private sealed class GiaPhaIntentJsonDto
        {
            public string Kind { get; set; }
            public List<string> Names { get; set; }
            public int? Generation { get; set; }
            public int? Year { get; set; }
            public bool IsBirthYear { get; set; }
            public int? AncestorLevels { get; set; }
            public bool UseCurrentFamily { get; set; }
            public string SpouseRole { get; set; }
            public string NewPersonName { get; set; }
        }
    }
}
