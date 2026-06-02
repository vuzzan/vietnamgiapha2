using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Nhúng khung SVG tùy chỉnh vào file SVG phả đồ xuất ra.</summary>
    public static class PhaDoBoxSvgVectorExport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Cache parse SVG khung — tránh XDocument.Parse lặp hàng nghìn lần khi xuất.</summary>
        private static readonly Dictionary<string, CachedInnerSvg> InnerSvgCache =
            new Dictionary<string, CachedInnerSvg>(StringComparer.Ordinal);

        private sealed class CachedInnerSvg
        {
            public string InnerXml;
            public double ViewBoxWidth;
            public double ViewBoxHeight;
        }

        private static readonly string[] BranchFillHex =
        {
            "#FFF3E0", "#E8F5E9", "#E3F2FD", "#FCE4EC", "#EDE7F6", "#FFF9C4"
        };

        /// <summary>Vẽ nền ô: rect mặc định hoặc fill + khung SVG (giống canvas WPF).</summary>
        public static void DrawBoxBackground(
            SvgExportWriter writer,
            double xMm,
            double yMm,
            double wMm,
            double hMm,
            int familyId,
            PhaDoBoxStyle style,
            double defaultStrokeMm = 0.2,
            bool useSimpleRectOnly = false)
        {
            DrawFrameBackground(writer, xMm, yMm, wMm, hMm, familyId, style, defaultStrokeMm, useSimpleRectOnly);
        }

        /// <summary>Vẽ nền khung (rect hoặc SVG) — dùng cho ô gia đình và khối tiêu đề.</summary>
        public static void DrawFrameBackground(
            SvgExportWriter writer,
            double xMm,
            double yMm,
            double wMm,
            double hMm,
            int familyId,
            IPhaDoSvgFrameStyle style,
            double defaultStrokeMm = 0.2,
            bool useSimpleRectOnly = false)
        {
            string fill = ResolveFillHex(familyId, style);
            string markup = style?.CustomShapeSvg;
            double vbW = style?.CustomShapeViewBoxWidth ?? 100;
            double vbH = style?.CustomShapeViewBoxHeight ?? 80;

            if (!useSimpleRectOnly
                && !string.IsNullOrWhiteSpace(markup)
                && TryGetInnerSvg(markup, ref vbW, ref vbH, out string inner))
            {
                if (!string.IsNullOrWhiteSpace(fill))
                {
                    AppendFillRect(writer, xMm, yMm, wMm, hMm, fill, stroke: false);
                }

                writer.Write("<svg x=\"" + F(xMm) + "\" y=\"" + F(yMm)
                    + "\" width=\"" + F(wMm) + "\" height=\"" + F(hMm)
                    + "\" viewBox=\"0 0 " + F(vbW) + ' ' + F(vbH)
                    + "\" preserveAspectRatio=\"none\" overflow=\"visible\">");
                writer.WriteLine();
                writer.WriteLine(inner);
                writer.WriteLine("</svg>");
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
            writer.WriteLine("<rect x=\"" + F(xMm) + "\" y=\"" + F(yMm)
                + "\" width=\"" + F(wMm) + "\" height=\"" + F(hMm)
                + "\" rx=\"" + F(rx) + "\" fill=\"" + fill
                + "\" stroke=\"#505050\" stroke-width=\"" + F(sw)
                + "\"/>");
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
            SvgExportWriter writer,
            double x,
            double y,
            double w,
            double h,
            string fill,
            bool stroke)
        {
            string line = "<rect x=\"" + F(x) + "\" y=\"" + F(y)
                + "\" width=\"" + F(w) + "\" height=\"" + F(h)
                + "\" fill=\"" + fill + '"';
            if (stroke)
            {
                line += " stroke=\"#505050\" stroke-width=\"0.2\"";
            }

            writer.WriteLine(line + "/>");
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

            if (InnerSvgCache.TryGetValue(sanitizedSvgMarkup, out CachedInnerSvg cached))
            {
                viewBoxWidth = cached.ViewBoxWidth;
                viewBoxHeight = cached.ViewBoxHeight;
                innerXml = cached.InnerXml;
                return !string.IsNullOrEmpty(innerXml);
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

                double vbW = viewBoxWidth;
                double vbH = viewBoxHeight;
                if (TryParseViewBox(root, out double parsedW, out double parsedH))
                {
                    if (parsedW > 0)
                    {
                        vbW = parsedW;
                    }

                    if (parsedH > 0)
                    {
                        vbH = parsedH;
                    }
                }

                var inner = new StringBuilder(256);
                foreach (var node in root.Nodes())
                {
                    if (node is XElement el)
                    {
                        inner.Append(el.ToString(SaveOptions.DisableFormatting));
                    }
                }

                innerXml = inner.ToString();
                if (innerXml.Length == 0)
                {
                    return false;
                }

                viewBoxWidth = vbW;
                viewBoxHeight = vbH;
                InnerSvgCache[sanitizedSvgMarkup] = new CachedInnerSvg
                {
                    InnerXml = innerXml,
                    ViewBoxWidth = vbW,
                    ViewBoxHeight = vbH
                };
                return true;
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
