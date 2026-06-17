using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace vietnamgiapha.AI
{
    /// <summary>Rule engine C# — đọc từ ai/rules/*.txt, Reload để áp dụng sau khi sửa file.</summary>
    public static class GiaPhaRuleEngineLoader
    {
        public const string RulesFolderName = "rules";

        private static readonly object CacheGate = new object();
        private static GiaPhaRuleEngineSnapshot _cached;

        public static string RulesDirectory =>
            Path.Combine(LocalLlamaPaths.AiRootDirectory, RulesFolderName);

        public static GiaPhaRuleEngineSnapshot GetSnapshot()
        {
            lock (CacheGate)
            {
                if (_cached == null)
                {
                    _cached = LoadFromDisk();
                }

                return _cached;
            }
        }

        public static GiaPhaRuleEngineSnapshot ReloadFromDisk()
        {
            lock (CacheGate)
            {
                _cached = LoadFromDisk();
                return _cached;
            }
        }

        public static void EnsureRulesFolderExists()
        {
            if (!Directory.Exists(RulesDirectory))
            {
                Directory.CreateDirectory(RulesDirectory);
            }
        }

        public static bool TryOpenRulesFolder()
        {
            try
            {
                EnsureRulesFolderExists();
                System.Diagnostics.Process.Start("explorer.exe", RulesDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string FormatReloadSummary(GiaPhaRuleEngineSnapshot snap)
        {
            if (snap == null)
            {
                return "❌ Không tải được rule base.";
            }

            var sb = new StringBuilder(384);
            sb.AppendLine("↻ Đã tải lại rule base (engine)");
            sb.AppendLine("Thư mục: " + snap.RulesDirectory);
            sb.AppendLine("Lúc: " + snap.LoadedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Top intent: " + snap.TopIntentRules.Count + " nhóm");
            sb.AppendLine("Detail intent: " + snap.DetailIntentRules.Count + " nhóm");
            sb.AppendLine("Edit actions: " + snap.EditIntentRules.Count + " nhóm");
            sb.AppendLine("Prefix tách tên: " + snap.ExtractNamePrefixes.Length);
            sb.AppendLine("Suffix tách tên: " + snap.ExtractNameSuffixes.Length);
            sb.AppendLine("Stopword (không coi là tên): " + snap.Stopwords.Length);
            return sb.ToString().TrimEnd();
        }

        /// <summary>Câu hỏi gốc — bỏ dấu rồi so với stopwords.txt.</summary>
        public static bool MatchesStopwordQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return false;
            }

            return MatchesStopword(NormalizeForMatch(question));
        }

        /// <summary>Câu/cụm đã bỏ dấu lowercase — khớp stopwords thì rule fail → có thể gọi Qwen.</summary>
        public static bool MatchesStopword(string normalizedNoDiacritics)
        {
            if (string.IsNullOrWhiteSpace(normalizedNoDiacritics))
            {
                return false;
            }

            string t = normalizedNoDiacritics.Trim().ToLowerInvariant();
            string[] list = GetSnapshot().Stopwords;
            foreach (string sw in list)
            {
                if (t == sw)
                {
                    return true;
                }
            }

            return false;
        }

        private static GiaPhaRuleEngineSnapshot LoadFromDisk()
        {
            EnsureRulesFolderExists();

            var snap = new GiaPhaRuleEngineSnapshot
            {
                LoadedAt = DateTime.Now,
                RulesDirectory = RulesDirectory,
                TopIntentRules = ParseKeywordFile(
                    Path.Combine(RulesDirectory, "top-intent.txt"),
                    GetDefaultTopIntent()),
                DetailIntentRules = ParseKeywordFile(
                    Path.Combine(RulesDirectory, "detail-intent.txt"),
                    GetDefaultDetailIntent()),
                EditIntentRules = ParseKeywordFile(
                    Path.Combine(RulesDirectory, "edit-actions.txt"),
                    GetDefaultEditIntent()),
                ExtractNamePrefixes = LoadLineList(
                    Path.Combine(RulesDirectory, "extract-name-prefixes.txt"),
                    GetDefaultPrefixes()),
                ExtractNameSuffixes = LoadLineList(
                    Path.Combine(RulesDirectory, "extract-name-suffixes.txt"),
                    GetDefaultSuffixes()),
                Stopwords = LoadNormalizedStopwords(
                    Path.Combine(RulesDirectory, "stopwords.txt"),
                    GetDefaultStopwords())
            };

            // Prefix/suffix dài hơn ưu tiên — tránh cắt nhầm (vd. "con của" trước "con")
            Array.Sort(snap.ExtractNamePrefixes, (a, b) =>
                (b?.Length ?? 0).CompareTo(a?.Length ?? 0));
            Array.Sort(snap.ExtractNameSuffixes, (a, b) =>
                (b?.Length ?? 0).CompareTo(a?.Length ?? 0));

            return snap;
        }

        private static List<GiaPhaRuleKeywordEntry> ParseKeywordFile(
            string path,
            List<GiaPhaRuleKeywordEntry> fallback)
        {
            if (!File.Exists(path))
            {
                return fallback;
            }

            try
            {
                var list = new List<GiaPhaRuleKeywordEntry>();
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    GiaPhaRuleKeywordEntry entry = ParseKeywordLine(raw);
                    if (entry != null)
                    {
                        list.Add(entry);
                    }
                }

                return list.Count > 0 ? list : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        /// <summary>Dòng: Kind: kw1, kw2 | Kind|ancestorLevels=2: kw | Kind|spouse=Vợ: kw</summary>
        private static GiaPhaRuleKeywordEntry ParseKeywordLine(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            string line = raw.Trim();
            if (line.StartsWith("#"))
            {
                return null;
            }

            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                return null;
            }

            string head = line.Substring(0, colon).Trim();
            string kwPart = line.Substring(colon + 1).Trim();
            if (kwPart.Length == 0)
            {
                return null;
            }

            var entry = new GiaPhaRuleKeywordEntry();
            string[] headParts = head.Split('|');
            entry.Kind = headParts[0].Trim();

            for (int i = 1; i < headParts.Length; i++)
            {
                string opt = headParts[i].Trim();
                if (opt.Equals("useCurrentFamily", StringComparison.OrdinalIgnoreCase))
                {
                    entry.UseCurrentFamily = true;
                    continue;
                }

                int eq = opt.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                string key = opt.Substring(0, eq).Trim();
                string val = opt.Substring(eq + 1).Trim();
                if (key.Equals("ancestorLevels", StringComparison.OrdinalIgnoreCase))
                {
                    int levels;
                    if (int.TryParse(val, out levels))
                    {
                        entry.AncestorLevels = levels;
                    }
                }
                else if (key.Equals("spouse", StringComparison.OrdinalIgnoreCase))
                {
                    entry.SpouseRole = val;
                }
            }

            var keywords = new List<string>();
            foreach (string piece in kwPart.Split(','))
            {
                string kw = piece.Trim();
                if (kw.Length > 0)
                {
                    keywords.Add(kw);
                }
            }

            if (keywords.Count == 0 || string.IsNullOrWhiteSpace(entry.Kind))
            {
                return null;
            }

            entry.Keywords = keywords.ToArray();
            return entry;
        }

        private static string[] LoadNormalizedStopwords(string path, string[] fallback)
        {
            string[] raw = LoadLineList(path, fallback);
            var normalized = new List<string>(raw.Length);
            foreach (string line in raw)
            {
                string n = NormalizeForMatch(line);
                if (n.Length > 0)
                {
                    normalized.Add(n);
                }
            }

            return normalized.Count > 0 ? normalized.ToArray() : new string[0];
        }

        private static string NormalizeForMatch(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "";
            }

            string lower = text.Trim().ToLowerInvariant();
            string formD = lower.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(formD.Length);
            foreach (char c in formD)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        private static string[] LoadLineList(string path, string[] fallback)
        {
            if (!File.Exists(path))
            {
                return fallback;
            }

            try
            {
                var list = new List<string>();
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string t = (raw ?? "").Trim();
                    if (t.Length == 0 || t.StartsWith("#"))
                    {
                        continue;
                    }

                    list.Add(t);
                }

                return list.Count > 0 ? list.ToArray() : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static List<GiaPhaRuleKeywordEntry> GetDefaultTopIntent()
        {
            return new List<GiaPhaRuleKeywordEntry>
            {
                Entry(GiaPhaIntentKinds.ThuyTo, "thuy to", "thuythu", "to tien", "thuy-to"),
                Entry(GiaPhaIntentKinds.CountPeople, "bao nhieu", "tong so", "so nguoi", "tong cong", "dem nguoi"),
                Entry(GiaPhaIntentKinds.NoDescendants, "chua co con", "khong co con", "vo hau", "tuyet tu", "khong con"),
                Entry(GiaPhaIntentKinds.CurrentFamily, true, "nguoi nay", "gia dinh nay", "day", "nguoi dang chon", "hien tai")
            };
        }

        private static List<GiaPhaRuleKeywordEntry> GetDefaultEditIntent()
        {
            return new List<GiaPhaRuleKeywordEntry>
            {
                Entry(GiaPhaIntentKinds.AddPerson, true,
                    "them nguoi vao gia dinh", "them nguoi vao gia pha", "them thanh vien",
                    "them nguoi moi", "chen nguoi", "insert nguoi", "them vo", "them chong",
                    "them nguoi ten", "them thanh vien vao"),
                Entry(GiaPhaIntentKinds.AddChildFamily, true,
                    "them gia dinh con", "them doi con", "them nhanh con", "them con moi",
                    "tao gia dinh con", "tao doi con"),
                Entry(GiaPhaIntentKinds.AddSiblingEm, true,
                    "them gia dinh em", "them em", "tao gia dinh em"),
                Entry(GiaPhaIntentKinds.AddSiblingAnh, true,
                    "them gia dinh anh", "them anh", "tao gia dinh anh"),
                Entry(GiaPhaIntentKinds.RenamePerson,
                    "doi ten", "sua ten", "dat ten", "doi ten thanh", "sua ten thanh"),
                Entry(GiaPhaIntentKinds.DeletePerson,
                    "xoa nguoi", "go nguoi", "xoa thanh vien", "loai nguoi")
            };
        }

        private static List<GiaPhaRuleKeywordEntry> GetDefaultDetailIntent()
        {
            var list = new List<GiaPhaRuleKeywordEntry>();
            list.Add(Entry(GiaPhaIntentKinds.Children, "con cua", "con cai", "con trai", "con gai", "co may con"));
            list.Add(Entry(GiaPhaIntentKinds.Parents, "cha cua", "ba cua", "cha la ai", "ba la ai"));
            list.Add(Entry(GiaPhaIntentKinds.Mother, "me cua", "ma cua", "me la ai"));
            list.Add(Entry(GiaPhaIntentKinds.Spouse, "Vợ", "vo cua", "vo la ai", "nguoi vo"));
            list.Add(Entry(GiaPhaIntentKinds.Spouse, "Chồng", "chong cua", "chong la ai", "nguoi chong"));
            list.Add(Entry(GiaPhaIntentKinds.Siblings, "anh em cua", "anh chi em cua", "anh cua", "em cua", "chi cua"));
            list.Add(Entry(GiaPhaIntentKinds.Descendants, "chau cua", "chau noi", "chau ngoai"));
            list.Add(Entry(GiaPhaIntentKinds.MemorialDay, "ngay ky", "ngay gio", "ky nhat", "gio", "ngay mat", "ngay chet", "ngay qua doi"));
            list.Add(Entry(GiaPhaIntentKinds.Grave, "mo ", "phan mo", "lang mo", "an tang", "chon cat"));
            list.Add(Entry(GiaPhaIntentKinds.GenerationRank, "doi may", "the he thu", "thu may"));
            list.Add(Entry(GiaPhaIntentKinds.Note, "ghi chu", "chi tiet", "mo ta", "ghi nhan"));
            list.Add(Entry(GiaPhaIntentKinds.Ancestor, 2, "ong noi", "ba noi"));
            list.Add(Entry(GiaPhaIntentKinds.Ancestor, 3, "ong co", "ba co", "cu ong", "cu ba", "ong cu", "ba cu", "ong to", "ba to"));
            list.Add(Entry(GiaPhaIntentKinds.Ancestor, 4, "ky cua", "ky la ai"));
            list.Add(Entry(GiaPhaIntentKinds.FullDescendants,
                "hau due", "con chau", "dong doi", "tat ca con chau", "toan bo con chau",
                "con chau la ai", "co may doi con chau", "may doi con chau", "bao nhieu doi con chau",
                "co may doi", "bao nhieu doi"));
            list.Add(Entry(GiaPhaIntentKinds.CourtesyName, "ten tu", "ten thuy", "ten thuong", "ten huy", "biet danh"));
            list.Add(Entry(GiaPhaIntentKinds.Search, "tim ", "tim kiem", "tra cuu", "thong tin ve", "thong tin cua", "hoi ve", "cho biet ve", "cho toi biet ve"));
            list.Add(Entry(GiaPhaIntentKinds.PersonSummary, "ai la", "la ai", "thong tin day du", "tom tat"));
            return list;
        }

        private static GiaPhaRuleKeywordEntry Entry(string kind, params string[] keywords)
        {
            return new GiaPhaRuleKeywordEntry { Kind = kind, Keywords = keywords };
        }

        private static GiaPhaRuleKeywordEntry Entry(string kind, bool useCurrentFamily, params string[] keywords)
        {
            return new GiaPhaRuleKeywordEntry
            {
                Kind = kind,
                UseCurrentFamily = useCurrentFamily,
                Keywords = keywords
            };
        }

        private static GiaPhaRuleKeywordEntry Entry(string kind, string spouse, params string[] keywords)
        {
            return new GiaPhaRuleKeywordEntry
            {
                Kind = kind,
                SpouseRole = spouse,
                Keywords = keywords
            };
        }

        private static GiaPhaRuleKeywordEntry Entry(string kind, int ancestorLevels, params string[] keywords)
        {
            return new GiaPhaRuleKeywordEntry
            {
                Kind = kind,
                AncestorLevels = ancestorLevels,
                Keywords = keywords
            };
        }

        private static string[] GetDefaultPrefixes()
        {
            return new[]
            {
                "con cái của ", "con cai cua ", "con của ", "con cua ",
                "anh chị em của ", "anh chi em cua ", "anh em của ", "anh em cua ",
                "cháu nội của ", "chau noi cua ", "cháu của ", "chau cua ",
                "ngày giỗ của ", "ngay gio cua ", "ngày giỗ ", "ngay gio ",
                "ngày mất của ", "ngay mat cua ", "ngày mất ", "ngay mat ",
                "ngày chết của ", "ngay chet cua ",
                "ngày kỵ của ", "ngay ky cua ", "ngày kỵ ", "ngay ky ",
                "phần mộ của ", "phan mo cua ", "mộ của ", "mo cua ",
                "đời mấy của ", "doi may cua ", "đời mấy ", "doi may ",
                "ông nội của ", "ong noi cua ", "bà nội của ", "ba noi cua ",
                "ông cố của ", "ong co cua ", "bà cố của ", "ba co cua ",
                "ông cụ của ", "ong cu cua ", "bà cụ của ", "ba cu cua ",
                "con cháu của ", "con chau cua ",
                "hậu duệ của ", "hau due cua ",
                "tên thụy của ", "ten thuy cua ", "tên tự của ", "ten tu cua ",
                "tên thường của ", "ten thuong cua ",
                "thông tin về ", "thong tin ve ", "thông tin của ", "thong tin cua ",
                "ghi chú về ", "ghi chu ve ", "ghi chú của ", "ghi chu cua ",
                "cho tôi biết về ", "cho toi biet ve ",
                "cho em biết ", "cho em biet ", "cho mình biết ", "cho minh biet ",
                "xin cho biết ", "xin cho biet ",
                "cha của ", "cha cua ", "ba của ", "ba cua ",
                "mẹ ruột của ", "me cua ", "mẹ của ", "ma cua ",
                "chồng của ", "chong cua ", "chồng là ", "chong la ",
                "vợ của ", "vo cua ", "vợ là ", "vo la ",
                "nhà ông ", "nha ong ", "nhà bà ", "nha ba ",
                "kỵ của ", "ky cua ",
                "tra cứu ", "tra cuu ", "tìm ", "tim ",
                "hỏi về ", "hoi ve ",
                "ông ", "ong ", "bà ", "ba "
            };
        }

        private static string[] GetDefaultStopwords()
        {
            return new[]
            {
                "chao", "xin chao", "chao ban", "xin chao ban",
                "hello", "hi", "hey", "ok", "oke",
                "cam on", "camon", "thanks", "thank you",
                "giup toi", "giup minh", "help",
                "test", "thu", "abc"
            };
        }

        private static string[] GetDefaultSuffixes()
        {
            return new[]
            {
                " có mấy đời con cháu?", " co may doi con chau?",
                " có mấy đời con cháu", " co may doi con chau",
                " bao nhiêu đời con cháu?", " bao nhieu doi con chau?",
                " bao nhiêu đời con cháu", " bao nhieu doi con chau",
                " bao nhiêu đời?", " bao nhieu doi?",
                " bao nhiêu đời", " bao nhieu doi",
                " có mấy đời?", " co may doi?",
                " có mấy đời", " co may doi",
                " là ai?", " la ai?", " là ai", " la ai",
                " đời mấy?", " doi may?", " đời mấy", " doi may",
                " ở đâu?", " o dau?", " ở đâu", " o dau",
                " có ai?", " co ai?", "?", "!"
            };
        }
    }

    public sealed class GiaPhaRuleEngineSnapshot
    {
        public DateTime LoadedAt { get; set; }
        public string RulesDirectory { get; set; }
        public List<GiaPhaRuleKeywordEntry> TopIntentRules { get; set; } = new List<GiaPhaRuleKeywordEntry>();
        public List<GiaPhaRuleKeywordEntry> DetailIntentRules { get; set; } = new List<GiaPhaRuleKeywordEntry>();
        public List<GiaPhaRuleKeywordEntry> EditIntentRules { get; set; } = new List<GiaPhaRuleKeywordEntry>();
        public string[] ExtractNamePrefixes { get; set; } = new string[0];
        public string[] ExtractNameSuffixes { get; set; } = new string[0];
        public string[] Stopwords { get; set; } = new string[0];
    }

    public sealed class GiaPhaRuleKeywordEntry
    {
        public string Kind { get; set; }
        public int? AncestorLevels { get; set; }
        public string SpouseRole { get; set; }
        public bool UseCurrentFamily { get; set; }
        public string[] Keywords { get; set; }
    }
}
