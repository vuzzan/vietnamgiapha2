using System;
using System.Collections.Generic;
using System.Linq;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Đo kích thước thẻ một gia đình (chủ + phối). Không dùng WPF — chạy được trên background thread.</summary>
    public sealed class FamilyCardMetrics
    {
        public double WidthMm { get; private set; }
        public double HeightMm { get; private set; }
        /// <summary>Chiều cao hàng đời khi xếp layout (≥ HeightMm).</summary>
        public double SlotHeightMm { get; private set; }
        public PersonInfo MainPerson { get; private set; }
        public IReadOnlyList<PersonInfo> Spouses { get; private set; }
        public IReadOnlyList<string> SpouseOverflow { get; private set; }
        public int Generation { get; private set; }
        public string FamilyLabel { get; private set; }
        /// <summary>Ghi chú cuối ô: khởi đầu phả con, kích thước cm…</summary>
        public IReadOnlyList<string> ExtraNotes { get; private set; } = Array.Empty<string>();

        public void ApplySizeOverride(double? widthMm, double? heightMm)
        {
            if (widthMm.HasValue && widthMm.Value > 0)
            {
                WidthMm = widthMm.Value;
            }

            if (heightMm.HasValue && heightMm.Value > 0)
            {
                HeightMm = heightMm.Value;
                SlotHeightMm = Math.Max(SlotHeightMm, HeightMm);
            }
        }

        public static FamilyCardMetrics Measure(
            FamilyViewModel family,
            GiaPhaRenderOptions options,
            double dpi)
        {
            if (GiaPhaRenderOptions.IsVerticalCardLayout(options.CardLayoutMode))
            {
                return MeasureVertical(family, options, dpi);
            }

            return MeasureHorizontal(family, options, dpi);
        }

        private static FamilyCardMetrics MeasureHorizontal(
            FamilyViewModel family,
            GiaPhaRenderOptions options,
            double dpi)
        {
            var metrics = new FamilyCardMetrics();
            metrics.Generation = family.familyInfo.FamilyLevel;
            
            metrics.FamilyLabel = "Đời " + metrics.Generation;// Vẽ box gia đình

            var persons = family.ListPerson?.ToList() ?? new List<PersonInfo>();
            metrics.MainPerson = persons.FirstOrDefault(p => p.IsMainPerson == 1)
                ?? persons.FirstOrDefault();

            var spouses = persons.Where(p => p != metrics.MainPerson).ToList();
            int maxLines = options.MaxSpouseLinesShown;
            metrics.Spouses = spouses.Take(maxLines).ToList();
            if (spouses.Count > maxLines)
            {
                metrics.SpouseOverflow = new List<string>
                {
                    "+" + (spouses.Count - maxLines) + " người"
                };
            }
            else
            {
                metrics.SpouseOverflow = new List<string>();
            }

            metrics.ExtraNotes = ResolveExtraNotes(family, options);

            double innerMaxMm = options.CardMaxWidthMm - options.CardPaddingMm * 2;
            double maxTextWidthMm = options.CardMinWidthMm;
            double totalTextHeightMm = 0;

            if (metrics.MainPerson != null)
            {
                string t = FormatMain(metrics.MainPerson);
                maxTextWidthMm = Math.Max(maxTextWidthMm, EstimateTextWidthMm(t, options.MainNameFontPt));
                totalTextHeightMm += EstimateWrappedHeightMm(t, options.MainNameFontPt, innerMaxMm, options);
            }

            foreach (var s in metrics.Spouses)
            {
                string t = FormatSpouse(s);
                maxTextWidthMm = Math.Max(maxTextWidthMm, EstimateTextWidthMm(t, options.SpouseFontPt));
                totalTextHeightMm += EstimateWrappedHeightMm(t, options.SpouseFontPt, innerMaxMm, options);
            }

            foreach (var line in metrics.SpouseOverflow)
            {
                maxTextWidthMm = Math.Max(maxTextWidthMm, EstimateTextWidthMm(line, options.SpouseFontPt));
                totalTextHeightMm += EstimateWrappedHeightMm(line, options.SpouseFontPt, innerMaxMm, options);
            }

            if (metrics.MainPerson == null && family != null)
            {
                string fallback = family.Name0 ?? family.Name ?? "Gia đình";
                if (fallback.Length > 0)
                {
                    maxTextWidthMm = Math.Max(maxTextWidthMm, EstimateTextWidthMm(fallback, options.MainNameFontPt));
                    totalTextHeightMm += EstimateWrappedHeightMm(fallback, options.MainNameFontPt, innerMaxMm, options);
                }
            }

            if (totalTextHeightMm < options.CardLineHeightMm)
            {
                totalTextHeightMm = options.CardLineHeightMm;
            }

            double widthFactor = options.CardWidthTextFactor > 0 ? options.CardWidthTextFactor : 1.0;
            double contentWidthMm = maxTextWidthMm * widthFactor + options.CardPaddingMm * 2;
            metrics.WidthMm = Math.Min(options.CardMaxWidthMm,
                Math.Max(options.CardMinWidthMm, contentWidthMm));

            metrics.HeightMm = options.CardHeaderHeightMm
                + options.CardPaddingMm * 2
                + totalTextHeightMm
                + options.CardBottomPaddingMm;

            ApplyExtraNotesToHorizontalSize(metrics, options, innerMaxMm);

            metrics.SlotHeightMm = metrics.HeightMm * options.CardHeightSafetyFactor;

            return metrics;
        }

        /// <summary>Thẻ hẹp, mỗi người một cột chữ dọc (tiết kiệm width cây).</summary>
        private static FamilyCardMetrics MeasureVertical(
            FamilyViewModel family,
            GiaPhaRenderOptions options,
            double dpi)
        {
            var metrics = new FamilyCardMetrics();
            metrics.Generation = family.familyInfo.FamilyLevel;
            metrics.FamilyLabel = "2Đời " + metrics.Generation;

            var persons = family.ListPerson?.ToList() ?? new List<PersonInfo>();
            metrics.MainPerson = persons.FirstOrDefault(p => p.IsMainPerson == 1)
                ?? persons.FirstOrDefault();

            var spouses = persons.Where(p => p != metrics.MainPerson).ToList();
            int maxLines = options.MaxSpouseLinesShown;
            metrics.Spouses = spouses.Take(maxLines).ToList();
            if (spouses.Count > maxLines)
            {
                metrics.SpouseOverflow = new List<string>
                {
                    "+" + (spouses.Count - maxLines) + " người"
                };
            }
            else
            {
                metrics.SpouseOverflow = new List<string>();
            }

            var columns = options.CardLayoutMode == GiaPhaCardLayoutMode.VerticalWord
                ? GiaPhaVerticalWordCardLayout.BuildColumns(metrics, options, family)
                : GiaPhaVerticalCardLayout.BuildColumns(metrics, options, family);
            GiaPhaVerticalCardLayout.MeasureCardSize(metrics.FamilyLabel, columns, options, out var w, out var h);
            if (!string.IsNullOrWhiteSpace(metrics.FamilyLabel)
                && options.CardLayoutMode == GiaPhaCardLayoutMode.Vertical)
            {
                double doiFontPt = options.VerticalGenerationLabelFontPt > 0
                    ? options.VerticalGenerationLabelFontPt
                    : options.HeaderFontPt;
                w += GiaPhaVerticalCardLayout.ColumnWidthMm(doiFontPt, options)
                    + GiaPhaVerticalCardLayout.ColumnGapMm;
            }

            metrics.ExtraNotes = ResolveExtraNotes(family, options);

            metrics.WidthMm = w;
            metrics.HeightMm = h;
            ApplyExtraNotesToVerticalSize(metrics, options);
            metrics.SlotHeightMm = metrics.HeightMm * options.CardHeightSafetyFactor;
            return metrics;
        }

        internal static string FormatMainPublic(PersonInfo p) => FormatMain(p);

        internal static string FormatSpousePublic(PersonInfo p) => FormatSpouse(p);

        internal static double EstimateWrappedLineHeightMm(
            string text,
            double fontPt,
            double innerMaxMm,
            GiaPhaRenderOptions options) =>
            EstimateWrappedHeightMm(text, fontPt, innerMaxMm, options);

        private static string FormatMain(PersonInfo p)
        {
            return p.MANS_NAME_HUY ?? "";
        }

        private static IReadOnlyList<string> ResolveExtraNotes(FamilyViewModel family, GiaPhaRenderOptions options)
        {
            if (options?.GetFamilyBoxNotes == null || family?.familyInfo == null)
            {
                return Array.Empty<string>();
            }

            int familyId = family.familyInfo.FamilyId;
            if (familyId <= 0)
            {
                return Array.Empty<string>();
            }

            var notes = options.GetFamilyBoxNotes(familyId);
            if (notes == null || notes.Count == 0)
            {
                return Array.Empty<string>();
            }

            return notes.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }

        private static double NoteFontPt(GiaPhaRenderOptions options)
        {
            return options.NoteFontPt > 0 ? options.NoteFontPt : 6.5;
        }

        private static void ApplyExtraNotesToHorizontalSize(
            FamilyCardMetrics metrics,
            GiaPhaRenderOptions options,
            double innerMaxMm)
        {
            if (metrics.ExtraNotes == null || metrics.ExtraNotes.Count == 0)
            {
                return;
            }

            double notePt = NoteFontPt(options);
            double notesBlockMm = options.CardNoteTopGapMm;
            foreach (var line in metrics.ExtraNotes)
            {
                metrics.WidthMm = Math.Max(metrics.WidthMm,
                    EstimateTextWidthMm(line, notePt) * (options.CardWidthTextFactor > 0 ? options.CardWidthTextFactor : 1.0)
                    + options.CardPaddingMm * 2);
                notesBlockMm += EstimateWrappedHeightMm(line, notePt, innerMaxMm, options);
            }

            metrics.HeightMm += notesBlockMm;
        }

        private static void ApplyExtraNotesToVerticalSize(FamilyCardMetrics metrics, GiaPhaRenderOptions options)
        {
            if (metrics.ExtraNotes == null || metrics.ExtraNotes.Count == 0)
            {
                return;
            }

            double innerMaxMm = Math.Max(8, metrics.WidthMm - options.CardPaddingMm * 2);
            double notePt = NoteFontPt(options);
            double notesBlockMm = options.CardNoteTopGapMm;
            foreach (var line in metrics.ExtraNotes)
            {
                double lineW = EstimateTextWidthMm(line, notePt) + options.CardPaddingMm * 2;
                metrics.WidthMm = Math.Max(metrics.WidthMm, Math.Min(options.CardVerticalMaxWidthMm * 2.5, lineW));
                notesBlockMm += EstimateWrappedHeightMm(line, notePt, innerMaxMm, options);
            }

            metrics.HeightMm += notesBlockMm;
        }

        private static string FormatSpouse(PersonInfo p)
        {
            string g = p.MANS_GENDER == "Nữ" ? "♀" : "♂";
            return g + " " + (p.MANS_NAME_HUY ?? "");
        }

        private static double MmPerChar(double fontPt)
        {
            return fontPt * 0.40;
        }

        private static double LineHeightMm(double fontPt, GiaPhaRenderOptions options)
        {
            return Math.Max(options.CardLineHeightMm, fontPt * 0.58);
        }

        private static double EstimateTextWidthMm(string text, double fontPt)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            return text.Length * MmPerChar(fontPt);
        }

        /// <summary>Ước lượng chiều cao khi tên dài bị xuống dòng trong CardMaxWidth.</summary>
        private static double EstimateWrappedHeightMm(
            string text,
            double fontPt,
            double innerMaxMm,
            GiaPhaRenderOptions options)
        {
            if (string.IsNullOrEmpty(text))
            {
                return options.CardLineHeightMm;
            }

            double lineH = LineHeightMm(fontPt, options);
            double textW = EstimateTextWidthMm(text, fontPt);
            if (textW <= innerMaxMm)
            {
                return lineH;
            }

            double mmPerChar = MmPerChar(fontPt);
            int charsPerLine = Math.Max(4, (int)(innerMaxMm / mmPerChar));
            int lines = (text.Length + charsPerLine - 1) / charsPerLine;
            return lines * lineH;
        }
    }
}
