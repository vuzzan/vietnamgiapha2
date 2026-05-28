using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Catalog svgId → SVG (Base64) trong file gia phả; family chỉ giữ svgId.</summary>
    public static class PhaDoSvgCatalog
    {
        /// <summary>Index mảng root trong file .json gia phả.</summary>
        public const int RootJsonSvgCatalogIndex = 12;

        /// <summary>Index thêm trong mảng family info (sau Height).</summary>
        public const int FamilyInfoSvgIdIndex = 9;

        public static string ComputeStableId(string sanitizedSvgMarkup)
        {
            if (string.IsNullOrWhiteSpace(sanitizedSvgMarkup))
            {
                return null;
            }

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sanitizedSvgMarkup));
                var sb = new StringBuilder("s", 17);
                for (int i = 0; i < 8; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }

        public static string UpsertShape(GiaphaInfo gp, string sanitizedSvgMarkup, double viewBoxWidth, double viewBoxHeight)
        {
            string hashId = ComputeStableId(sanitizedSvgMarkup);
            if (string.IsNullOrWhiteSpace(hashId))
            {
                return null;
            }

            return UpsertShapeWithId(gp, hashId, sanitizedSvgMarkup, viewBoxWidth, viewBoxHeight);
        }

        /// <summary>Chuẩn hóa tên do người dùng nhập thành svgId an toàn cho JSON.</summary>
        public static string NormalizeUserSvgId(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                return null;
            }

            var sb = new StringBuilder();
            foreach (char c in userName.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    sb.Append(c);
                }
                else if (char.IsWhiteSpace(c) || c == '.')
                {
                    sb.Append('_');
                }
            }

            string id = sb.ToString().Trim('_');
            if (id.Length == 0)
            {
                return null;
            }

            if (char.IsDigit(id[0]))
            {
                id = "f_" + id;
            }

            if (id.Length > 48)
            {
                id = id.Substring(0, 48);
            }

            return id;
        }

        public static bool IsAutoHashSvgId(string svgId)
        {
            return !string.IsNullOrWhiteSpace(svgId)
                && svgId.Length == 17
                && svgId[0] == 's';
        }

        public static string UpsertShapeWithId(
            GiaphaInfo gp,
            string svgId,
            string sanitizedSvgMarkup,
            double viewBoxWidth,
            double viewBoxHeight)
        {
            if (gp == null || string.IsNullOrWhiteSpace(svgId) || string.IsNullOrWhiteSpace(sanitizedSvgMarkup))
            {
                return null;
            }

            if (gp.SvgShapesById == null)
            {
                gp.SvgShapesById = new Dictionary<string, PhaDoSvgShape>(StringComparer.Ordinal);
            }

            gp.SvgShapesById[svgId] = PhaDoSvgShape.FromMarkup(svgId, sanitizedSvgMarkup, viewBoxWidth, viewBoxHeight);
            return svgId;
        }

        public static void ResolveShapeIntoStyle(PhaDoBoxStyle style, IDictionary<string, PhaDoSvgShape> catalog)
        {
            ResolveShapeIntoFrame(style, catalog);
        }

        public static void ResolveShapeIntoFrame(IPhaDoSvgFrameStyle style, IDictionary<string, PhaDoSvgShape> catalog)
        {
            if (style == null || string.IsNullOrWhiteSpace(style.ShapeSvgId) || catalog == null)
            {
                return;
            }

            if (!catalog.TryGetValue(style.ShapeSvgId, out var shape) || shape == null)
            {
                return;
            }

            string markup = shape.GetSvgMarkup();
            if (string.IsNullOrWhiteSpace(markup))
            {
                return;
            }

            style.ApplyResolvedMarkup(markup, shape.ViewBoxWidth, shape.ViewBoxHeight, style.ShapeSvgId);
        }

        public static void PruneUnused(GiaphaInfo gp)
        {
            if (gp?.SvgShapesById == null || gp.SvgShapesById.Count == 0 || gp.familyRoot == null)
            {
                return;
            }

            var used = new HashSet<string>(StringComparer.Ordinal);
            CollectUsedSvgIds(gp.familyRoot, used);

            var remove = gp.SvgShapesById.Keys.Where(id => !used.Contains(id)).ToList();
            foreach (string id in remove)
            {
                gp.SvgShapesById.Remove(id);
            }
        }

        public static void CollectUsedSvgIds(FamilyInfo family, ISet<string> used)
        {
            if (family == null || used == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(family.PhaDoShapeSvgId))
            {
                used.Add(family.PhaDoShapeSvgId);
            }

            if (family.FamilyChildren == null)
            {
                return;
            }

            foreach (var child in family.FamilyChildren)
            {
                CollectUsedSvgIds(child, used);
            }
        }

        public static string ToJsonArray(IDictionary<string, PhaDoSvgShape> shapes)
        {
            if (shapes == null || shapes.Count == 0)
            {
                return "[]";
            }

            var sb = new StringBuilder("[");
            bool first = true;
            foreach (var kv in shapes.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var shape = kv.Value;
                if (shape == null || string.IsNullOrWhiteSpace(shape.SvgBase64))
                {
                    continue;
                }

                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                string id = string.IsNullOrWhiteSpace(shape.SvgId) ? kv.Key : shape.SvgId;
                sb.Append('[');
                sb.Append('"').Append(EscapeJsonString(id)).Append("\",");
                sb.Append('"').Append(EscapeJsonString(shape.SvgBase64)).Append("\",");
                sb.Append(shape.ViewBoxWidth.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(shape.ViewBoxHeight.ToString(CultureInfo.InvariantCulture));
                sb.Append(']');
            }

            sb.Append(']');
            return sb.ToString();
        }

        public static Dictionary<string, PhaDoSvgShape> ParseJsonArray(string jsonArrayText)
        {
            var result = new Dictionary<string, PhaDoSvgShape>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(jsonArrayText))
            {
                return result;
            }

            try
            {
                using (var doc = JsonDocument.Parse(jsonArrayText))
                {
                    if (doc.RootElement.ValueKind != JsonValueKind.Array)
                    {
                        return result;
                    }

                    foreach (var entry in doc.RootElement.EnumerateArray())
                    {
                        TryAddParsedCatalogEntry(result, entry);
                    }
                }
            }
            catch
            {
                // Thử parse lại sau khi chuẩn hóa (System.Json đôi khi ToString() khác chuẩn)
                try
                {
                    string normalized = jsonArrayText.Trim();
                    if (!normalized.StartsWith("[", StringComparison.Ordinal))
                    {
                        normalized = "[" + normalized + "]";
                    }

                    using (var doc = JsonDocument.Parse(normalized))
                    {
                        foreach (var entry in doc.RootElement.EnumerateArray())
                        {
                            TryAddParsedCatalogEntry(result, entry);
                        }
                    }
                }
                catch
                {
                    // File cũ hoặc JSON lỗi — bỏ qua catalog
                }
            }

            return result;
        }

        private static void TryAddParsedCatalogEntry(Dictionary<string, PhaDoSvgShape> result, JsonElement entry)
        {
            if (entry.ValueKind != JsonValueKind.Array || entry.GetArrayLength() < 4)
            {
                return;
            }

            string svgId = entry[0].GetString();
            string svgBase64 = entry[1].GetString();
            if (string.IsNullOrWhiteSpace(svgId) || string.IsNullOrWhiteSpace(svgBase64))
            {
                return;
            }

            double vbW = entry[2].ValueKind == JsonValueKind.Number ? entry[2].GetDouble() : 100;
            double vbH = entry[3].ValueKind == JsonValueKind.Number ? entry[3].GetDouble() : 80;
            result[svgId] = new PhaDoSvgShape
            {
                SvgId = svgId,
                SvgBase64 = svgBase64,
                ViewBoxWidth = vbW,
                ViewBoxHeight = vbH
            };
        }

        public static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", "\\n");
        }
    }
}
