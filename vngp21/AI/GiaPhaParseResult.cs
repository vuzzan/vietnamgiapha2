namespace vietnamgiapha.AI
{
    public enum GiaPhaParseSource
    {
        Rule,
        LocalLlm,
        Failed
    }

    /// <summary>Kết quả parse câu hỏi → intent.</summary>
    public sealed class GiaPhaParseResult
    {
        public GiaPhaIntent Intent { get; set; }
        public GiaPhaParseSource Source { get; set; }
        public string Note { get; set; }

        public bool IsParsed =>
            Intent != null && GiaPhaIntentKinds.IsKnown(Intent.Kind);

        /// <summary>Rule khớp chặt (regex/mẫu rõ) — false = weak, orchestrator ưu tiên Qwen.</summary>
        public bool IsStrict { get; set; }
    }
}
