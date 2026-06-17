using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace vietnamgiapha.AI
{
    /// <summary>Đọc prompt intent từ ai/intent/*.txt — Reload để áp dụng sau khi sửa file.</summary>
    public static class GiaPhaIntentPromptLoader
    {
        public const string IntentFolderName = "intent";

        private static readonly object CacheGate = new object();
        private static GiaPhaIntentRulesSnapshot _cached;

        private static readonly string[] DefaultBlockedKeywords = {
            "chao", "xin chao", "chao ban", "hello", "hi", "hey", "cam on", "camon",
            "thoi tiet", "bao nhieu do", "nhiet do",
            "nau an", "mon an", "cong thuc",
            "lap trinh", "python", "javascript", "code ",
            "viet bai", "tho ", "truyen ",
            "chung khoan", "bitcoin", "gia vang",
            "du lich", "phim ", "bong da",
            "lam sao de", "huong dan", "cach lam",
            "sua loi", "bug ", "windows ", "may tinh"
        };

        public static string IntentDirectory =>
            Path.Combine(LocalLlamaPaths.AiRootDirectory, IntentFolderName);

        public static string SystemPromptPath =>
            Path.Combine(IntentDirectory, "system-prompt.txt");

        public static string IntentMappingPath =>
            Path.Combine(IntentDirectory, "intent-mapping.txt");

        public static string ExamplesPath =>
            Path.Combine(IntentDirectory, "examples.txt");

        public static string UserPromptPath =>
            Path.Combine(IntentDirectory, "user-prompt.txt");

        public static string BlockedKeywordsPath =>
            Path.Combine(IntentDirectory, "blocked-keywords.txt");

        public static string ReadmePath =>
            Path.Combine(IntentDirectory, "README.txt");

        /// <summary>Lấy rules đã cache; lần đầu tự tải từ đĩa.</summary>
        public static GiaPhaIntentRulesSnapshot GetSnapshot()
        {
            lock (CacheGate)
            {
                if (_cached == null)
                {
                    _cached = LoadSnapshotFromDisk();
                }

                return _cached;
            }
        }

        /// <summary>Tải lại toàn bộ rules từ ai/intent — gọi sau khi sửa file txt.</summary>
        public static GiaPhaIntentRulesSnapshot ReloadFromDisk()
        {
            lock (CacheGate)
            {
                _cached = LoadSnapshotFromDisk();
                return _cached;
            }
        }

        public static void EnsureIntentFolderExists()
        {
            if (!Directory.Exists(IntentDirectory))
            {
                Directory.CreateDirectory(IntentDirectory);
            }
        }

        public static string BuildSystemPrompt()
        {
            return GetSnapshot().SystemPrompt ?? "";
        }

        public static string LoadUserPromptHeader()
        {
            return GetSnapshot().UserPromptHeader ?? GetDefaultUserPromptHeader();
        }

        public static string[] LoadBlockedKeywords(string[] fallback)
        {
            string[] fromSnapshot = GetSnapshot().BlockedKeywords;
            if (fromSnapshot != null && fromSnapshot.Length > 0)
            {
                return fromSnapshot;
            }

            return fallback ?? DefaultBlockedKeywords;
        }

        /// <summary>Tóm tắt hiển thị trong chat sau Reload.</summary>
        public static string FormatReloadSummary(GiaPhaIntentRulesSnapshot snap)
        {
            if (snap == null)
            {
                return "❌ Không tải được rules.";
            }

            var sb = new StringBuilder(512);
            sb.AppendLine("↻ Đã tải lại rules Qwen");
            sb.AppendLine("Thư mục: " + snap.IntentDirectory);
            sb.AppendLine("Lúc: " + snap.LoadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Prompt system: ~" + (snap.SystemPrompt?.Length ?? 0) + " ký tự");
            sb.AppendLine("Từ khóa chặn: " + (snap.BlockedKeywords?.Length ?? 0));
            sb.AppendLine();
            sb.AppendLine("File:");

            if (snap.Files != null)
            {
                foreach (GiaPhaIntentRuleFileInfo f in snap.Files)
                {
                    if (f.Exists)
                    {
                        sb.AppendLine("• " + f.FileName + ": " + f.LineCount + " dòng, "
                            + f.CharCount + " ký tự");
                    }
                    else
                    {
                        sb.AppendLine("• " + f.FileName + ": (thiếu — dùng mặc định)");
                    }
                }
            }

            sb.AppendLine();
            sb.AppendLine("Sửa file txt trong thư mục trên → bấm ↻ Rules → hỏi thử lại.");
            return sb.ToString().TrimEnd();
        }

        public static bool TryOpenIntentFolder()
        {
            try
            {
                EnsureIntentFolderExists();
                System.Diagnostics.Process.Start("explorer.exe", IntentDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static GiaPhaIntentRulesSnapshot LoadSnapshotFromDisk()
        {
            EnsureIntentFolderExists();

            var snap = new GiaPhaIntentRulesSnapshot
            {
                LoadedAt = DateTime.Now,
                IntentDirectory = IntentDirectory,
                UserPromptHeader = LoadTextFile(UserPromptPath, GetDefaultUserPromptHeader()).TrimEnd(),
                BlockedKeywords = ReadBlockedKeywordsFromFile(),
                Files = CollectFileInfos()
            };

            snap.SystemPrompt = BuildSystemPromptBody();
            return snap;
        }

        private static string BuildSystemPromptBody()
        {
            var sb = new StringBuilder(8192);
            string system = LoadTextFile(SystemPromptPath, GetDefaultSystemPrompt()).Trim();
            if (system.Length > 0)
            {
                sb.AppendLine(system);
                sb.AppendLine();
            }

            sb.AppendLine("=== WHITELIST kind ===");
            foreach (string k in GiaPhaIntentKinds.AllKindsForLlm())
            {
                sb.AppendLine("- " + k);
            }

            sb.AppendLine();
            sb.AppendLine("=== ÁNH XẠ CÂU TIẾNG VIỆT → kind ===");
            string mapping = LoadTextFile(IntentMappingPath, GetDefaultIntentMapping()).Trim();
            sb.AppendLine(FilterCommentLines(mapping));
            sb.AppendLine();
            sb.AppendLine("=== SCHEMA ===");
            sb.AppendLine(GetJsonSchemaBlock());
            sb.AppendLine();
            sb.AppendLine("=== VÍ DỤ (chỉ học format, không copy tên) ===");
            string examples = LoadTextFile(ExamplesPath, GetDefaultExamples()).Trim();
            sb.AppendLine(FilterCommentLines(examples));

            return sb.ToString();
        }

        private static List<GiaPhaIntentRuleFileInfo> CollectFileInfos()
        {
            var list = new List<GiaPhaIntentRuleFileInfo>();
            string[] names = {
                "system-prompt.txt",
                "intent-mapping.txt",
                "examples.txt",
                "user-prompt.txt",
                "blocked-keywords.txt",
                "README.txt"
            };

            foreach (string name in names)
            {
                string path = Path.Combine(IntentDirectory, name);
                var info = new GiaPhaIntentRuleFileInfo { FileName = name, Exists = File.Exists(path) };
                if (info.Exists)
                {
                    try
                    {
                        string text = File.ReadAllText(path, Encoding.UTF8);
                        info.CharCount = text.Length;
                        info.LineCount = CountLines(text);
                        info.LastWriteUtc = File.GetLastWriteTimeUtc(path);
                    }
                    catch
                    {
                        info.Exists = false;
                    }
                }

                list.Add(info);
            }

            return list;
        }

        private static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }

        private static string[] ReadBlockedKeywordsFromFile()
        {
            if (!File.Exists(BlockedKeywordsPath))
            {
                return DefaultBlockedKeywords;
            }

            try
            {
                var list = new List<string>();
                foreach (string line in File.ReadAllLines(BlockedKeywordsPath, Encoding.UTF8))
                {
                    string t = (line ?? "").Trim();
                    if (t.Length == 0 || t.StartsWith("#"))
                    {
                        continue;
                    }

                    list.Add(t);
                }

                return list.Count > 0 ? list.ToArray() : DefaultBlockedKeywords;
            }
            catch
            {
                return DefaultBlockedKeywords;
            }
        }

        private static string LoadTextFile(string path, string fallback)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path, Encoding.UTF8);
                }
            }
            catch
            {
                // Dùng fallback
            }

            return fallback ?? "";
        }

        private static string FilterCommentLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            var sb = new StringBuilder(text.Length);
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string t = line.Trim();
                    if (t.StartsWith("#"))
                    {
                        continue;
                    }

                    if (sb.Length > 0)
                    {
                        sb.AppendLine();
                    }

                    sb.Append(line);
                }
            }

            return sb.ToString();
        }

        private static string GetJsonSchemaBlock()
        {
            return "{"
                + Environment.NewLine + "  \"kind\": \"Children\","
                + Environment.NewLine + "  \"names\": [\"Nguyễn Văn A\"],"
                + Environment.NewLine + "  \"generation\": null,"
                + Environment.NewLine + "  \"year\": null,"
                + Environment.NewLine + "  \"isBirthYear\": true,"
                + Environment.NewLine + "  \"ancestorLevels\": null,"
                + Environment.NewLine + "  \"useCurrentFamily\": false,"
                + Environment.NewLine + "  \"spouseRole\": null,"
                + Environment.NewLine + "  \"newPersonName\": null"
                + Environment.NewLine + "}";
        }

        private static string GetDefaultSystemPrompt()
        {
            return "Bạn là bộ PHÂN LOẠI câu hỏi tra cứu gia phả Việt Nam."
                + Environment.NewLine + "CHỈ trả về MỘT object JSON hợp lệ.";
        }

        private static string GetDefaultUserPromptHeader()
        {
            return "NHIỆM VỤ: Phân loại CÂU HỎI sau vào đúng 1 intent trong whitelist gia phả."
                + Environment.NewLine + "KHÔNG trả lời nội dung — chỉ JSON intent."
                + Environment.NewLine
                + Environment.NewLine + "NGỮ CẢNH PHẢ ĐỒ (tham khảo, không bịa tên):";
        }

        private static string GetDefaultIntentMapping()
        {
            return "Children: con của X, X có mấy con";
        }

        private static string GetDefaultExamples()
        {
            return "Câu: \"Con của A?\""
                + Environment.NewLine
                + "{\"kind\":\"Children\",\"names\":[\"A\"],\"generation\":null,\"year\":null,\"isBirthYear\":true,\"ancestorLevels\":null,\"useCurrentFamily\":false,\"spouseRole\":null}";
        }
    }
}
