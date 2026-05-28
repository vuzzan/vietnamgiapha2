using System;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Nhúng khung SVG tùy chỉnh vào file SVG phả đồ xuất ra.</summary>
    public static class PhaDoBoxSvgVectorExport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] BranchFillHex =
        {
            "#FFF3E0", "#E8F5E9", "#E3F2FD", "#FCE4EC", "#EDE7F6", "#FFF9C4"
        };

        /// <summary>Vẽ nền ô: rect mặc định hoặc fill + khung SVG (giống canvas WPF).</summary>
        public static void DrawBoxBackground(
            StringBuilder sb,
            double xMm,
            double yMm,
            double wMm,
            double hMm,
            int familyId,
            PhaDoBoxStyle style,
            double defaultStrokeMm = 0.2)
        {
            DrawFrameBackground(sb, xMm, yMm, wMm, hMm, familyId, style, defaultStrokeMm);
        }

        /// <summary>Vẽ nền khung (rect hoặc SVG) — dùng cho ô gia đình và khối tiêu đề.</summary>
        public static void DrawFrameBackground(
            StringBuilder sb,
            double xMm,
            double yMm,
            double wMm,
            double hMm,
            int familyId,
            IPhaDoSvgFrameStyle style,
            double defaultStrokeMm = 0.2)
        {
            string fill = ResolveFillHex(familyId, style);
            string markup = style?.CustomShapeSvg;
            double vbW = style?.CustomShapeViewBoxWidth ?? 100;
            double vbH = style?.CustomShapeViewBoxHeight ?? 80;

            if (!string.IsNullOrWhiteSpace(markup)
                && TryGetInnerSvg(markup, ref vbW, ref vbH, out string inner))
            {
                if (!string.IsNullOrWhiteSpace(fill))
                {
                    AppendFillRect(sb, xMm, yMm, wMm, hMm, fill, stroke: false);
                }

                sb.Append("<svg x=\"").Append(F(xMm)).Append("\" y=\"").Append(F(yMm))
                    .Append("\" width=\"").Append(F(wMm)).Append("\" height=\"").Append(F(hMm))
                    .Append("\" viewBox=\"0 0 ").Append(F(vbW)).Append(' ').Append(F(vbH))
                    .AppendLine("\" preserveAspectRatio=\"none\" overflow=\"visible\">");
                sb.AppendLine(inner);
                sb.AppendLine("</svg>");
                return;
            }

            if (string.IsNullOrWhiteSpace(fill))
            {
                if (familyId < 0)
                {
                    return;
                }

                fill = BranchFillHex[Math.Abs(familyId) % BranchFillHex.Length];
            }

            double rx = 1.5;
            double sw = Math.Max(0.2, defaultStrokeMm);
            sb.Append("<rect x=\"").Append(F(xMm)).Append("\" y=\"").Append(F(yMm))
                .Append("\" width=\"").Append(F(wMm)).Append("\" height=\"").Append(F(hMm))
                .Append("\" rx=\"").Append(F(rx)).Append("\" fill=\"").Append(fill)
                .Append("\" stroke=\"#505050\" stroke-width=\"").Append(F(sw))
                .AppendLine("\"/>");
        }

        private static string ResolveFillHex(int familyId, IPhaDoSvgFrameStyle style)
        {
            if (!string.IsNullOrWhiteSpace(style?.FillColorHex))
            {
                string hex = style.FillColorHex.Trim();
                if (!hex.StartsWith("#", StringComparison.Ordinal))
                {
                    hex = "#" + hex;
                }

                return hex;
            }

            if (familyId < 0)
            {
                return "#FFFFFF";
            }

            return BranchFillHex[Math.Abs(familyId) % BranchFillHex.Length];
        }

        private static void AppendFillRect(
            StringBuilder sb,
            double x,
            double y,
            double w,
            double h,
            string fill,
            bool stroke)
        {
            sb.Append("<rect x=\"").Append(F(x)).Append("\" y=\"").Append(F(y))
                .Append("\" width=\"").Append(F(w)).Append("\" height=\"").Append(F(h))
                .Append("\" fill=\"").Append(fill).Append('"');
            if (stroke)
            {
                sb.Append(" stroke=\"#505050\" stroke-width=\"0.2\"");
            }

            sb.AppendLine("/>");
        }

        private static bool TryGetInnerSvg(
            string sanitizedSvgMarkup,
            ref double viewBoxWidth,
            ref double viewBoxHeight,
            out string innerXml)
        {
            innerXml = null;
            if (string.IsNullOrWhiteSpace(sanitizedSvgMarkup))
            {
                return false;
            }

            try
            {
                var doc = XDocument.Parse(sanitizedSvgMarkup, LoadOptions.None);
                XElement root = doc.Root;
                if (root == null
                    || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (TryParseViewBox(root, out double vbW, out double vbH))
                {
                    if (vbW > 0)
                    {
                        viewBoxWidth = vbW;
                    }

                    if (vbH > 0)
                    {
                        viewBoxHeight = vbH;
                    }
                }

                var inner = new StringBuilder();
                foreach (var node in root.Nodes())
                {
                    if (node is XElement el)
                    {
                        inner.Append(el.ToString(SaveOptions.DisableFormatting));
                    }
                }

                innerXml = inner.ToString();
                return innerXml.Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryParseViewBox(XElement svg, out double w, out double h)
        {
            w = h = 0;
            string vb = svg.Attribute("viewBox")?.Value;
            if (string.IsNullOrWhiteSpace(vb))
            {
                return false;
            }

            string[] parts = vb.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return false;
            }

            if (!double.TryParse(parts[2], NumberStyles.Float, Inv, out w))
            {
                return false;
            }

            return double.TryParse(parts[3], NumberStyles.Float, Inv, out h);
        }

        private static string F(double v) => v.ToString("0.###", Inv);
    }
}
