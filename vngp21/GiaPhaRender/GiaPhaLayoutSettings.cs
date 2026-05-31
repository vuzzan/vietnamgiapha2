using System;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Tham số layout phả đồ do người dùng chỉnh — lưu/load qua JSON; dialog hiển thị theo cm.</summary>
    public sealed class GiaPhaLayoutSettings
    {
        public double MarginCm { get; set; } = 1.5;
        public double HorizontalGapCm { get; set; } = 1.0;
        public double GenerationGapCm { get; set; } = 3.2;
        public double BusLineGapCm { get; set; } = 1.6;
        public double MinBusSpanCm { get; set; } = 1.4;

        public double CardMinWidthCm { get; set; } = 2.6;
        public double CardMaxWidthCm { get; set; } = 7.2;
        public double CardWidthTextFactor { get; set; } = 0.58;
        public double CardVerticalMinWidthCm { get; set; } = 2.0;
        public double CardVerticalMaxWidthCm { get; set; } = 3.6;
        public double CardPaddingCm { get; set; } = 0.25;
        public double CardLineHeightCm { get; set; } = 0.5;
        public double CardHeaderHeightCm { get; set; } = 0.6;
        public double CardBottomPaddingCm { get; set; } = 0.2;
        public double CardHeightSafetyFactor { get; set; } = 1.32;

        public int MaxSpouseLinesShown { get; set; } = 4;

        public double HeaderFontPt { get; set; } = 7;
        public double MainNameFontPt { get; set; } = 9;
        public double SpouseFontPt { get; set; } = 7.5;
        public double VerticalGenerationLabelFontPt { get; set; } = 10;
        public int ConnectorPathType { get; set; } = (int)GiaPhaConnectorPathType.Orthogonal;

        public static GiaPhaLayoutSettings CreateDefault()
        {
            return FromOptions(GiaPhaRenderOptions.ForFitContent(96));
        }

        public static GiaPhaLayoutSettings FromOptions(GiaPhaRenderOptions o)
        {
            if (o == null)
            {
                return CreateDefault();
            }

            return new GiaPhaLayoutSettings
            {
                MarginCm = MmToCm(o.MarginMm),
                HorizontalGapCm = MmToCm(o.HorizontalGapMm),
                GenerationGapCm = MmToCm(o.GenerationGapMm),
                BusLineGapCm = MmToCm(o.BusLineGapMm),
                MinBusSpanCm = MmToCm(o.MinBusSpanMm),
                CardMinWidthCm = MmToCm(o.CardMinWidthMm),
                CardMaxWidthCm = MmToCm(o.CardMaxWidthMm),
                CardWidthTextFactor = o.CardWidthTextFactor,
                CardVerticalMinWidthCm = MmToCm(o.CardVerticalMinWidthMm),
                CardVerticalMaxWidthCm = MmToCm(o.CardVerticalMaxWidthMm),
                CardPaddingCm = MmToCm(o.CardPaddingMm),
                CardLineHeightCm = MmToCm(o.CardLineHeightMm),
                CardHeaderHeightCm = MmToCm(o.CardHeaderHeightMm),
                CardBottomPaddingCm = MmToCm(o.CardBottomPaddingMm),
                CardHeightSafetyFactor = o.CardHeightSafetyFactor,
                MaxSpouseLinesShown = o.MaxSpouseLinesShown,
                HeaderFontPt = o.HeaderFontPt,
                MainNameFontPt = o.MainNameFontPt,
                SpouseFontPt = o.SpouseFontPt,
                VerticalGenerationLabelFontPt = o.VerticalGenerationLabelFontPt,
                ConnectorPathType = (int)o.ConnectorPathType
            };
        }

        public void ApplyTo(GiaPhaRenderOptions options)
        {
            if (options == null)
            {
                return;
            }

            options.MarginMm = CmToMm(MarginCm);
            options.HorizontalGapMm = CmToMm(HorizontalGapCm);
            options.GenerationGapMm = CmToMm(GenerationGapCm);
            options.BusLineGapMm = CmToMm(BusLineGapCm);
            options.MinBusSpanMm = CmToMm(MinBusSpanCm);
            options.CardMinWidthMm = CmToMm(CardMinWidthCm);
            options.CardMaxWidthMm = CmToMm(CardMaxWidthCm);
            options.CardWidthTextFactor = CardWidthTextFactor;
            options.CardVerticalMinWidthMm = CmToMm(CardVerticalMinWidthCm);
            options.CardVerticalMaxWidthMm = CmToMm(CardVerticalMaxWidthCm);
            options.CardPaddingMm = CmToMm(CardPaddingCm);
            options.CardLineHeightMm = CmToMm(CardLineHeightCm);
            options.CardHeaderHeightMm = CmToMm(CardHeaderHeightCm);
            options.CardBottomPaddingMm = CmToMm(CardBottomPaddingCm);
            options.CardHeightSafetyFactor = CardHeightSafetyFactor;
            options.MaxSpouseLinesShown = MaxSpouseLinesShown;
            options.HeaderFontPt = HeaderFontPt;
            options.MainNameFontPt = MainNameFontPt;
            options.SpouseFontPt = SpouseFontPt;
            options.VerticalGenerationLabelFontPt = VerticalGenerationLabelFontPt;
            options.ConnectorPathType = Enum.IsDefined(typeof(GiaPhaConnectorPathType), ConnectorPathType)
                ? (GiaPhaConnectorPathType)ConnectorPathType
                : GiaPhaConnectorPathType.Orthogonal;
        }

        public GiaPhaLayoutSettings Clone()
        {
            return (GiaPhaLayoutSettings)MemberwiseClone();
        }

        public static double MmToCm(double mm) => mm / 10.0;

        public static double CmToMm(double cm) => cm * 10.0;
    }
}
