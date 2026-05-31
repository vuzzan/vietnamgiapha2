namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Tag trên TextBlock nhãn "Đời X" — dùng để hit-test và chỉnh style.</summary>
    public sealed class PhaDoGenLabelTag
    {
        public int Level { get; }
        public PhaDoGenLabelTag(int level) => Level = level;
    }

    /// <summary>Style nhãn "Đời X" — font, màu, đậm/thường.</summary>
    public sealed class PhaDoGenLabelStyle
    {
        public double FontPt { get; set; } = 0;          // 0 = dùng mặc định theo card layout
        public string FontFamily { get; set; }            // null = dùng options.FontFamilyName
        public string ForegroundHex { get; set; }        // null = màu mặc định theo layout mode
        public bool Bold { get; set; } = false;
        public bool Italic { get; set; } = false;

        public PhaDoGenLabelStyle Clone() => new PhaDoGenLabelStyle
        {
            FontPt = FontPt, FontFamily = FontFamily,
            ForegroundHex = ForegroundHex, Bold = Bold, Italic = Italic
        };
    }
}
