namespace vietnamgiapha.AI
{
    /// <summary>Kết quả fact từ engine — có thể kèm FactsPack cho LLM polish sau.</summary>
    public sealed class GiaPhaQueryResult
    {
        public bool Success { get; set; }
        public string AnswerText { get; set; }
        public string FactsPack { get; set; }
        public GiaPhaIntent Intent { get; set; }
        public GiaPhaParseSource ParseSource { get; set; }
        public string StatusHint { get; set; }
        /// <summary>Gia đình bị ảnh hưởng sau lệnh sửa — UI chọn lại và rebuild index.</summary>
        public FamilyViewModel AffectedFamily { get; set; }

        public static GiaPhaQueryResult Fail(string message)
        {
            return new GiaPhaQueryResult
            {
                Success = false,
                AnswerText = message,
                FactsPack = message
            };
        }
    }
}
