using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Mục combo khung SVG — dùng chung dialog ô và tiêu đề.</summary>
    public sealed class PhaDoSvgFrameListItem
    {
        public enum FrameKind
        {
            DefaultRect = 0,
            Catalog = 1,
            CreateNew = 2
        }

        public FrameKind Kind { get; set; }
        public string SvgId { get; set; }
        public string Display { get; set; }
        public PhaDoSvgShape Shape { get; set; }
    }

    public static class PhaDoSvgFrameListBuilder
    {
        public static List<PhaDoSvgFrameListItem> Build(IDictionary<string, PhaDoSvgShape> catalog)
        {
            var items = new List<PhaDoSvgFrameListItem>
            {
                new PhaDoSvgFrameListItem
                {
                    Kind = PhaDoSvgFrameListItem.FrameKind.DefaultRect,
                    Display = "Mặc định (rect bo góc)"
                }
            };

            if (catalog != null)
            {
                foreach (var kv in catalog.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    var shape = kv.Value;
                    if (shape == null || string.IsNullOrWhiteSpace(shape.SvgBase64))
                    {
                        continue;
                    }

                    string id = string.IsNullOrWhiteSpace(shape.SvgId) ? kv.Key : shape.SvgId;
                    items.Add(new PhaDoSvgFrameListItem
                    {
                        Kind = PhaDoSvgFrameListItem.FrameKind.Catalog,
                        SvgId = id,
                        Shape = shape,
                        Display = FormatCatalogDisplay(id, shape)
                    });
                }
            }

            items.Add(new PhaDoSvgFrameListItem
            {
                Kind = PhaDoSvgFrameListItem.FrameKind.CreateNew,
                Display = "+ Tạo khung mới..."
            });

            return items;
        }

        public static string FormatCatalogDisplay(string svgId, PhaDoSvgShape shape)
        {
            string label = PhaDoSvgCatalog.IsAutoHashSvgId(svgId)
                ? (svgId.Length > 14 ? svgId.Substring(0, 14) + "…" : svgId)
                : svgId;
            return label + " (" + shape.ViewBoxWidth.ToString("0.##", CultureInfo.InvariantCulture)
                + "×" + shape.ViewBoxHeight.ToString("0.##", CultureInfo.InvariantCulture) + ")";
        }
    }
}
