using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Kích thước khối tiêu đề theo nội dung 2 dòng chữ (không full width trang).</summary>
    public sealed class PhaDoTitleBlockLayout
    {
        public double LeftMm { get; set; }
        public double TopMm { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
        public double TextLeftMm { get; set; }
        public double Line1TopMm { get; set; }
        public double Line2TopMm { get; set; }
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
            {
                return new PhaDoTitleBlockLayout();
            }

            bool has1 = !string.IsNullOrWhiteSpace(options.Title);
            bool has2 = !string.IsNullOrWhiteSpace(options.TitleLine2);
            if (!has1 && !has2)
            {
                return new PhaDoTitleBlockLayout();
            }

            if (dpi < 1)
            {
                dpi = 96;
            }

            double line1W = 0;
            double line1H = 0;
            double line2W = 0;
            double line2H = 0;

            if (has1)
            {
                MeasureLine(
                    options.Title,
                    options.TitleLine1FontFamily ?? options.FontFamilyName,
                    options.TitleFontPt,
                    FontWeights.Bold,
                    dpi,
                    out line1W,
                    out line1H);
            }

            if (has2)
            {
                MeasureLine(
                    options.TitleLine2,
                    options.TitleLine2FontFamily ?? options.FontFamilyName,
                    options.TitleLine2FontPt,
                    FontWeights.Normal,
                    dpi,
                    out line2W,
                    out line2H);
            }

            double textWmm = Math.Max(line1W, line2W);
            double widthMm = Math.Max(MinWidthMm, textWmm + PadHorizontalMm * 2);

            double maxContentW = options.PageWidthMm > 0
                ? options.PageWidthMm - options.MarginMm * 2
                : options.ContentWidthMm;
            if (maxContentW > 0 && widthMm > maxContentW)
            {
                widthMm = maxContentW;
            }

            double innerHmm = 0;
            if (has1)
            {
                innerHmm += line1H;
            }

            if (has2)
            {
                if (has1)
                {
                    innerHmm += LineGapMm;
                }

                innerHmm += line2H;
            }

            double heightMm = innerHmm + PadVerticalMm * 2;
            double topMm = options.MarginMm;
            double leftMm = options.MarginMm;
            double textLeftMm = leftMm + PadHorizontalMm;
            double line1TopMm = topMm + PadVerticalMm;
            double line2TopMm = has2
                ? line1TopMm + (has1 ? line1H + LineGapMm : 0)
                : line1TopMm;

            return new PhaDoTitleBlockLayout
            {
                LeftMm = leftMm,
                TopMm = topMm,
                WidthMm = widthMm,
                HeightMm = heightMm,
                TextLeftMm = textLeftMm,
                Line1TopMm = line1TopMm,
                Line2TopMm = line2TopMm
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
