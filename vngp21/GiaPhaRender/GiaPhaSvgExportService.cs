using System;
using System.Globalization;
using System.IO;
using System.Text;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Xuất phả đồ ra SVG vector (mm) — mở được trên trình duyệt, Inkscape, Illustrator…</summary>
    public static class GiaPhaSvgExportService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = result.Options ?? GiaPhaRenderOptions.ForFitContentPrint();
            double wMm = result.PageWidthMm > 0 ? result.PageWidthMm : 200;
            double hMm = result.PageHeightMm > 0 ? result.PageHeightMm : 200;

            var sb = new StringBuilder(64 * 1024);
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ");
            sb.Append("width=\"").Append(F(wMm)).Append("mm\" ");
            sb.Append("height=\"").Append(F(hMm)).Append("mm\" ");
            sb.Append("viewBox=\"0 0 ").Append(F(wMm)).Append(' ').Append(F(hMm)).AppendLine("\">");

            sb.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

            DrawTitle(sb, options, titleStyle);
            DrawGenerationBands(sb, result, options, wMm);
            DrawCards(sb, result, options, boxStyleForFamilyId);
            DrawConnectors(sb, result, options); // trên ô — giống canvas

            sb.AppendLine("</svg>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static void DrawTitle(StringBuilder sb, GiaPhaRenderOptions options, PhaDoTitleStyle titleStyle)
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
                sb,
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
                AppendTitleLine(sb, options.Title, layout.TextLeftMm + off.DeltaXmm, layout.Line1TopMm + off.DeltaYmm,
                    options.TitleLine1FontFamily ?? options.FontFamilyName,
                    options.TitleFontPt, options.TitleLine1ForegroundHex, bold: true);
            }

            if (has2)
            {
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 1);
                AppendTitleLine(sb, options.TitleLine2, layout.TextLeftMm + off.DeltaXmm, layout.Line2TopMm + off.DeltaYmm,
                    options.TitleLine2FontFamily ?? options.FontFamilyName,
                    options.TitleLine2FontPt, options.TitleLine2ForegroundHex, bold: false);
            }

            if (has3)
            {
                double l3Pt = options.TitleLine3FontPt > 0 ? options.TitleLine3FontPt : defaultSmallPt;
                string l3Fore = !string.IsNullOrWhiteSpace(options.TitleLine3ForegroundHex)
                    ? options.TitleLine3ForegroundHex : "#888888";
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 2);
                AppendTitleLine(sb, options.TitleLine3, layout.TextLeftMm + off.DeltaXmm, layout.Line3TopMm + off.DeltaYmm,
                    options.TitleLine3FontFamily ?? options.FontFamilyName,
                    l3Pt, l3Fore, bold: false);
            }

            if (has4)
            {
                double l4Pt = options.TitleLine4FontPt > 0 ? options.TitleLine4FontPt : defaultSmallPt;
                string l4Fore = !string.IsNullOrWhiteSpace(options.TitleLine4ForegroundHex)
                    ? options.TitleLine4ForegroundHex : "#888888";
                var off = PhaDoSvgExportHelper.GetTitleLineOffset(titleStyle, 3);
                AppendTitleLine(sb, options.TitleLine4, layout.TextLeftMm + off.DeltaXmm, layout.Line4TopMm + off.DeltaYmm,
                    options.TitleLine4FontFamily ?? options.FontFamilyName,
                    l4Pt, l4Fore, bold: false);
            }
        }

        private static double AppendTitleLine(
            StringBuilder sb,
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
            string fill = string.IsNullOrWhiteSpace(foregroundHex) ? "#000000" : foregroundHex.Trim();
            if (!fill.StartsWith("#", StringComparison.Ordinal))
            {
                fill = "#" + fill;
            }

            sb.Append("<text x=\"").Append(F(xMm)).Append("\" y=\"").Append(F(baselineY))
                .Append("\" font-family=\"").Append(EscAttr(fontFamily ?? "Segoe UI"))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm));
            if (bold)
            {
                sb.Append("\" font-weight=\"bold");
            }

            sb.Append("\" fill=\"").Append(EscAttr(fill)).Append("\">")
                .Append(EscText(text))
                .AppendLine("</text>");

            return topMm + Math.Max(5, fontPt * 0.38);
        }

        private static void DrawGenerationBands(
            StringBuilder sb,
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options,
            double pageWmm)
        {
            for (int i = 0; i < result.GenerationBands.Count; i++)
            {
                var band = result.GenerationBands[i];
                string fill = i % 2 == 0 ? "#F8FAFC" : "#EBF1F5";
                sb.Append("<rect x=\"0\" y=\"").Append(F(band.Ymm)).Append("\" width=\"")
                    .Append(F(pageWmm)).Append("\" height=\"").Append(F(band.HeightMm))
                    .Append("\" fill=\"").Append(fill).AppendLine("\"/>");

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
                string genFill = !string.IsNullOrWhiteSpace(genStyle?.ForegroundHex)
                    ? genStyle.ForegroundHex.Trim()
                    : (vertical ? "#193778" : "#5a5a5a");
                if (!genFill.StartsWith("#", StringComparison.Ordinal))
                {
                    genFill = "#" + genFill;
                }

                double headerMm = PtToMm(bandLabelPt);
                sb.Append("<text x=\"").Append(F(options.MarginMm)).Append("\" y=\"")
                    .Append(F(band.Ymm + headerMm * 0.85))
                    .Append("\" font-family=\"").Append(EscAttr(genFont))
                    .Append(", sans-serif\" font-size=\"").Append(F(headerMm));
                if (genBold)
                {
                    sb.Append("\" font-weight=\"bold");
                }

                sb.Append("\" fill=\"").Append(EscAttr(genFill)).Append("\">Đời ")
                    .Append(band.Level).AppendLine("</text>");
            }
        }

        private static void DrawConnectors(
            StringBuilder sb,
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options)
        {
            double thin = Math.Max(0.35, options.CardPaddingMm * 0.12);
            double bus = thin + 0.25;

            foreach (var link in result.Connectors)
            {
                double sw = link.Kind == GiaPhaConnectorKind.Bus ? bus : thin;
                sb.Append("<line x1=\"").Append(F(link.X1mm)).Append("\" y1=\"")
                    .Append(F(link.Y1mm)).Append("\" x2=\"").Append(F(link.X2mm))
                    .Append("\" y2=\"").Append(F(link.Y2mm))
                    .Append("\" stroke=\"#232323\" stroke-width=\"")
                    .Append(F(sw)).AppendLine("\"/>");
            }
        }

        private static void DrawCards(
            StringBuilder sb,
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options,
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId)
        {
            foreach (var placed in result.Nodes)
            {
                DrawCard(sb, placed, options, boxStyleForFamilyId);
            }
        }

        private static void DrawCard(
            StringBuilder sb,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId)
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
                sb, x, y, w, h, familyId, boxStyle,
                Math.Max(0.2, options.CardPaddingMm * 0.08));

            if (GiaPhaRenderOptions.IsVerticalCardLayout(options.CardLayoutMode))
            {
                DrawCardVertical(sb, placed, options, boxStyle, x, y, pad);
                return;
            }

            DrawCardHorizontal(sb, placed, options, boxStyle, x, y, pad, w);
        }

        private static void DrawCardHorizontal(
            StringBuilder sb,
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
                sb, placed.Metrics.FamilyLabel, x + pad, lineTopMm,
                options, boxStyle, slot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.GenerationLabel);

            slot = PhaDoBoxVisualTag.MainPersonSlotIndex;
            lineTopMm = y + options.CardHeaderHeightMm + pad * 0.5;

            if (placed.Metrics.MainPerson != null)
            {
                lineTopMm = AppendCardTextLine(
                    sb, FormatMain(placed.Metrics.MainPerson), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.Person);
                slot++;
            }
            else if (placed.Family != null)
            {
                string label = placed.Family.Name0 ?? placed.Family.Name ?? "Gia đình";
                lineTopMm = AppendCardTextLine(
                    sb, label, x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.Person);
                slot++;
            }

            foreach (var spouse in placed.Metrics.Spouses)
            {
                lineTopMm = AppendCardTextLine(
                    sb, FormatSpouse(spouse), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.Person);
                slot++;
            }

            foreach (var overflow in placed.Metrics.SpouseOverflow)
            {
                if (!string.IsNullOrWhiteSpace(overflow))
                {
                    lineTopMm = AppendCardTextLine(
                        sb, overflow, x + pad, lineTopMm,
                        options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.Person);
                    slot++;
                }
            }

            AppendCardExtraNotes(sb, placed, options, boxStyle, x, pad, w, ref lineTopMm, slot);
        }

        /// <summary>Một dòng chữ ngang — vẽ tại layoutTop + offset slot; trả về top layout dòng kế (không cộng dồn offset).</summary>
        private static double AppendCardTextLine(
            StringBuilder sb,
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
            if (!fillHex.StartsWith("#", StringComparison.Ordinal))
            {
                fillHex = "#" + fillHex;
            }

            sb.Append("<text x=\"").Append(F(drawX)).Append("\" y=\"").Append(F(baselineY))
                .Append("\" font-family=\"").Append(EscAttr(fontFamily))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm));
            if (bold)
            {
                sb.Append("\" font-weight=\"bold");
            }

            sb.Append("\" fill=\"").Append(EscAttr(fillHex)).Append("\">")
                .Append(EscText(text))
                .AppendLine("</text>");

            double step = Math.Max(options.CardLineHeightMm, fontPt * 0.58);
            return layoutTopMm + step;
        }

        private static void AppendCardExtraNotes(
            StringBuilder sb,
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
                    sb, note.Trim(), x + pad, lineTopMm,
                    options, boxStyle, slot, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.ExtraNote);
                noteSlot++;
            }
        }

        private static void DrawCardVertical(
            StringBuilder sb,
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
                AppendVerticalText(sb, placed.Metrics.FamilyLabel,
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
                    AppendVerticalWordStackText(sb, col.HorizontalWordLines,
                        colX + off.DeltaXmm, contentTop + off.DeltaYmm, col.WidthMm,
                        fontPt, bold, options, fillHex, fontFamily);
                }
                else
                {
                    AppendVerticalText(sb, col.Text,
                        colX + off.DeltaXmm, contentTop + off.DeltaYmm, col.WidthMm,
                        fontPt, bold, options, fillHex, fontFamily);
                }

                personSlot++;
                colX += col.WidthMm + GiaPhaVerticalCardLayout.ColumnGapMm;
            }

            AppendCardExtraNotesVertical(sb, placed, options, boxStyle, x, y, pad, personSlot);
        }

        /// <summary>Word: mỗi từ một dòng chữ ngang trong cột người.</summary>
        private static void AppendVerticalWordStackText(
            StringBuilder sb,
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

            for (int i = 0; i < words.Length; i++)
            {
                double centerX = columnLeft + columnWidthMm / 2.0;
                if (!fillHex.StartsWith("#", StringComparison.Ordinal))
                {
                    fillHex = "#" + fillHex;
                }

                sb.Append("<text x=\"").Append(F(centerX)).Append("\" y=\"").Append(F(y))
                    .Append("\" font-family=\"").Append(EscAttr(fontFamily))
                    .Append(", sans-serif\" font-size=\"").Append(F(fontMm))
                    .Append("\" text-anchor=\"middle\"");
                if (bold)
                {
                    sb.Append(" font-weight=\"bold\"");
                }

                sb.Append(" fill=\"").Append(EscAttr(fillHex)).Append("\">").Append(EscText(words[i])).AppendLine("</text>");
                y += lineStep;
            }
        }

        private static void AppendVerticalText(
            StringBuilder sb,
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

            if (!fillHex.StartsWith("#", StringComparison.Ordinal))
            {
                fillHex = "#" + fillHex;
            }

            sb.Append("<text x=\"").Append(F(colCenter)).Append("\" y=\"").Append(F(columnTop))
                .Append("\" font-family=\"").Append(EscAttr(fontFamily))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm)).Append('"');
            if (bold)
            {
                sb.Append(" font-weight=\"bold\"");
            }

            sb.Append(" writing-mode=\"vertical-rl\" text-anchor=\"middle\" dominant-baseline=\"hanging\" fill=\"")
                .Append(EscAttr(fillHex)).Append("\">")
                .Append(EscText(text))
                .AppendLine("</text>");
        }

        private static double AppendTextLine(
            StringBuilder sb,
            string text,
            double x,
            double y,
            GiaPhaRenderOptions options,
            double fontPt,
            bool bold)
        {
            return AppendTextLine(sb, text, x, y, options, fontPt, bold, "#000000");
        }

        private static double AppendTextLine(
            StringBuilder sb,
            string text,
            double x,
            double y,
            GiaPhaRenderOptions options,
            double fontPt,
            bool bold,
            string fillHex)
        {
            double fontMm = PtToMm(fontPt);
            sb.Append("<text x=\"").Append(F(x)).Append("\" y=\"").Append(F(y))
                .Append("\" font-family=\"").Append(EscAttr(options.FontFamilyName))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm)).Append('"');
            if (bold)
            {
                sb.Append(" font-weight=\"bold\"");
            }

            sb.Append(" fill=\"").Append(EscAttr(fillHex ?? "#000000")).Append("\">")
                .Append(EscText(text)).AppendLine("</text>");

            double step = Math.Max(options.CardLineHeightMm, fontPt * 0.58);
            return y + step;
        }

        private static void AppendCardExtraNotesVertical(
            StringBuilder sb,
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
                    sb, note.Trim(), x + pad, lineTopMm,
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

        private static string F(double v)
        {
            return v.ToString("0.###", Inv);
        }

        private static string EscText(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }

            return s
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }

        private static string EscAttr(string s)
        {
            return EscText(s ?? "Segoe UI");
        }
    }
}
