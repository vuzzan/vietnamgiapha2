using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Trích &lt;svg&gt; từ HTML/SVG dán vào, loại tag/attribute nguy hiểm.</summary>
    public sealed class PhaDoBoxSvgSanitizeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string SanitizedSvgMarkup { get; set; }
        public double ViewBoxWidth { get; set; } = 100;
        public double ViewBoxHeight { get; set; } = 80;
    }

    public static class PhaDoBoxSvgSanitizer
    {
        private static readonly HashSet<string> BlockedLocalNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "foreignObject", "iframe", "embed", "object", "link", "audio", "video"
        };

        private static readonly XName SvgName = XName.Get("svg", "http://www.w3.org/2000/svg");

        public static PhaDoBoxSvgSanitizeResult Sanitize(string rawInput)
        {
            var result = new PhaDoBoxSvgSanitizeResult();
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                result.Success = true;
                result.Message = "Để trống = dùng khung rect bo góc mặc định.";
                return result;
            }

            string svgFragment = ExtractSvgMarkup(rawInput);
            if (string.IsNullOrWhiteSpace(svgFragment))
            {
                result.Message = "Không tìm thấy thẻ <svg> trong nội dung dán.";
                return result;
            }

            // Bỏ DOCTYPE / XML declaration — file Illustrator/Inkscape hay có, gây lỗi parse.
            svgFragment = StripXmlProlog(svgFragment);

            try
            {
                var doc = XDocument.Parse(svgFragment, LoadOptions.None);
                XElement root = doc.Root;
                if (root == null || !string.Equals(root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
                {
                    result.Message = "Nội dung không phải SVG hợp lệ.";
                    return result;
                }

                StripUnsafeNodes(root);
                StripUnsafeAttributes(root);

                if (!TryParseViewBox(root, out double vbW, out double vbH))
                {
                    vbW = ParseLength(root.Attribute("width")?.Value, 100);
                    vbH = ParseLength(root.Attribute("height")?.Value, 80);
                }

                if (vbW < 1 || vbH < 1)
                {
                    vbW = 100;
                    vbH = 80;
                }

                result.SanitizedSvgMarkup = root.ToString(SaveOptions.DisableFormatting);
                result.ViewBoxWidth = vbW;
                result.ViewBoxHeight = vbH;
                result.Success = true;
                result.Message = "SVG hợp lệ (" + vbW.ToString("0.##") + " × " + vbH.ToString("0.##") + ").";
            }
            catch (Exception ex)
            {
                result.Message = "Lỗi đọc SVG: " + ex.Message;
            }

            return result;
        }

        private static string ExtractSvgMarkup(string raw)
        {
            raw = StripXmlProlog(raw?.Trim() ?? string.Empty);
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            // Cho phép khoảng trắng trước '>' — Inkscape hay xuất </svg\n>
            var match = Regex.Match(
                raw,
                @"<svg\b[\s\S]*?</svg\s*>",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (match.Success)
            {
                return match.Value;
            }

            // Fallback: tìm thủ công khi regex không khớp (tag mở/đóng lạ)
            int start = raw.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return null;
            }

            int close = raw.LastIndexOf("</svg", StringComparison.OrdinalIgnoreCase);
            if (close < start)
            {
                return null;
            }

            int endGt = raw.IndexOf('>', close);
            if (endGt < 0)
            {
                return null;
            }

            return raw.Substring(start, endGt - start + 1);
        }

        /// <summary>Loại bỏ &lt;?xml …?&gt; và &lt;!DOCTYPE …&gt; trước khi parse.</summary>
        private static string StripXmlProlog(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return raw;
            }

            string s = raw.TrimStart();
            for (int pass = 0; pass < 4; pass++)
            {
                if (s.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                {
                    int end = s.IndexOf("?>", StringComparison.Ordinal);
                    if (end < 0)
                    {
                        break;
                    }

                    s = s.Substring(end + 2).TrimStart();
                    continue;
                }

                if (s.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
                {
                    int end = s.IndexOf('>');
                    if (end < 0)
                    {
                        break;
                    }

                    s = s.Substring(end + 1).TrimStart();
                    continue;
                }

                break;
            }

            return s;
        }

        private static void StripUnsafeNodes(XElement element)
        {
            foreach (var child in element.Elements().ToList())
            {
                if (BlockedLocalNames.Contains(child.Name.LocalName))
                {
                    child.Remove();
                    continue;
                }

                if (string.Equals(child.Name.LocalName, "image", StringComparison.OrdinalIgnoreCase))
                {
                    string href = child.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"))?.Value
                        ?? child.Attribute("href")?.Value;
                    if (!string.IsNullOrEmpty(href)
                        && (href.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                            || href.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                            || href.StartsWith("file:", StringComparison.OrdinalIgnoreCase)))
                    {
                        child.Remove();
                        continue;
                    }
                }

                StripUnsafeNodes(child);
            }
        }

        private static void StripUnsafeAttributes(XElement element)
        {
            foreach (var attr in element.Attributes().ToList())
            {
                string local = attr.Name.LocalName;
                if (local.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    attr.Remove();
                    continue;
                }

                if (string.Equals(local, "href", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(local, "xlink:href", StringComparison.OrdinalIgnoreCase))
                {
                    string val = attr.Value?.Trim() ?? "";
                    if (val.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                        || val.StartsWith("data:text/html", StringComparison.OrdinalIgnoreCase))
                    {
                        attr.Remove();
                    }
                }
            }

            foreach (var child in element.Elements())
            {
                StripUnsafeAttributes(child);
            }
        }

        private static bool TryParseViewBox(XElement svg, out double width, out double height)
        {
            width = 0;
            height = 0;
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

            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out width)
                && !double.TryParse(parts[2], out width))
            {
                return false;
            }

            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out height)
                && !double.TryParse(parts[3], out height))
            {
                return false;
            }

            return width > 0 && height > 0;
        }

        private static double ParseLength(string value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            value = value.Trim().ToLowerInvariant();
            int i = 0;
            while (i < value.Length && (char.IsDigit(value[i]) || value[i] == '.' || value[i] == '-'))
            {
                i++;
            }

            string num = value.Substring(0, i);
            if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)
                || double.TryParse(num, out v))
            {
                return v > 0 ? v : fallback;
            }

            return fallback;
        }
    }
}
