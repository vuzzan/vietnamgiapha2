using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Kích thước khối tiêu đề theo nội dung 4 dòng chữ.</summary>
    public sealed class PhaDoTitleBlockLayout
    {
        public double LeftMm { get; set; }
        public double TopMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double TextLeftMm { get; set; }
        public double Line1TopMm { get; set; }
        public double Line2TopMm { get; set; }
        public double Line3TopMm { get; set; }
        public double Line4TopMm { get; set; }
    }

    public static class PhaDoTitleBlockMetrics
    {
        private const double PadHorizontalMm = 4;
        private const double PadVerticalMm = 3;
        private const double MinWidthMm = 20;
        private const double LineGapMm = 0.8;

        public static PhaDoTitleBlockLayout Measure(GiaPhaRenderOptions options, double dpi)
        {
            if (options == null)
                return new PhaDoTitleBlockLayout();

            bool has1 = !string.IsNullOrWhiteSpace(options.Title);
            bool has2 = !string.IsNullOrWhiteSpace(options.TitleLine2);
            bool has3 = !string.IsNullOrWhiteSpace(options.TitleLine3);
            bool has4 = !string.IsNullOrWhiteSpace(options.TitleLine4);
            if (!has1 && !has2 && !has3 && !has4)
                return new PhaDoTitleBlockLayout();

            if (dpi < 1) dpi = 96;

            // Dòng 1–2: font lớn (tên + ở tại); dòng 3–4: font nhỏ (thống kê)
            double smallFontPt = Math.Max(7, (options.TitleLine2FontPt > 0 ? options.TitleLine2FontPt : 12) * 0.78);

            double l1W = 0, l1H = 0, l2W = 0, l2H = 0, l3W = 0, l3H = 0, l4W = 0, l4H = 0;

            if (has1) MeasureLine(options.Title, options.TitleLine1FontFamily ?? options.FontFamilyName, options.TitleFontPt, FontWeights.Bold, dpi, out l1W, out l1H);
            if (has2) MeasureLine(options.TitleLine2, options.TitleLine2FontFamily ?? options.FontFamilyName, options.TitleLine2FontPt, FontWeights.Normal, dpi, out l2W, out l2H);
            if (has3) MeasureLine(options.TitleLine3, options.FontFamilyName, smallFontPt, FontWeights.Normal, dpi, out l3W, out l3H);
            if (has4) MeasureLine(options.TitleLine4, options.FontFamilyName, smallFontPt, FontWeights.Normal, dpi, out l4W, out l4H);

            double textWmm = Math.Max(Math.Max(l1W, l2W), Math.Max(l3W, l4W));
            double widthMm = Math.Max(MinWidthMm, textWmm + PadHorizontalMm * 2);

            double maxContentW = options.PageWidthMm > 0 ? options.PageWidthMm - options.MarginMm * 2 : options.ContentWidthMm;
            if (maxContentW > 0 && widthMm > maxContentW) widthMm = maxContentW;

            // Tích lũy chiều cao — thêm gap nhỏ giữa các dòng
            double innerHmm = 0;
            double line1TopMm, line2TopMm, line3TopMm, line4TopMm;

            double topMm = options.ManualTitlePositionSet ? options.ManualTitleTopMm : options.MarginMm;
            double leftMm = options.ManualTitlePositionSet ? options.ManualTitleLeftMm : options.MarginMm;

            line1TopMm = topMm + PadVerticalMm;
            if (has1) innerHmm += l1H;

            line2TopMm = line1TopMm + (has1 ? l1H + LineGapMm : 0);
            if (has2) innerHmm += (has1 ? LineGapMm : 0) + l2H;

            // Gap nhỏ hơn trước dòng 3 (phân cách nhẹ phần auto)
            double gapBefore3 = (has1 || has2) ? LineGapMm * 0.7 : 0;
            line3TopMm = line2TopMm + (has2 ? l2H + gapBefore3 : 0) + (!has2 && (has1 || has2) ? gapBefore3 : 0);
            if (has3) innerHmm += gapBefore3 + l3H;

            line4TopMm = line3TopMm + (has3 ? l3H + LineGapMm * 0.5 : 0);
            if (has4) innerHmm += (has3 ? LineGapMm * 0.5 : 0) + l4H;

            // Cho phép override kích thước thủ công (kéo resize)
            double heightMm = options.ManualTitleHeightMm > 0
                ? options.ManualTitleHeightMm
                : innerHmm + PadVerticalMm * 2;
            if (options.ManualTitleWidthMm > 0) widthMm = options.ManualTitleWidthMm;

            return new PhaDoTitleBlockLayout
            {
                LeftMm = leftMm,
                TopMm = topMm,
                WidthMm = widthMm,
                HeightMm = heightMm,
                TextLeftMm = leftMm + PadHorizontalMm,
                Line1TopMm = line1TopMm,
                Line2TopMm = line2TopMm,
                Line3TopMm = line3TopMm,
                Line4TopMm = line4TopMm
            };
        }

        private static void MeasureLine(
            string text,
            string fontFamily,
            double fontPt,
            FontWeight weight,
            double dpi,
            out double widthMm,
            out double heightMm)
        {
            widthMm = 0;
            heightMm = 0;
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            double fontPx = fontPt * dpi / 72.0;
            var typeface = new Typeface(
                new FontFamily(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily),
                FontStyles.Normal,
                weight,
                FontStretches.Normal);

            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                fontPx,
                Brushes.Black,
                dpi);

            widthMm = PrintUnits.PixelsToMm(formatted.Width, dpi);
            heightMm = PrintUnits.PixelsToMm(formatted.Height, dpi);
        }
    }
}
