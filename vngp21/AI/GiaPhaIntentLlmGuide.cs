using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace vietnamgiapha.AI
{
    /// <summary>
    /// Phạm vi intent + quy tắc cho Qwen — đồng bộ với rule engine, không trả lời ngoài miền gia phả.
    /// </summary>
    public static class GiaPhaIntentLlmGuide
    {
        /// <summary>Prompt hệ thống — đọc từ ai/intent/*.txt (xem GiaPhaIntentPromptLoader).</summary>
        public static string BuildSystemPrompt()
        {
            return GiaPhaIntentPromptLoader.BuildSystemPrompt();
        }

        /// <summary>Hậu xử lý intent Qwen — chuẩn hóa kind, điền trường thiếu theo rule.</summary>
        public static GiaPhaIntent NormalizeAfterParse(GiaPhaIntent intent, string question)
        {
            if (intent == null)
            {
                return GiaPhaIntent.Unknown(question);
            }

            if (IsOutOfDomainQuestion(question)
                || GiaPhaRuleEngineLoader.MatchesStopwordQuestion(question))
            {
                return GiaPhaIntent.Unknown(question);
            }

            intent.Kind = GiaPhaIntentKinds.TryNormalizeKind(intent.Kind);
            if (!GiaPhaIntentKinds.IsKnown(intent.Kind))
            {
                return GiaPhaIntent.Unknown(question);
            }

            string q = RemoveDiacritics((question ?? "").ToLowerInvariant());

            if (intent.Kind == GiaPhaIntentKinds.Spouse)
            {
                intent.SpouseRole = NormalizeSpouseRole(intent.SpouseRole, q);
            }

            if (intent.Kind == GiaPhaIntentKinds.Ancestor && !intent.AncestorLevels.HasValue)
            {
                intent.AncestorLevels = InferAncestorLevels(q);
            }

            if (intent.Kind == GiaPhaIntentKinds.CurrentFamily
                || ContainsAny(q, "nguoi nay", "gia dinh nay", "dang chon", "hien tai", "day", "dang xem"))
            {
                intent.UseCurrentFamily = true;
            }

            if (GiaPhaIntentKinds.IsEditAction(intent.Kind))
            {
                FillEditFieldsFromQuestion(intent, question, q);
            }

            if (NeedsPersonName(intent.Kind) && (intent.Names == null || intent.Names.Count == 0))
            {
                string name = TryExtractPersonName(question);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    intent.Names.Add(name);
                }
            }

            if (intent.Kind == GiaPhaIntentKinds.Relationship && intent.Names.Count < 2)
            {
                string n1;
                string n2;
                if (TryExtractTwoNames(question, out n1, out n2))
                {
                    intent.Names.Clear();
                    intent.Names.Add(n1);
                    intent.Names.Add(n2);
                }
            }

            if (intent.Kind == GiaPhaIntentKinds.GenerationList && !intent.Generation.HasValue)
            {
                var m = Regex.Match(q, @"doi\s*(\d+)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int level))
                {
                    intent.Generation = level;
                }
            }

            if (intent.Kind == GiaPhaIntentKinds.ByBirthYear && !intent.Year.HasValue)
            {
                var m = Regex.Match(q, @"sinh\s*(?:nam)?\s*(\d{4})");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int y))
                {
                    intent.Year = y;
                    intent.IsBirthYear = true;
                }
            }

            if (intent.Kind == GiaPhaIntentKinds.ByDeathYear && !intent.Year.HasValue)
            {
                var m = Regex.Match(q, @"(?:mat|chet|qua doi|tu tran|ky)\s*(?:nam)?\s*(\d{4})");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int y))
                {
                    intent.Year = y;
                    intent.IsBirthYear = false;
                }
            }

            // Qwen hay bịa tên từ ngữ cảnh (GĐ #1…) — tên phải xuất hiện trong câu hỏi
            if (!ValidateNamesPresentInQuestion(intent, question, q))
            {
                return GiaPhaIntent.Unknown(question);
            }

            return intent;
        }

        /// <summary>Tên trong intent phải có trong câu hỏi — chống hallucination từ context.</summary>
        private static bool ValidateNamesPresentInQuestion(GiaPhaIntent intent, string question, string qNormalized)
        {
            if (intent == null || intent.Names == null || intent.Names.Count == 0)
            {
                return true;
            }

            if (intent.UseCurrentFamily
                || intent.Kind == GiaPhaIntentKinds.CurrentFamily
                || intent.Kind == GiaPhaIntentKinds.ThuyTo
                || intent.Kind == GiaPhaIntentKinds.CountPeople
                || intent.Kind == GiaPhaIntentKinds.NoDescendants
                || intent.Kind == GiaPhaIntentKinds.GenerationList
                || intent.Kind == GiaPhaIntentKinds.ByBirthYear
                || intent.Kind == GiaPhaIntentKinds.ByDeathYear)
            {
                return true;
            }

            // Lệnh sửa cấu trúc — không bắt buộc tên trong câu
            if (intent.Kind == GiaPhaIntentKinds.AddPerson
                || intent.Kind == GiaPhaIntentKinds.AddChildFamily
                || intent.Kind == GiaPhaIntentKinds.AddSiblingEm
                || intent.Kind == GiaPhaIntentKinds.AddSiblingAnh
                || intent.Kind == GiaPhaIntentKinds.DeletePerson)
            {
                if (intent.UseCurrentFamily || intent.Names == null || intent.Names.Count == 0)
                {
                    return true;
                }
            }

            string q = qNormalized ?? RemoveDiacritics((question ?? "").ToLowerInvariant());
            foreach (string name in intent.Names)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                // Bỏ qua mã ngữ cảnh Qwen hay bịa
                string nameNorm = RemoveDiacritics(name.ToLowerInvariant());
                if (nameNorm.Contains("gd #") || nameNorm.StartsWith("doi "))
                {
                    return false;
                }

                if (!q.Contains(nameNorm))
                {
                    return false;
                }
            }

            return true;
        }

        private static void FillEditFieldsFromQuestion(GiaPhaIntent intent, string question, string q)
        {
            if (intent == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(intent.NewPersonName))
            {
                var mTen = Regex.Match(q, @"(?:ten|tên)\s+(.+)$");
                if (mTen.Success)
                {
                    intent.NewPersonName = mTen.Groups[1].Value.Trim();
                }
            }

            var mRename = Regex.Match(
                q,
                @"(?:doi|sua)\s+ten(?:\s+cua)?\s+(.+?)\s+(?:thanh|than)\s+(.+)");
            if (mRename.Success)
            {
                if (intent.Names == null)
                {
                    intent.Names = new List<string>();
                }

                if (intent.Names.Count == 0)
                {
                    intent.Names.Add(mRename.Groups[1].Value.Trim());
                }

                intent.NewPersonName = mRename.Groups[2].Value.Trim();
            }

            if (ContainsAny(q, "gia dinh nay", "dang chon", "hien tai", "vao gia dinh", "vao gia pha"))
            {
                intent.UseCurrentFamily = true;
            }
        }

        private static bool NeedsPersonName(string kind)
        {
            switch (kind)
            {
                case GiaPhaIntentKinds.Children:
                case GiaPhaIntentKinds.Parents:
                case GiaPhaIntentKinds.Mother:
                case GiaPhaIntentKinds.Spouse:
                case GiaPhaIntentKinds.Siblings:
                case GiaPhaIntentKinds.Descendants:
                case GiaPhaIntentKinds.FullDescendants:
                case GiaPhaIntentKinds.Ancestor:
                case GiaPhaIntentKinds.MemorialDay:
                case GiaPhaIntentKinds.Grave:
                case GiaPhaIntentKinds.GenerationRank:
                case GiaPhaIntentKinds.CourtesyName:
                case GiaPhaIntentKinds.Note:
                case GiaPhaIntentKinds.PersonSummary:
                case GiaPhaIntentKinds.Search:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsOutOfDomainQuestion(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return true;
            }

            string q = RemoveDiacritics(question.ToLowerInvariant());
            string[] blocked = GiaPhaIntentPromptLoader.LoadBlockedKeywords(null);

            foreach (string b in blocked)
            {
                if (q.Contains(b))
                {
                    return true;
                }
            }

            return false;
        }

        private static string NormalizeSpouseRole(string role, string qNormalized)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                string r = role.Trim().ToLowerInvariant();
                if (r.Contains("chong") || r == "chồng")
                {
                    return "Chồng";
                }
                if (r.Contains("vo") || r == "vợ")
                {
                    return "Vợ";
                }
            }

            if (ContainsAny(qNormalized, "chong cua", "chong la", "nguoi chong"))
            {
                return "Chồng";
            }

            return "Vợ";
        }

        private static int InferAncestorLevels(string qNormalized)
        {
            if (ContainsAny(qNormalized, "ky cua", "ky la ai"))
            {
                return 4;
            }

            if (ContainsAny(qNormalized, "ong co", "ba co", "cu ong", "cu ba", "ong cu", "ba cu", "ong to", "ba to"))
            {
                return 3;
            }

            if (ContainsAny(qNormalized, "ong noi", "ba noi"))
            {
                return 2;
            }

            return 2;
        }

        private static string TryExtractPersonName(string originalQuestion)
        {
            if (string.IsNullOrWhiteSpace(originalQuestion))
            {
                return null;
            }

            string[] prefixes = {
                "con của ", "con cua ", "cha của ", "cha cua ", "ba của ", "ba cua ",
                "mẹ của ", "me cua ", "ma cua ", "vợ của ", "vo cua ",
                "chồng của ", "chong cua ", "anh em của ", "anh em cua ",
                "cháu của ", "chau cua ", "ngày kỵ của ", "ngay ky cua ",
                "mộ của ", "mo cua ", "đời mấy của ", "doi may cua ",
                "ông nội của ", "ong noi cua ", "bà nội của ", "ba noi cua ",
                "kỵ của ", "ky cua ", "hậu duệ của ", "hau due cua ",
                "tên tự của ", "ten tu cua ", "thông tin về ", "thong tin ve ",
                "tìm ", "tim ", "ai là con của ", "ai la con cua ",
                "ai sinh ra ", "ai co con ten "
            };

            string s = originalQuestion.Trim();
            foreach (string pre in prefixes)
            {
                if (s.StartsWith(pre, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(pre.Length).Trim();
                    break;
                }
            }

            string[] suffixes = { " là ai?", " la ai?", "?", " ở đâu?", " o dau?" };
            foreach (string suf in suffixes)
            {
                if (s.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - suf.Length).Trim();
                    break;
                }
            }

            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private static bool TryExtractTwoNames(string original, out string n1, out string n2)
        {
            n1 = n2 = null;
            if (string.IsNullOrWhiteSpace(original))
            {
                return false;
            }

            string s = original.Trim();
            string[] removeSuffix = {
                " có quan hệ gì?", " co quan he gi?", " quan hệ gì?", " quan he gi?", "?"
            };
            foreach (string suf in removeSuffix)
            {
                if (s.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - suf.Length).Trim();
                    break;
                }
            }

            string[] separators = { " và ", " va ", " với ", " voi ", " & " };
            foreach (string sep in separators)
            {
                int idx = s.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (idx > 0)
                {
                    n1 = s.Substring(0, idx).Trim();
                    n2 = s.Substring(idx + sep.Length).Trim();
                    return !string.IsNullOrWhiteSpace(n1) && !string.IsNullOrWhiteSpace(n2);
                }
            }

            return false;
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            foreach (string n in needles)
            {
                if (text.Contains(n))
                {
                    return true;
                }
            }

            return false;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text ?? "";
            }

            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (char c in normalized)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
