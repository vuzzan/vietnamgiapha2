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
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId = null)
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

            DrawTitle(sb, options);
            DrawGenerationBands(sb, result, options, wMm);
            DrawCards(sb, result, options, boxStyleForFamilyId);
            DrawConnectors(sb, result, options); // trên ô — giống canvas

            sb.AppendLine("</svg>");

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static void DrawTitle(StringBuilder sb, GiaPhaRenderOptions options)
        {
            bool has1 = !string.IsNullOrWhiteSpace(options.Title);
            bool has2 = !string.IsNullOrWhiteSpace(options.TitleLine2);
            if (!has1 && !has2)
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

            if (has1)
            {
                AppendTitleLine(sb, options.Title, layout.TextLeftMm, layout.Line1TopMm,
                    options.TitleLine1FontFamily ?? options.FontFamilyName,
                    options.TitleFontPt, options.TitleLine1ForegroundHex, bold: true);
            }

            if (has2)
            {
                AppendTitleLine(sb, options.TitleLine2, layout.TextLeftMm, layout.Line2TopMm,
                    options.TitleLine2FontFamily ?? options.FontFamilyName,
                    options.TitleLine2FontPt, options.TitleLine2ForegroundHex, bold: false);
            }
        }

        private static double AppendTitleLine(
            StringBuilder sb,
            string text,
            double xMm,
            double yMm,
            string fontFamily,
            double fontPt,
            string foregroundHex,
            bool bold)
        {
            double fontMm = PtToMm(fontPt);
            double baselineY = yMm + fontMm * 0.85;
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

            return yMm + Math.Max(5, fontPt * 0.38);
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
                double bandLabelPt = vertical
                    ? options.VerticalGenerationLabelFontPt
                    : options.HeaderFontPt;
                double headerMm = PtToMm(bandLabelPt);
                sb.Append("<text x=\"").Append(F(options.MarginMm)).Append("\" y=\"")
                    .Append(F(band.Ymm + headerMm * 0.85))
                    .Append("\" font-family=\"").Append(EscAttr(options.FontFamilyName))
                    .Append(", sans-serif\" font-size=\"").Append(F(headerMm));
                if (vertical)
                {
                    sb.Append("\" font-weight=\"600\" fill=\"#193778\">Đời ");
                }
                else
                {
                    sb.Append("\" fill=\"#5a5a5a\">Đời ");
                }

                sb.Append(band.Level).AppendLine("</text>");
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
                DrawCardVertical(sb, placed, options, x, y, pad);
                return;
            }

            double headerLabelMm = PtToMm(options.HeaderFontPt);
            sb.Append("<text x=\"").Append(F(x + pad)).Append("\" y=\"")
                .Append(F(y + pad * 0.4 + headerLabelMm * 0.85))
                .Append("\" font-family=\"").Append(EscAttr(options.FontFamilyName))
                .Append(", sans-serif\" font-size=\"").Append(F(headerLabelMm))
                .Append("\" fill=\"#464646\">")
                .Append(EscText(placed.Metrics.FamilyLabel))
                .AppendLine("</text>");

            double lineY = y + options.CardHeaderHeightMm + pad * 0.5 + PtToMm(options.MainNameFontPt) * 0.85;

            if (placed.Metrics.MainPerson != null)
            {
                lineY = AppendTextLine(sb, FormatMain(placed.Metrics.MainPerson),
                    x + pad, lineY, options, options.MainNameFontPt, true);
            }
            else if (placed.Family != null)
            {
                string label = placed.Family.Name0 ?? placed.Family.Name ?? "Gia đình";
                lineY = AppendTextLine(sb, label, x + pad, lineY, options,
                    options.MainNameFontPt, true);
            }

            foreach (var spouse in placed.Metrics.Spouses)
            {
                lineY = AppendTextLine(sb, FormatSpouse(spouse), x + pad, lineY,
                    options, options.SpouseFontPt, false);
            }

            foreach (var overflow in placed.Metrics.SpouseOverflow)
            {
                if (!string.IsNullOrWhiteSpace(overflow))
                {
                    lineY = AppendTextLine(sb, overflow, x + pad, lineY,
                        options, options.SpouseFontPt, false);
                }
            }
        }

        private static void DrawCardVertical(
            StringBuilder sb,
            GiaPhaPlacedNode placed,
            GiaPhaRenderOptions options,
            double x,
            double y,
            double pad)
        {
            // Thẻ dọc SVG: không vẽ dải header "Đời" trên box
            double contentTop = y + pad * 0.5;
            double colX = x + pad;

            var columns = options.CardLayoutMode == GiaPhaCardLayoutMode.VerticalWord
                ? GiaPhaVerticalWordCardLayout.BuildColumns(placed.Metrics, options, placed.Family)
                : GiaPhaVerticalCardLayout.BuildColumns(placed.Metrics, options, placed.Family);

            foreach (var col in columns)
            {
                if (col.IsWordStack)
                {
                    AppendVerticalWordStackText(sb, col.HorizontalWordLines, colX, contentTop, col.WidthMm,
                        col.FontPt, col.Bold, options);
                }
                else
                {
                    AppendVerticalText(sb, col.Text, colX, contentTop, col.WidthMm,
                        col.FontPt, col.Bold, options);
                }

                colX += col.WidthMm + GiaPhaVerticalCardLayout.ColumnGapMm;
            }
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
            GiaPhaRenderOptions options)
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
                sb.Append("<text x=\"").Append(F(centerX)).Append("\" y=\"").Append(F(y))
                    .Append("\" font-family=\"").Append(EscAttr(options.FontFamilyName))
                    .Append(", sans-serif\" font-size=\"").Append(F(fontMm))
                    .Append("\" text-anchor=\"middle\"");
                if (bold)
                {
                    sb.Append(" font-weight=\"bold\"");
                }

                sb.Append(" fill=\"#000000\">").Append(EscText(words[i])).AppendLine("</text>");
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
            string fillHex = "#000000")
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            double colCenter = GiaPhaVerticalCardLayout.ColumnCenterMm(columnLeft, columnWidthMm);
            double fontMm = PtToMm(fontPt);

            sb.Append("<text x=\"").Append(F(colCenter)).Append("\" y=\"").Append(F(columnTop))
                .Append("\" font-family=\"").Append(EscAttr(options.FontFamilyName))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm)).Append('"');
            if (bold)
            {
                sb.Append(" font-weight=\"bold\"");
            }

            sb.Append(" writing-mode=\"vertical-rl\" text-anchor=\"middle\" dominant-baseline=\"hanging\" fill=\"")
                .Append(fillHex).Append("\">")
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
            double fontMm = PtToMm(fontPt);
            sb.Append("<text x=\"").Append(F(x)).Append("\" y=\"").Append(F(y))
                .Append("\" font-family=\"").Append(EscAttr(options.FontFamilyName))
                .Append(", sans-serif\" font-size=\"").Append(F(fontMm)).Append('"');
            if (bold)
            {
                sb.Append(" font-weight=\"bold\"");
            }

            sb.Append(" fill=\"#000000\">")
                .Append(EscText(text)).AppendLine("</text>");

            double step = Math.Max(options.CardLineHeightMm, fontPt * 0.58);
            return y + step;
        }

        private static string FormatMain(PersonInfo p)
        {
            return "★ " + (p.MANS_NAME_HUY ?? "").Trim();
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
