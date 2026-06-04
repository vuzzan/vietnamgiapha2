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
            /// <summary>title_* hoặc family_*.svg trong thư mục ZoneSvg.</summary>
            ZoneSvg = 1,
            Catalog = 2,
            CreateNew = 3
        }

        public FrameKind Kind { get; set; }
        public string SvgId { get; set; }
        public string Display { get; set; }
        public PhaDoSvgShape Shape { get; set; }
    }

    public static class PhaDoSvgFrameListBuilder
    {
        /// <param name="zoneSvgPrefix">
        /// <see cref="PhaDoZoneSvgFolderLoader.TitlePrefix"/> hoặc
        /// <see cref="PhaDoZoneSvgFolderLoader.FamilyPrefix"/> — null thì không nạp ZoneSvg.
        /// </param>
        public static List<PhaDoSvgFrameListItem> Build(
            IDictionary<string, PhaDoSvgShape> catalog,
            string zoneSvgPrefix = null)
        {
            var items = new List<PhaDoSvgFrameListItem>
            {
                new PhaDoSvgFrameListItem
                {
                    Kind = PhaDoSvgFrameListItem.FrameKind.DefaultRect,
                    Display = "Mặc định (rect bo góc)"
                }
            };

            var zoneIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(zoneSvgPrefix))
            {
                AddZoneSvgEntries(items, zoneIds, zoneSvgPrefix);
            }

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
                    if (zoneIds.Contains(id))
                    {
                        continue;
                    }

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

        private static void AddZoneSvgEntries(
            List<PhaDoSvgFrameListItem> items,
            HashSet<string> zoneIds,
            string zoneSvgPrefix)
        {
            var zoneLoaded = PhaDoZoneSvgFolderLoader.Load(PhaDoZoneSvgFolderLoader.ResolveFolderPath());
            List<PhaDoZoneSvgFileEntry> entries = GetZoneEntriesForPrefix(zoneLoaded, zoneSvgPrefix);
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries.OrderBy(e => e.Id, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(entry?.Id)
                    || !zoneLoaded.ShapesById.TryGetValue(entry.Id, out var zoneShape)
                    || zoneShape == null
                    || string.IsNullOrWhiteSpace(zoneShape.GetSvgMarkup()))
                {
                    continue;
                }

                zoneIds.Add(entry.Id);
                items.Add(new PhaDoSvgFrameListItem
                {
                    Kind = PhaDoSvgFrameListItem.FrameKind.ZoneSvg,
                    SvgId = entry.Id,
                    Shape = zoneShape,
                    Display = FormatZoneDisplay(entry, zoneShape)
                });
            }
        }

        private static List<PhaDoZoneSvgFileEntry> GetZoneEntriesForPrefix(
            PhaDoZoneSvgFolderLoadResult zoneLoaded,
            string zoneSvgPrefix)
        {
            if (zoneLoaded == null || string.IsNullOrWhiteSpace(zoneSvgPrefix))
            {
                return null;
            }

            if (string.Equals(zoneSvgPrefix, PhaDoZoneSvgFolderLoader.TitlePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return zoneLoaded.TitleEntries;
            }

            if (string.Equals(zoneSvgPrefix, PhaDoZoneSvgFolderLoader.FamilyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return zoneLoaded.FamilyEntries;
            }

            var merged = new List<PhaDoZoneSvgFileEntry>();
            if (zoneLoaded.TitleEntries != null)
            {
                merged.AddRange(zoneLoaded.TitleEntries);
            }

            if (zoneLoaded.FamilyEntries != null)
            {
                merged.AddRange(zoneLoaded.FamilyEntries);
            }

            return merged
                .Where(e => e != null
                    && !string.IsNullOrWhiteSpace(e.Id)
                    && e.Id.StartsWith(zoneSvgPrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public static string FormatCatalogDisplay(string svgId, PhaDoSvgShape shape)
        {
            string label = PhaDoSvgCatalog.IsAutoHashSvgId(svgId)
                ? (svgId.Length > 14 ? svgId.Substring(0, 14) + "…" : svgId)
                : svgId;
            return label + " (" + shape.ViewBoxWidth.ToString("0.##", CultureInfo.InvariantCulture)
                + "×" + shape.ViewBoxHeight.ToString("0.##", CultureInfo.InvariantCulture) + ")";
        }

        private static string FormatZoneDisplay(PhaDoZoneSvgFileEntry entry, PhaDoSvgShape shape)
        {
            string shortName = !string.IsNullOrWhiteSpace(entry?.Display)
                ? entry.Display
                : (entry?.Id ?? shape?.SvgId ?? "?");
            return shortName + "  ·  ZoneSvg  ("
                + shape.ViewBoxWidth.ToString("0.##", CultureInfo.InvariantCulture)
                + "×" + shape.ViewBoxHeight.ToString("0.##", CultureInfo.InvariantCulture) + ")";
        }
    }
}
