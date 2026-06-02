using System;
using System.Globalization;
using System.Text;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Xuất phả đồ ra SVG vector (mm) — ghi file theo luồng, tránh OutOfMemory.</summary>
    public static class GiaPhaSvgExportService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Trên ngưỡng này: xuất rect đơn giản, không nhúng SVG khung từng ô.</summary>
        public const int SimpleBoxExportNodeThreshold = 1500;

        /// <summary>viewBox dùng mm — font-size phải cùng đơn vị (không dùng pt tuyệt đối).</summary>
        private static double PtToMm(double fontPt) => fontPt * 25.4 / 72.0;

        public static void Export(
            string filePath,
            GiaPhaRenderResult result,
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId = null,
            PhaDoTitleStyle titleStyle = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            var options = result.Options ?? GiaPhaRenderOptions.ForFitContentPrint();
            double wMm = result.PageWidthMm > 0 ? result.PageWidthMm : 200;
            double hMm = result.PageHeightMm > 0 ? result.PageHeightMm : 200;
            int nodeCount = result.Nodes?.Count ?? 0;
            bool simpleBoxes = nodeCount >= SimpleBoxExportNodeThreshold;

            using (var writer = new SvgExportWriter(filePath))
            {
                writer.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
                writer.Write("width=\"" + F(wMm) + "mm\" ");
                writer.Write("height=\"" + F(hMm) + "mm\" ");
                writer.WriteLine("viewBox=\"0 0 " + F(wMm) + " " + F(hMm) + "\">");

                writer.WriteLine("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

                DrawTitle(writer, options, titleStyle);
                DrawGenerationBands(writer, result, options, wMm);
                DrawCards(writer, result, options, boxStyleForFamilyId, simpleBoxes);
                DrawConnectors(writer, result, options);

                writer.WriteLine("</svg>");
            }
        }

        private static void DrawTitle(SvgExportWriter writer, GiaPhaRenderOptions options, PhaDoTitleStyle titleStyle)
        {
            bool has1 = !string.IsNullOrWhiteSpace(options.Title);
            bool has2 = !string.IsNullOrWhiteSpace(options.TitleLine2);
            bool has3 = !string.IsNullOrWhiteSpace(options.TitleLine3);
            bool has4 = !string.IsNullOrWhiteSpace(options.TitleLine4);
            if (!has1 && !has2 && !has3 && !has4)
            {
                return;
            }

            double dpi = options.PrintDpi > 0 ? options.PrintDpi : 150;
            var layout = PhaDoTitleBlockMetrics.Measure(options, dpi);

            var titleFrame = new PhaDoTitleStyle
            {
                FillColorHex = options.TitleFillColorHex,
                CustomShapeSvg = options.TitleCustomShapeSvg,
                CustomShapeViewBoxWidth = options.TitleCustomShapeViewBoxWidth,
                CustomShapeViewBoxHeight = options.TitleCustomShapeViewBoxHeight
            };
            PhaDoBoxSvgVectorExport.DrawFrameBackground(
                writer,
                layout.LeftMm,
                layout.TopMm,
                layout.WidthMm,
                layout.HeightMm,
                familyId: -1,
                titleFrame);

            double defaultSmallPt = Math.Max(7, (options.TitleLine2FontPt > 0 ? options.TitleLine2FontPt : 12) * 0.78);

            if (has1)
            {
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 0);
                AppendTitleLine(writer, options.Title, layout.TextLeftMm + off.DeltaXmm, layout.Line1TopMm + off.DeltaYmm,
                    options.TitleLine1FontFamily ?? options.FontFamilyName,
                    options.TitleFontPt, options.TitleLine1ForegroundHex, bold: true);
            }

            if (has2)
            {
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 1);
                AppendTitleLine(writer, options.TitleLine2, layout.TextLeftMm + off.DeltaXmm, layout.Line2TopMm + off.DeltaYmm,
                    options.TitleLine2FontFamily ?? options.FontFamilyName,
                    options.TitleLine2FontPt, options.TitleLine2ForegroundHex, bold: false);
            }

            if (has3)
            {
                double l3Pt = options.TitleLine3FontPt > 0 ? options.TitleLine3FontPt : defaultSmallPt;
                string l3Fore = !string.IsNullOrWhiteSpace(options.TitleLine3ForegroundHex)
                    ? options.TitleLine3ForegroundHex : "#888888";
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 2);
                AppendTitleLine(writer, options.TitleLine3, layout.TextLeftMm + off.DeltaXmm, layout.Line3TopMm + off.DeltaYmm,
                    options.TitleLine3FontFamily ?? options.FontFamilyName,
                    l3Pt, l3Fore, bold: false);
            }

            if (has4)
            {
                double l4Pt = options.TitleLine4FontPt > 0 ? options.TitleLine4FontPt : defaultSmallPt;
                string l4Fore = !string.IsNullOrWhiteSpace(options.TitleLine4ForegroundHex)
                    ? options.TitleLine4ForegroundHex : "#888888";
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 3);
                AppendTitleLine(writer, options.TitleLine4, layout.TextLeftMm + off.DeltaXmm, layout.Line4TopMm + off.DeltaYmm,
                    options.TitleLine4FontFamily ?? options.FontFamilyName,
                    l4Pt, l4Fore, bold: false);
            }
        }

        private static void AppendTitleLine(
            SvgExportWriter writer,
            string text,
            double xMm,
            double topMm,
            string fontFamily,
            double fontPt,
            string foregroundHex,
            bool bold)
        {
            double fontMm = PtToMm(fontPt);
            double baselineY = topMm + fontMm * 0.85;
            string fill = NormalizeHex(foregroundHex, "#000000");

            var line = new StringBuilder(128);
            line.Append("<text x=\"").Append(F(xMm)).Append("\" y=\"").Append(F(baselineY))
                .Append("\" font-family=\"").Append(EscAttr(fontFamily ?? "Segoe UI"))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm));
            if (bold)
            {
                line.Append("\" font-weight=\"bold");
            }

            line.Append("\" fill=\"").Append(EscAttr(fill)).Append("\">")
                .Append(EscText(text))
                .Append("</text>");
            writer.WriteLine(line.ToString());
        }

        private static void DrawGenerationBands(
            SvgExportWriter writer,
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options,
            double pageWmm)
        {
            for (int i = 0; i < result.GenerationBands.Count; i++)
            {
                var band = result.GenerationBands[i];
                string fill = i % 2 == 0 ? "#F8FAFC" : "#EBF1F5";
                writer.WriteLine("<rect x=\"0\" y=\"" + F(band.Ymm) + "\" width=\""
                    + F(pageWmm) + "\" height=\"" + F(band.HeightMm) + "\" fill=\"" + fill + "\"/>");

                bool vertical = GiaPhaRenderOptions.IsVerticalCardLayout(options.CardLayoutMode);
                var genStyle = options.GenLabelStyle;
                double bandLabelPt = genStyle?.FontPt > 0
                    ? genStyle.FontPt
                    : (vertical ? options.VerticalGenerationLabelFontPt : options.HeaderFontPt);
                string genFont = !string.IsNullOrWhiteSpace(genStyle?.FontFamily)
                    ? genStyle.FontFamily
                    : options.FontFamilyName;
                bool genBold = genStyle != null
                    ? genStyle.Bold
                    : vertical;
                string genFill = NormalizeHex(
                    !string.IsNullOrWhiteSpace(genStyle?.ForegroundHex)
                        ? genStyle.ForegroundHex.Trim()
                        : (vertical ? "#193778" : "#5a5a5a"),
                    "#5a5a5a");

                double headerMm = PtToMm(bandLabelPt);
                var line = new StringBuilder(160);
                line.Append("<text x=\"").Append(F(options.MarginMm)).Append("\" y=\"")
                    .Append(F(band.Ymm + headerMm * 0.85))
                    .Append("\" font-family=\"").Append(EscAttr(genFont))
                    .Append(", sans-serif\" font-size=\"").Append(F(headerMm));
                if (genBold)
                {
                    line.Append("\" font-weight=\"bold");
                }

                line.Append("\" fill=\"").Append(EscAttr(genFill)).Append("\">Đời ")
                    .Append(band.Level).Append("</text>");
                writer.WriteLine(line.ToString());
            }
        }

        private static void DrawConnectors(
            SvgExportWriter writer,
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options)
        {
            double thin = Math.Max(0.35, options.CardPaddingMm * 0.12);
            double bus = thin + 0.25;

            foreach (var link in result.Connectors)
            {
                double sw = link.Kind == GiaPhaConnectorKind.Bus ? bus : thin;
                writer.WriteLine("<line x1=\"" + F(link.X1mm) + "\" y1=\"" + F(link.Y1mm)
                    + "\" x2=\"" + F(link.X2mm) + "\" y2=\"" + F(link.Y2mm)
                    + "\" stroke=\"#232323\" stroke-width=\"" + F(sw) + "\"/>");
            }
        }

        private static void DrawCards(
            SvgExportWriter writer,
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options,
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId,
            bool simpleBoxes)
        {
            foreach (var placed in result.Nodes)
            {
                DrawCard(writer, placed, options, boxStyleForFamilyId, simpleBoxes);
            }
        }

        private static void DrawCard(
            SvgExportWriter writer,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId,
            bool simpleBoxes)
        {
            double x = placed.Xmm;
            double y = placed.Ymm;
            double w = placed.Metrics.WidthMm;
            double h = placed.Metrics.HeightMm;
            double pad = options.CardPaddingMm;

            int familyId = placed.Family?.familyInfo?.FamilyId ?? 0;
            PhaDoBoxStyle boxStyle = null;
            if (boxStyleForFamilyId != null && familyId != 0)
            {
                boxStyle = boxStyleForFamilyId(familyId);
            }

            PhaDoBoxSvgVectorExport.DrawBoxBackground(
                writer, x, y, w, h, familyId, boxStyle,
                Math.Max(0.2, options.CardPaddingMm * 0.08),
                useSimpleRectOnly: simpleBoxes);

            if (GiaPhaRenderOptions.IsVerticalCardLayout(options.CardLayoutMode))
            {
                DrawCardVertical(writer, placed, options, boxStyle, x, y, pad);
                return;
            }

            DrawCardHorizontal(writer, placed, options, boxStyle, x, y, pad, w);
        }

        private static void DrawCardHorizontal(
            SvgExportWriter writer,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            PhaDoBoxStyle boxStyle,
            double x,
            double y,
            double pad,
            double w)
        {
            int slot = PhaDoBoxVisualTag.FamilyLabelSlotIndex;
            double lineTopMm = y + pad * 0.4;
            lineTopMm = AppendCardTextLine(
                writer, placed.Metrics.FamilyLabel, x + pad, lineTopMm,
                options, boxStyle, slot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.GenerationLabel);

            slot = PhaDoBoxVisualTag.MainPersonSlotIndex;
            lineTopMm = y + options.CardHeaderHeightMm + pad * 0.5;

            if (placed.Metrics.MainPerson != null)
            {
                lineTopMm = AppendCardTextLine(
                    writer, FormatMain(placed.Metrics.MainPerson), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.Person);
                slot++;
            }
            else if (placed.Family != null)
            {
                string label = placed.Family.Name0 ?? placed.Family.Name ?? "Gia đình";
                lineTopMm = AppendCardTextLine(
                    writer, label, x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.Person);
                slot++;
            }

            foreach (var spouse in placed.Metrics.Spouses)
            {
                lineTopMm = AppendCardTextLine(
                    writer, FormatSpouse(spouse), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.Person);
                slot++;
            }

            foreach (var overflow in placed.Metrics.SpouseOverflow)
            {
                if (!string.IsNullOrWhiteSpace(overflow))
                {
                    lineTopMm = AppendCardTextLine(
                        writer, overflow, x + pad, lineTopMm,
                        options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.Person);
                    slot++;
                }
            }

            AppendCardExtraNotes(writer, placed, options, boxStyle, x, pad, w, ref lineTopMm, slot);
        }

        private static double AppendCardTextLine(
            SvgExportWriter writer,
            string text,
            double xMm,
            double layoutTopMm,
            GiaPhaRenderOptions options,
            PhaDoBoxStyle boxStyle,
            int slot,
            PhaDoPersonTextRole role,
            PhaDoBoxElementKind elementKind)
        {
            var off = PhaDoSvgExportHelper.GetPersonSlotOffset(boxStyle, slot);
            double drawX = xMm + off.DeltaXmm;
            double drawTop = layoutTopMm + off.DeltaYmm;

            double defaultPt = PhaDoSvgExportHelper.DefaultFontPt(options, elementKind, role);
            var style = PhaDoSvgExportHelper.ResolveSlotTextStyle(boxStyle, options, slot, role, elementKind);
            PhaDoSvgExportHelper.ResolveDrawParams(
                style, options, defaultPt, role, elementKind,
                out double fontPt, out string fontFamily, out string fillHex, out bool bold);

            double fontMm = PtToMm(fontPt);
            double baselineY = drawTop + fontMm * 0.85;
            fillHex = NormalizeHex(fillHex, "#000000");

            var line = new StringBuilder(160);
            line.Append("<text x=\"").Append(F(drawX)).Append("\" y=\"").Append(F(baselineY))
                .Append("\" font-family=\"").Append(EscAttr(fontFamily))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm));
            if (bold)
            {
                line.Append("\" font-weight=\"bold");
            }

            line.Append("\" fill=\"").Append(EscAttr(fillHex)).Append("\">")
                .Append(EscText(text))
                .Append("</text>");
            writer.WriteLine(line.ToString());

            double step = Math.Max(options.CardLineHeightMm, fontPt * 0.58);
            return layoutTopMm + step;
        }

        private static void AppendCardExtraNotes(
            SvgExportWriter writer,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            PhaDoBoxStyle boxStyle,
            double x,
            double pad,
            double w,
            ref double lineTopMm,
            int startSlot)
        {
            var notes = placed.Metrics.ExtraNotes;
            if (notes == null || notes.Count == 0)
            {
                return;
            }

            lineTopMm += options.CardNoteTopGapMm;
            int noteSlot = 0;
            foreach (var note in notes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                int slot = startSlot + noteSlot;
                lineTopMm = AppendCardTextLine(
                    writer, note.Trim(), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.ExtraNote);
                noteSlot++;
            }
        }

        private static void DrawCardVertical(
            SvgExportWriter writer,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            PhaDoBoxStyle boxStyle,
            double x,
            double y,
            double pad)
        {
            double contentTop = y + pad * 0.5;
            double colX = x + pad;
            int personSlot = PhaDoBoxVisualTag.MainPersonSlotIndex;

            var columns = options.CardLayoutMode == GiaPhaCardLayoutMode.VerticalWord
                ? GiaPhaVerticalWordCardLayout.BuildColumns(placed.Metrics, options, placed.Family)
                : GiaPhaVerticalCardLayout.BuildColumns(placed.Metrics, options, placed.Family);

            bool isWord = options.CardLayoutMode == GiaPhaCardLayoutMode.VerticalWord;
            if (!isWord && !string.IsNullOrWhiteSpace(placed.Metrics.FamilyLabel))
            {
                double doiFontPt = options.VerticalGenerationLabelFontPt > 0
                    ? options.VerticalGenerationLabelFontPt
                    : options.HeaderFontPt;
                double doiColW = GiaPhaVerticalCardLayout.ColumnWidthMm(doiFontPt, options);
                int doiSlot = PhaDoBoxVisualTag.FamilyLabelSlotIndex;
                var doiOff = PhaDoSvgExportHelper.GetPersonSlotOffset(boxStyle, doiSlot);
                var doiStyle = PhaDoSvgExportHelper.ResolveSlotTextStyle(
                    boxStyle, options, doiSlot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.GenerationLabel);
                PhaDoSvgExportHelper.ResolveDrawParams(
                    doiStyle, options, doiFontPt, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.GenerationLabel,
                    out double fontPt, out string fontFamily, out string fillHex, out bool bold);
                AppendVerticalText(writer, placed.Metrics.FamilyLabel,
                    colX + doiOff.DeltaXmm, contentTop + doiOff.DeltaYmm, doiColW,
                    fontPt, bold, options, fillHex, fontFamily);
                colX += doiColW + GiaPhaVerticalCardLayout.ColumnGapMm;
            }

            foreach (var col in columns)
            {
                var role = col.Bold ? PhaDoPersonTextRole.Main : PhaDoPersonTextRole.Spouse;
                var off = PhaDoSvgExportHelper.GetPersonSlotOffset(boxStyle, personSlot);
                var slotStyle = PhaDoSvgExportHelper.ResolveSlotTextStyle(
                    boxStyle, options, personSlot, role, PhaDoBoxElementKind.Person);
                PhaDoSvgExportHelper.ResolveDrawParams(
                    slotStyle, options, col.FontPt, role, PhaDoBoxElementKind.Person,
                    out double fontPt, out string fontFamily, out string fillHex, out bool bold);

                if (col.IsWordStack)
                {
                    AppendVerticalWordStackText(writer, col.HorizontalWordLines,
                        colX + off.DeltaXmm, contentTop + off.DeltaYmm, col.WidthMm,
                        fontPt, bold, options, fillHex, fontFamily);
                }
                else
                {
                    AppendVerticalText(writer, col.Text,
                        colX + off.DeltaXmm, contentTop + off.DeltaYmm, col.WidthMm,
                        fontPt, bold, options, fillHex, fontFamily);
                }

                personSlot++;
                colX += col.WidthMm + GiaPhaVerticalCardLayout.ColumnGapMm;
            }

            AppendCardExtraNotesVertical(writer, placed, options, boxStyle, x, y, pad, personSlot);
        }

        private static void AppendVerticalWordStackText(
            SvgExportWriter writer,
            string[] words,
            double columnLeft,
            double columnTop,
            double columnWidthMm,
            double fontPt,
            bool bold,
            GiaPhaRenderOptions options,
            string fillHex,
            string fontFamily)
        {
            if (words == null || words.Length == 0)
            {
                return;
            }

            double lineStep = GiaPhaVerticalWordCardLayout.WordLineHeightMm(fontPt, options);
            double fontMm = PtToMm(fontPt);
            double y = columnTop + fontMm * 0.85;
            fillHex = NormalizeHex(fillHex, "#000000");

            for (int i = 0; i < words.Length; i++)
            {
                double centerX = columnLeft + columnWidthMm / 2.0;
                var line = new StringBuilder(128);
                line.Append("<text x=\"").Append(F(centerX)).Append("\" y=\"").Append(F(y))
                    .Append("\" font-family=\"").Append(EscAttr(fontFamily))
                    .Append(", sans-serif\" font-size=\"").Append(F(fontMm))
                    .Append("\" text-anchor=\"middle\"");
                if (bold)
                {
                    line.Append(" font-weight=\"bold\"");
                }

                line.Append(" fill=\"").Append(EscAttr(fillHex)).Append("\">")
                    .Append(EscText(words[i])).Append("</text>");
                writer.WriteLine(line.ToString());
                y += lineStep;
            }
        }

        private static void AppendVerticalText(
            SvgExportWriter writer,
            string text,
            double columnLeft,
            double columnTop,
            double columnWidthMm,
            double fontPt,
            bool bold,
            GiaPhaRenderOptions options,
            string fillHex,
            string fontFamily)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            double colCenter = GiaPhaVerticalCardLayout.ColumnCenterMm(columnLeft, columnWidthMm);
            double fontMm = PtToMm(fontPt);
            fillHex = NormalizeHex(fillHex, "#000000");

            var line = new StringBuilder(text.Length + 160);
            line.Append("<text x=\"").Append(F(colCenter)).Append("\" y=\"").Append(F(columnTop))
                .Append("\" font-family=\"").Append(EscAttr(fontFamily))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm)).Append('"');
            if (bold)
            {
                line.Append(" font-weight=\"bold\"");
            }

            line.Append(" writing-mode=\"vertical-rl\" text-anchor=\"middle\" dominant-baseline=\"hanging\" fill=\"")
                .Append(EscAttr(fillHex)).Append("\">")
                .Append(EscText(text))
                .Append("</text>");
            writer.WriteLine(line.ToString());
        }

        private static void AppendCardExtraNotesVertical(
            SvgExportWriter writer,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            PhaDoBoxStyle boxStyle,
            double x,
            double y,
            double pad,
            int startSlot)
        {
            var notes = placed.Metrics.ExtraNotes;
            if (notes == null || notes.Count == 0)
            {
                return;
            }

            double notePt = options.NoteFontPt > 0 ? options.NoteFontPt : 6.5;
            double innerMaxMm = Math.Max(8, placed.Metrics.WidthMm - options.CardPaddingMm * 2);
            double noteBlockMm = options.CardNoteTopGapMm;
            foreach (var note in notes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                noteBlockMm += FamilyCardMetrics.EstimateWrappedLineHeightMm(
                    note.Trim(), notePt, innerMaxMm, options);
            }

            double lineTopMm = y + placed.Metrics.HeightMm - options.CardBottomPaddingMm - noteBlockMm
                + options.CardNoteTopGapMm;
            int noteSlot = 0;
            foreach (var note in notes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                int slot = PhaDoBoxVisualTag.MainPersonSlotIndex + 100 + noteSlot;
                lineTopMm = AppendCardTextLine(
                    writer, note.Trim(), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.ExtraNote);
                noteSlot++;
            }
        }

        private static string FormatMain(PersonInfo p)
        {
            return (p.MANS_NAME_HUY ?? "").Trim();
        }

        private static string FormatSpouse(PersonInfo p)
        {
            string g = p.MANS_GENDER == "Nữ" ? "♀" : "♂";
            return g + " " + (p.MANS_NAME_HUY ?? "").Trim();
        }

        private static string NormalizeHex(string hex, string fallback)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return fallback;
            }

            hex = hex.Trim();
            if (!hex.StartsWith("#", StringComparison.Ordinal))
            {
                hex = "#" + hex;
            }

            return hex;
        }

        private static string F(double v)
        {
            return v.ToString("0.###", Inv);
        }

        /// <summary>Escape XML một lần — tránh chuỗi trung gian lớn.</summary>
        private static string EscText(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            if (s.IndexOf('&') < 0 && s.IndexOf('<') < 0 && s.IndexOf('>') < 0 && s.IndexOf('"') < 0)
            {
                return s;
            }

            var sb = new StringBuilder(s.Length + 16);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default: sb.Append(c); break;
                }
            }

            return sb.ToString();
        }

        private static string EscAttr(string s)
        {
            return EscText(s ?? "Segoe UI");
        }
    }
}
