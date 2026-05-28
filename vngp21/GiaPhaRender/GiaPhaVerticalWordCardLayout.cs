using System;
using System.Collections.Generic;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>
    /// Thẻ dọc kiểu Word: mỗi người một cột; trong cột mỗi từ một dòng ngang xếp dọc.
    /// Ví dụ "Lương Trọng Nghĩa" → Lương / Trọng / Nghĩa (chữ nằm ngang từng dòng).
    /// </summary>
    internal static class GiaPhaVerticalWordCardLayout
    {
        public static List<GiaPhaVerticalTextColumn> BuildColumns(
            FamilyCardMetrics metrics,
            GiaPhaRenderOptions options,
            FamilyViewModel familyFallback)
        {
            var columns = new List<GiaPhaVerticalTextColumn>();

            if (metrics.MainPerson != null)
            {
                AddPersonWordStack(columns,
                    FamilyCardMetrics.FormatMainPublic(metrics.MainPerson),
                    options.MainNameFontPt, true, options);
            }
            else if (familyFallback != null)
            {
                string label = familyFallback.Name0 ?? familyFallback.Name ?? "Gia đình";
                if (!string.IsNullOrWhiteSpace(label))
                {
                    AddPersonWordStack(columns, label, options.MainNameFontPt, true, options);
                }
            }

            if (metrics.Spouses != null)
            {
                foreach (var s in metrics.Spouses)
                {
                    AddPersonWordStack(columns,
                        FamilyCardMetrics.FormatSpousePublic(s),
                        options.SpouseFontPt, false, options);
                }
            }

            if (metrics.SpouseOverflow != null)
            {
                foreach (var line in metrics.SpouseOverflow)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        AddPersonWordStack(columns, line, options.SpouseFontPt, false, options);
                    }
                }
            }

            return columns;
        }

        private static void AddPersonWordStack(
            List<GiaPhaVerticalTextColumn> columns,
            string text,
            double fontPt,
            bool bold,
            GiaPhaRenderOptions options)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string[] words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return;
            }

            double widthMm = 0;
            double lineH = WordLineHeightMm(fontPt, options);
            foreach (string word in words)
            {
                widthMm = Math.Max(widthMm, HorizontalWordWidthMm(word, fontPt));
            }

            columns.Add(new GiaPhaVerticalTextColumn
            {
                HorizontalWordLines = words,
                FontPt = fontPt,
                Bold = bold,
                WidthMm = Math.Max(GiaPhaVerticalCardLayout.ColumnWidthMm(fontPt, options), widthMm + 1.0),
                HeightMm = words.Length * lineH
            });
        }

        public static double HorizontalWordWidthMm(string word, double fontPt)
        {
            if (string.IsNullOrEmpty(word))
            {
                return 4;
            }

            return word.Length * fontPt * 0.52 + 1.5;
        }

        public static double WordLineHeightMm(double fontPt, GiaPhaRenderOptions options)
        {
            return Math.Max(options.CardLineHeightMm, fontPt * 25.4 / 72.0 * 1.12);
        }
    }
}
