using System;
using System.IO;
using System.Text;

namespace vietnamgiapha.AI
{
    /// <summary>
    /// Log theo dõi luồng: câu hỏi → rule/Qwen intent → fact engine.
    /// File: {thư mục chạy}\logs\ai-intent-trace.log
    /// </summary>
    public static class GiaPhaIntentTraceLog
    {
        private static readonly object Gate = new object();

        public static string LogFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "ai-intent-trace.log");

        public static void WriteBlock(string title, string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                body = "(trống)";
            }

            lock (Gate)
            {
                try
                {
                    string dir = Path.GetDirectoryName(LogFilePath);
                    if (!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var sb = new StringBuilder(512 + body.Length);
                    sb.AppendLine();
                    sb.Append('[').Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")).Append("] ");
                    sb.AppendLine(title);
                    sb.AppendLine(body);
                    sb.AppendLine(new string('-', 72));

                    File.AppendAllText(LogFilePath, sb.ToString(), Encoding.UTF8);
                }
                catch
                {
                    // Không làm hỏng chat nếu ghi log thất bại
                }
            }
        }

        public static void LogAskStart(
            string question,
            AiSettings settings,
            FamilyViewModel currentFamily)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine("question: " + (question ?? ""));
            if (settings != null)
            {
                sb.AppendLine("backend: " + (settings.BackendMode ?? "?"));
                sb.AppendLine("useLlmForIntentParse: " + settings.UseLlmForIntentParse);
            }

            if (currentFamily?.familyInfo != null)
            {
                sb.AppendLine("selected_family_id: " + currentFamily.familyInfo.FamilyId);
                sb.AppendLine("selected_family_level: " + currentFamily.familyInfo.FamilyLevel);
                sb.AppendLine("selected_name: " + (currentFamily.familyInfo.Name0 ?? ""));
            }
            else
            {
                sb.AppendLine("selected_family: (không)");
            }

            WriteBlock("=== CÂU HỎI ===", sb.ToString());
        }

        public static void LogRuleParse(GiaPhaParseResult parse)
        {
            WriteBlock("=== RULE PARSE ===", FormatParseResult(parse));
        }

        public static void LogQwenRequest(string userPromptPreview)
        {
            string body = "gọi Qwen parse intent…\nuser_prompt_preview:\n" + Truncate(userPromptPreview, 1200);
            WriteBlock("=== QWEN REQUEST ===", body);
        }

        public static void LogQwenResponse(string rawResponse, string extractedJson, GiaPhaIntent mappedIntent, bool parsedOk, string note)
        {
            var sb = new StringBuilder(512);
            sb.AppendLine("parsed_ok: " + parsedOk);
            if (!string.IsNullOrEmpty(note))
            {
                sb.AppendLine("note: " + note);
            }

            sb.AppendLine("raw_response:");
            sb.AppendLine(Truncate(rawResponse, 2000));
            sb.AppendLine();
            sb.AppendLine("extracted_json:");
            sb.AppendLine(string.IsNullOrEmpty(extractedJson) ? "(không trích được)" : extractedJson);
            sb.AppendLine();
            sb.AppendLine("mapped_intent:");
            sb.AppendLine(FormatIntent(mappedIntent));

            WriteBlock("=== QWEN RESPONSE ===", sb.ToString());
        }

        public static void LogQwenSkipped(string reason)
        {
            WriteBlock("=== QWEN PARSE ===", "skipped: " + reason);
        }

        public static void LogExecute(GiaPhaQueryResult result)
        {
            if (result == null)
            {
                WriteBlock("=== FACT ENGINE ===", "result: null");
                return;
            }

            var sb = new StringBuilder(512);
            sb.AppendLine("success: " + result.Success);
            sb.AppendLine("parse_source: " + result.ParseSource);
            if (!string.IsNullOrEmpty(result.StatusHint))
            {
                sb.AppendLine("status: " + result.StatusHint);
            }

            if (result.Intent != null)
            {
                sb.AppendLine("intent:");
                sb.AppendLine(FormatIntent(result.Intent));
            }

            sb.AppendLine();
            sb.AppendLine("answer:");
            sb.AppendLine(Truncate(result.AnswerText, 3000));

            WriteBlock("=== FACT ENGINE ===", sb.ToString());
        }

        public static void LogFailed(string stage, string message)
        {
            WriteBlock("=== LỖI / KHÔNG HIỂU ===", "stage: " + stage + "\n" + (message ?? ""));
        }

        public static string FormatIntent(GiaPhaIntent intent)
        {
            if (intent == null)
            {
                return "(null)";
            }

            var sb = new StringBuilder(128);
            sb.AppendLine("  kind: " + intent.Kind);
            if (intent.Names != null && intent.Names.Count > 0)
            {
                sb.AppendLine("  names: " + string.Join(" | ", intent.Names));
            }

            if (intent.Generation.HasValue)
            {
                sb.AppendLine("  generation: " + intent.Generation.Value);
            }

            if (intent.Year.HasValue)
            {
                sb.AppendLine("  year: " + intent.Year.Value + " (birth=" + intent.IsBirthYear + ")");
            }

            if (intent.AncestorLevels.HasValue)
            {
                sb.AppendLine("  ancestorLevels: " + intent.AncestorLevels.Value);
            }

            if (intent.UseCurrentFamily)
            {
                sb.AppendLine("  useCurrentFamily: true");
            }

            if (!string.IsNullOrEmpty(intent.SpouseRole))
            {
                sb.AppendLine("  spouseRole: " + intent.SpouseRole);
            }

            return sb.ToString().TrimEnd();
        }

        private static string FormatParseResult(GiaPhaParseResult parse)
        {
            if (parse == null)
            {
                return "parse: null";
            }

            var sb = new StringBuilder(128);
            sb.AppendLine("is_parsed: " + parse.IsParsed);
            sb.AppendLine("is_strict: " + parse.IsStrict);
            sb.AppendLine("source: " + parse.Source);
            if (!string.IsNullOrEmpty(parse.Note))
            {
                sb.AppendLine("note: " + parse.Note);
            }

            sb.AppendLine(FormatIntent(parse.Intent));
            return sb.ToString().TrimEnd();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }

            return text.Substring(0, max) + "…";
        }
    }
}
