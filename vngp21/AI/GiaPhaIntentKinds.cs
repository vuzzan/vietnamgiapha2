using System;

namespace vietnamgiapha.AI
{
    /// <summary>Loại intent trong miền gia phả — whitelist cho parser rule/LLM.</summary>
    public static class GiaPhaIntentKinds
    {
        public const string Unknown = "Unknown";
        public const string ThuyTo = "ThuyTo";
        public const string CountPeople = "CountPeople";
        public const string NoDescendants = "NoDescendants";
        public const string ByBirthYear = "ByBirthYear";
        public const string ByDeathYear = "ByDeathYear";
        public const string GenerationList = "GenerationList";
        public const string CurrentFamily = "CurrentFamily";
        public const string Relationship = "Relationship";
        public const string Children = "Children";
        public const string Parents = "Parents";
        public const string Mother = "Mother";
        public const string Spouse = "Spouse";
        public const string Siblings = "Siblings";
        public const string Descendants = "Descendants";
        public const string MemorialDay = "MemorialDay";
        public const string Grave = "Grave";
        public const string GenerationRank = "GenerationRank";
        public const string Note = "Note";
        public const string Ancestor = "Ancestor";
        public const string FullDescendants = "FullDescendants";
        public const string CourtesyName = "CourtesyName";
        public const string PersonSummary = "PersonSummary";
        public const string Search = "Search";

        // ── Lệnh sửa gia phả (thực thi trên cây, không tra cứu) ─────────────
        public const string AddPerson = "AddPerson";
        public const string AddChildFamily = "AddChildFamily";
        public const string AddSiblingEm = "AddSiblingEm";
        public const string AddSiblingAnh = "AddSiblingAnh";
        public const string RenamePerson = "RenamePerson";
        public const string DeletePerson = "DeletePerson";

        public static bool IsKnown(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind) && kind != Unknown;
        }

        /// <summary>Intent thuộc nhóm biên tập — orchestrator gọi edit executor thay vì fact query.</summary>
        public static bool IsEditAction(string kind)
        {
            return kind == AddPerson
                || kind == AddChildFamily
                || kind == AddSiblingEm
                || kind == AddSiblingAnh
                || kind == RenamePerson
                || kind == DeletePerson;
        }

        /// <summary>Chuẩn hóa kind Qwen trả về (alias tiếng Việt / sai hoa thường).</summary>
        public static string TryNormalizeKind(string rawKind)
        {
            if (string.IsNullOrWhiteSpace(rawKind))
            {
                return Unknown;
            }

            string k = rawKind.Trim();
            foreach (string known in AllKindsForLlm())
            {
                if (string.Equals(known, k, StringComparison.OrdinalIgnoreCase))
                {
                    return known;
                }
            }

            string lower = k.ToLowerInvariant();
            switch (lower)
            {
                case "con":
                case "childrens":
                case "child":
                    return Children;
                case "cha":
                case "ba":
                case "father":
                case "parents":
                    return Parents;
                case "me":
                case "mother":
                    return Mother;
                case "vo":
                case "wife":
                    return Spouse;
                case "chong":
                case "husband":
                    return Spouse;
                case "anh em":
                case "sibling":
                case "siblings":
                    return Siblings;
                case "chau":
                case "descendant":
                    return Descendants;
                case "hau due":
                case "descendants":
                    return FullDescendants;
                case "to tien":
                case "thuy to":
                    return ThuyTo;
                case "dem nguoi":
                case "count":
                    return CountPeople;
                case "doi":
                case "generation":
                    return GenerationList;
                case "quan he":
                case "relationship":
                    return Relationship;
                case "tim":
                case "search":
                case "person":
                    return Search;
                case "thong tin":
                case "summary":
                    return PersonSummary;
                case "them nguoi":
                case "addperson":
                case "add person":
                    return AddPerson;
                case "them gia dinh con":
                case "addchildfamily":
                    return AddChildFamily;
                case "them em":
                case "addsiblingem":
                    return AddSiblingEm;
                case "them anh":
                case "addsiblinganh":
                    return AddSiblingAnh;
                case "doi ten":
                case "sua ten":
                case "renameperson":
                    return RenamePerson;
                case "xoa nguoi":
                case "deleteperson":
                    return DeletePerson;
                default:
                    return Unknown;
            }
        }

        public static string[] AllKindsForLlm()
        {
            return new[]
            {
                ThuyTo, CountPeople, NoDescendants, ByBirthYear, ByDeathYear, GenerationList,
                CurrentFamily, Relationship, Children, Parents, Mother, Spouse, Siblings,
                Descendants, MemorialDay, Grave, GenerationRank, Note, Ancestor,
                FullDescendants, CourtesyName, PersonSummary, Search,
                AddPerson, AddChildFamily, AddSiblingEm, AddSiblingAnh, RenamePerson, DeletePerson,
                Unknown
            };
        }
    }
}
