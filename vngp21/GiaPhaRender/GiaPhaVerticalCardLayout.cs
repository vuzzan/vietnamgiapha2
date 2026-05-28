using System;
using System.Collections.Generic;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Cột trong thẻ dọc — dùng chung metrics / canvas / SVG.</summary>
    internal sealed class GiaPhaVerticalTextColumn
    {
        /// <summary>Chữ từng ký tự dọc (kiểu Dọc).</summary>
        public string Text { get; set; }

        /// <summary>Kiểu Word: mỗi từ một dòng ngang, xếp chồng trong cột một người.</summary>
        public string[] HorizontalWordLines { get; set; }

        public bool IsWordStack => HorizontalWordLines != null && HorizontalWordLines.Length > 0;

        public double FontPt { get; set; }
        public bool Bold { get; set; }
        public double WidthMm { get; set; }
        public double HeightMm { get; set; }
    }

    internal static class GiaPhaVerticalCardLayout
    {
        public const double ColumnGapMm = 1.0;

        public static List<GiaPhaVerticalTextColumn> BuildColumns(
            FamilyCardMetrics metrics,
            GiaPhaRenderOptions options,
            FamilyViewModel familyFallback)
        {
            var columns = new List<GiaPhaVerticalTextColumn>();

            if (metrics.MainPerson != null)
            {
                columns.Add(MakeColumn(
                    FamilyCardMetrics.FormatMainPublic(metrics.MainPerson),
                    options.MainNameFontPt, true, options));
            }
            else if (familyFallback != null)
            {
                string label = familyFallback.Name0 ?? familyFallback.Name ?? "Gia đình";
                if (!string.IsNullOrWhiteSpace(label))
                {
                    columns.Add(MakeColumn(label, options.MainNameFontPt, true, options));
                }
            }

            if (metrics.Spouses != null)
            {
                foreach (var s in metrics.Spouses)
                {
                    columns.Add(MakeColumn(
                        FamilyCardMetrics.FormatSpousePublic(s),
                        options.SpouseFontPt, false, options));
                }
            }

            if (metrics.SpouseOverflow != null)
            {
                foreach (var line in metrics.SpouseOverflow)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        columns.Add(MakeColumn(line, options.SpouseFontPt, false, options));
                    }
                }
            }

            return columns;
        }

        public static void MeasureCardSize(
            string headerLabel,
            List<GiaPhaVerticalTextColumn> columns,
            GiaPhaRenderOptions options,
            out double widthMm,
            out double heightMm)
        {
            double pad = options.CardPaddingMm;
            widthMm = pad * 2;
            double maxColH = options.CardLineHeightMm;

            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                widthMm += ColumnGapMm;
                widthMm += col.WidthMm;
                maxColH = Math.Max(maxColH, col.HeightMm);
            }

            if (columns.Count == 0 && string.IsNullOrWhiteSpace(headerLabel))
            {
                widthMm = options.CardVerticalMinWidthMm;
                maxColH = options.CardLineHeightMm;
            }

            widthMm = Math.Max(options.CardVerticalMinWidthMm, widthMm);
            widthMm = Math.Min(widthMm, options.CardVerticalMaxWidthMm * 2.5);

            // Chiều cao chỉ từ cột tên dọc — không cộng thêm vùng header "Đời" trên thẻ
            heightMm = pad * 2 + maxColH + options.CardBottomPaddingMm;
        }

        private static GiaPhaVerticalTextColumn MakeColumn(
            string text,
            double fontPt,
            bool bold,
            GiaPhaRenderOptions options)
        {
            return new GiaPhaVerticalTextColumn
            {
                Text = text ?? "",
                FontPt = fontPt,
                Bold = bold,
                WidthMm = ColumnWidthMm(fontPt, options),
                HeightMm = VerticalRunHeightMm(text, fontPt, options)
            };
        }

        public static double ColumnWidthMm(double fontPt, GiaPhaRenderOptions options)
        {
            return Math.Max(options.CardLineHeightMm, FontThicknessMm(fontPt)) + 0.6;
        }

        /// <summary>Độ dày cột chữ (mm) sau khi xoay — ≈ chiều cao font.</summary>
        public static double FontThicknessMm(double fontPt)
        {
            return fontPt * 25.4 / 72.0;
        }

        /// <summary>Neo góc trên-phải cột: chữ xoay -90° chạy xuống trong ô.</summary>
        public static void GetColumnAnchorMm(
            double columnLeftMm,
            double columnTopMm,
            double columnWidthMm,
            out double anchorXMm,
            out double anchorYMm)
        {
            anchorXMm = columnLeftMm + columnWidthMm;
            anchorYMm = columnTopMm;
        }

        public static double VerticalRunHeightMm(string text, double fontPt, GiaPhaRenderOptions options)
        {
            if (string.IsNullOrEmpty(text))
            {
                return options.CardLineHeightMm;
            }

            return text.Length * CharStepMm(fontPt);
        }

        public static double CharStepMm(double fontPt)
        {
            return fontPt * 0.48;
        }

        public static double ColumnCenterMm(double columnLeftMm, double columnWidthMm)
        {
            return columnLeftMm + columnWidthMm / 2.0;
        }
    }
}
