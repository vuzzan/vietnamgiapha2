using System.Collections.Generic;

namespace vietnamgiapha.AI
{
    /// <summary>Ý định câu hỏi gia phả — map sang engine fact.</summary>
    public sealed class GiaPhaIntent
    {
        public string Kind { get; set; } = GiaPhaIntentKinds.Unknown;
        public List<string> Names { get; set; } = new List<string>();
        public int? Generation { get; set; }
        public int? Year { get; set; }
        public bool IsBirthYear { get; set; }
        /// <summary>Số cấp tổ tiên (2=ông nội, 3=cụ, 4=kỵ).</summary>
        public int? AncestorLevels { get; set; }
        public bool UseCurrentFamily { get; set; }
        /// <summary>Vợ hoặc Chồng khi Kind=Spouse.</summary>
        public string SpouseRole { get; set; }
        /// <summary>Tên mới khi AddPerson (tên cụ thể) hoặc RenamePerson.</summary>
        public string NewPersonName { get; set; }
        public string RawQuestion { get; set; }

        public static GiaPhaIntent Unknown(string question)
        {
            return new GiaPhaIntent
            {
                Kind = GiaPhaIntentKinds.Unknown,
                RawQuestion = question ?? ""
            };
        }
    }
}
