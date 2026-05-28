using System;
using System.Collections.Generic;
using System.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Áp offset kéo-thả theo trục X và rebuild connectors.</summary>
    public static class GiaPhaManualLayoutService
    {
        public static GiaPhaRenderResult ApplyHorizontalOffsets(
            GiaPhaRenderResult baseResult,
            IReadOnlyDictionary<int, double> offsetXByFamilyIdMm)
        {
            return ApplyManualOffsets(baseResult, offsetXByFamilyIdMm, null);
        }

        public static GiaPhaRenderResult ApplyManualOffsets(
            GiaPhaRenderResult baseResult,
            IReadOnlyDictionary<int, double> offsetXByFamilyIdMm,
            IReadOnlyDictionary<int, double> offsetYByFamilyIdMm)
        {
            if (baseResult == null)
            {
                throw new ArgumentNullException(nameof(baseResult));
            }

            bool hasX = offsetXByFamilyIdMm != null && offsetXByFamilyIdMm.Count > 0;
            bool hasY = offsetYByFamilyIdMm != null && offsetYByFamilyIdMm.Count > 0;
            if (!hasX && !hasY)
            {
                return baseResult;
            }

            var result = CloneBase(baseResult);
            foreach (var node in baseResult.Nodes)
            {
                var adjusted = new GiaPhaPlacedNode
                {
                    Family = node.Family,
                    Metrics = node.Metrics,
                    Level = node.Level,
                    Xmm = node.Xmm + (hasX ? ResolveOffset(node, offsetXByFamilyIdMm) : 0),
                    Ymm = node.Ymm + (hasY ? ResolveOffset(node, offsetYByFamilyIdMm) : 0)
                };
                result.Nodes.Add(adjusted);
            }

            RebuildConnectors(result);
            return result;
        }

        private static double ResolveOffset(
            GiaPhaPlacedNode node,
            IReadOnlyDictionary<int, double> offsetXByFamilyIdMm)
        {
            int familyId = node.Family?.familyInfo?.FamilyId ?? 0;
            if (familyId == 0)
            {
                return 0;
            }

            return offsetXByFamilyIdMm.TryGetValue(familyId, out var deltaMm)
                ? deltaMm
                : 0;
        }

        private static GiaPhaRenderResult CloneBase(GiaPhaRenderResult source)
        {
            var clone = new GiaPhaRenderResult
            {
                Options = source.Options,
                Dpi = source.Dpi,
                Scale = source.Scale,
                PageWidthMm = source.PageWidthMm,
                PageHeightMm = source.PageHeightMm,
                PageWidthPixels = source.PageWidthPixels,
                PageHeightPixels = source.PageHeightPixels,
                ContentWidthMm = source.ContentWidthMm,
                ContentHeightMm = source.ContentHeightMm,
                CanvasWidthPixels = source.CanvasWidthPixels,
                CanvasHeightPixels = source.CanvasHeightPixels
            };

            foreach (var band in source.GenerationBands)
            {
                clone.GenerationBands.Add(new GiaPhaGenerationBand
                {
                    Level = band.Level,
                    Ymm = band.Ymm,
                    HeightMm = band.HeightMm
                });
            }

            return clone;
        }

        public static void RebuildConnectorsOnly(GiaPhaRenderResult result)
        {
            if (result == null)
            {
                return;
            }

            result.Connectors.Clear();
            RebuildConnectors(result);
        }

        private static void RebuildConnectors(GiaPhaRenderResult result)
        {
            var options = result.Options ?? GiaPhaRenderOptions.ForFitContent();
            var byFamily = result.Nodes
                .Where(n => n.Family != null)
                .ToDictionary(n => n.Family, n => n);

            foreach (var parent in result.Nodes.Where(n => n.Family != null))
            {
                var childNodes = parent.Family.Children
                    .Where(byFamily.ContainsKey)
                    .Select(c => byFamily[c])
                    .ToList();

                if (childNodes.Count == 0)
                {
                    continue;
                }

                double parentCx = parent.Xmm + parent.Metrics.WidthMm / 2.0;
                double parentBottom = parent.Ymm + parent.Metrics.HeightMm;
                double childTop = childNodes.Min(c => c.Ymm);
                double gap = childTop - parentBottom;
                if (gap < options.BusLineGapMm)
                {
                    gap = options.BusLineGapMm;
                }

                double busY = parentBottom + gap * 0.5;
                result.Connectors.Add(new GiaPhaConnector
                {
                    Kind = GiaPhaConnectorKind.Trunk,
                    X1mm = parentCx,
                    Y1mm = parentBottom,
                    X2mm = parentCx,
                    Y2mm = busY
                });

                double busLeft = childNodes.Min(c => c.Xmm + c.Metrics.WidthMm / 2.0);
                double busRight = childNodes.Max(c => c.Xmm + c.Metrics.WidthMm / 2.0);
                busLeft = Math.Min(busLeft, parentCx);
                busRight = Math.Max(busRight, parentCx);
                double span = busRight - busLeft;
                if (span < options.MinBusSpanMm)
                {
                    double mid = (busLeft + busRight) / 2.0;
                    busLeft = mid - options.MinBusSpanMm / 2.0;
                    busRight = mid + options.MinBusSpanMm / 2.0;
                }

                result.Connectors.Add(new GiaPhaConnector
                {
                    Kind = GiaPhaConnectorKind.Bus,
                    X1mm = busLeft,
                    Y1mm = busY,
                    X2mm = busRight,
                    Y2mm = busY
                });

                foreach (var child in childNodes)
                {
                    double childCx = child.Xmm + child.Metrics.WidthMm / 2.0;
                    result.Connectors.Add(new GiaPhaConnector
                    {
                        Kind = GiaPhaConnectorKind.Branch,
                        X1mm = childCx,
                        Y1mm = busY,
                        X2mm = childCx,
                        Y2mm = child.Ymm
                    });
                }
            }
        }
    }
}
