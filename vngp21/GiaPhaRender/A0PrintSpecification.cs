namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Kích thước giấy ISO A0 (mm).</summary>
    public static class A0PrintSpecification
    {
        public const double WidthMm = 841.0;
        public const double HeightMm = 1189.0;

        /// <summary>A0 ngang: rộng × cao (mm) — phù hợp phả hệ trải ngang.</summary>
        public const double LandscapeWidthMm = HeightMm;
        public const double LandscapeHeightMm = WidthMm;

        public const double DefaultMarginMm = 20.0;
        public const double DefaultPrintDpi = 150.0;
        public const double MaxPrintDpi = 200.0;
    }
}
