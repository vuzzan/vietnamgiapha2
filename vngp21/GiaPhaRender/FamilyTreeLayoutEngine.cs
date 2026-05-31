using System;
using System.Collections.Generic;
using System.Linq;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>
    /// Thuật toán cây căn giữa (Reingold–Tilford đơn giản):
    /// đặt con theo FamilyOrder, cha nằm giữa dải con.
    /// </summary>
    public sealed class FamilyTreeLayoutEngine
    {
        private readonly GiaPhaRenderOptions _options;
        private readonly double _dpi;
        private readonly Dictionary<FamilyViewModel, LayoutTreeNode> _map = new Dictionary<FamilyViewModel, LayoutTreeNode>();
        private double _maxLevel;
        private double _contentWidthMm;
        private double _contentHeightMm;

        public FamilyTreeLayoutEngine(GiaPhaRenderOptions options, double dpi)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _dpi = dpi;
        }

        public GiaPhaRenderResult Layout(FamilyViewModel root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            _map.Clear();
            var layoutRoot = BuildTree(root, 0);
            double cursorMm = 0;
            LayoutSubtree(layoutRoot, ref cursorMm);
            ResolveHorizontalOverlaps(layoutRoot);

            AssignVerticalByGeneration(layoutRoot);
            ComputeBounds(layoutRoot);

            double scale = 1.0;
            double offsetXmm = _options.MarginMm;
            double offsetYmm = _options.MarginMm + TitleBlockHeightMm();

            if (_options.FitContentToTree)
            {
                // 100% — không scale; khổ = đúng cây
                scale = 1.0;
            }
            else if (_options.ScaleToFitPage)
            {
                double availW = _options.ContentWidthMm;
                double availH = _options.ContentHeightMm - TitleBlockHeightMm();
                double sx = availW / Math.Max(_contentWidthMm, 1);
                double sy = availH / Math.Max(_contentHeightMm, 1);
                scale = Math.Min(1.0, Math.Min(sx, sy));
            }

            double scaledW = _contentWidthMm * scale;
            double scaledH = _contentHeightMm * scale;

            if (_options.FitContentToTree)
            {
                offsetXmm = _options.MarginMm;
                offsetYmm = _options.MarginMm + TitleBlockHeightMm();
            }
            else if (_options.CenterContentOnPage)
            {
                offsetXmm = _options.MarginMm + (_options.ContentWidthMm - scaledW) / 2.0;
                offsetYmm = _options.MarginMm + TitleBlockHeightMm()
                    + (_options.ContentHeightMm - TitleBlockHeightMm() - scaledH) / 2.0;
            }
            else
            {
                offsetXmm = _options.MarginMm;
                offsetYmm = _options.MarginMm + TitleBlockHeightMm();
            }

            ApplyTransform(layoutRoot, offsetXmm, offsetYmm, scale);

            return BuildResult(layoutRoot, scale, offsetXmm, offsetYmm, scaledW, scaledH);
        }

        private LayoutTreeNode BuildTree(FamilyViewModel family, int level)
        {
            var metrics = FamilyCardMetrics.Measure(family, _options, _dpi);
            var node = new LayoutTreeNode(family, metrics) { Level = level };
            _map[family] = node;
            _maxLevel = Math.Max(_maxLevel, level);

            var children = family.Children
                .OrderBy(c => c.familyInfo.FamilyOrder)
                .ThenBy(c => c.familyInfo.FamilyId)
                .ToList();

            foreach (var child in children)
            {
                node.Children.Add(BuildTree(child, level + 1));
            }

            return node;
        }

        /// <summary>Trả về cạnh phải của subtree (mm).</summary>
        private double LayoutSubtree(LayoutTreeNode node, ref double leftMm)
        {
            if (node.Children.Count == 0)
            {
                node.Xmm = leftMm;
                leftMm += node.WidthMm + _options.HorizontalGapMm;
                return leftMm - _options.HorizontalGapMm;
            }

            double childLeft = leftMm;
            foreach (var child in node.Children)
            {
                LayoutSubtree(child, ref childLeft);
            }

            double childrenLeft = node.Children[0].Xmm;
            double childrenRight = node.Children[node.Children.Count - 1].Xmm
                + node.Children[node.Children.Count - 1].WidthMm;

            node.Xmm = (childrenLeft + childrenRight - node.WidthMm) / 2.0;

            double subtreeRight = Math.Max(node.Xmm + node.WidthMm, childLeft - _options.HorizontalGapMm);
            double subtreeLeft = Math.Min(node.Xmm, childrenLeft);
            leftMm = Math.Max(leftMm, subtreeRight + _options.HorizontalGapMm);

            return subtreeRight;
        }

        /// <summary>Đẩy subtree sang phải nếu các node cùng độ sâu layout chồng nhau (lặp nhiều pass).</summary>
        private void ResolveHorizontalOverlaps(LayoutTreeNode root)
        {
            var byGeneration = CollectByLayoutDepth(root);
            double gap = _options.HorizontalGapMm;

            for (int pass = 0; pass < 12; pass++)
            {
                bool anyShift = false;
                foreach (var gen in byGeneration.Keys.OrderBy(k => k))
                {
                    var row = byGeneration[gen].OrderBy(n => n.Xmm).ToList();
                    for (int i = 1; i < row.Count; i++)
                    {
                        var prev = row[i - 1];
                        var cur = row[i];
                        double prevRight = prev.Xmm + prev.WidthMm;
                        if (cur.Xmm < prevRight + gap)
                        {
                            double dx = prevRight + gap - cur.Xmm;
                            ShiftSubtreeX(cur, dx);
                            anyShift = true;
                        }
                    }
                }
                if (!anyShift)
                {
                    break;
                }
            }
        }

        /// <summary>Nhóm theo độ sâu từ gốc layout (0,1,2…) — Root1→Root2 gọn như Root0→Root1 (FamilyLevel trên box vẫn tuyệt đối).</summary>
        private static Dictionary<int, List<LayoutTreeNode>> CollectByLayoutDepth(LayoutTreeNode root)
        {
            var byDepth = new Dictionary<int, List<LayoutTreeNode>>();
            Visit(root, n =>
            {
                int depth = n.Level;
                if (!byDepth.ContainsKey(depth))
                {
                    byDepth[depth] = new List<LayoutTreeNode>();
                }
                byDepth[depth].Add(n);
            });
            return byDepth;
        }

        private static void ShiftSubtreeX(LayoutTreeNode node, double dx)
        {
            Visit(node, n => n.Xmm += dx);
        }

        private void AssignVerticalByGeneration(LayoutTreeNode root)
        {
            var byGeneration = CollectByLayoutDepth(root);

            double yMm = 0;
            foreach (var gen in byGeneration.Keys.OrderBy(k => k))
            {
                var row = byGeneration[gen];
                // Hàng đời = max(SlotHeight) — ô layout lớn hơn thẻ, tránh tràn sang đời sau
                double rowSlotMm = row.Max(n => n.SlotHeightMm);
                foreach (var n in row)
                {
                    n.Ymm = yMm;
                }
                yMm += rowSlotMm + _options.GenerationGapMm;
            }
        }

        private void ComputeBounds(LayoutTreeNode node)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            Visit(node, n =>
            {
                minX = Math.Min(minX, n.Xmm);
                minY = Math.Min(minY, n.Ymm);
                maxX = Math.Max(maxX, n.Xmm + n.WidthMm);
                maxY = Math.Max(maxY, n.Ymm + n.SlotHeightMm);
            });
            _contentWidthMm = maxX - minX;
            _contentHeightMm = maxY - minY;
            if (double.IsInfinity(minX))
            {
                _contentWidthMm = 0;
                _contentHeightMm = 0;
            }
            else
            {
                ShiftTree(node, -minX, -minY);
            }
        }

        private static void ShiftTree(LayoutTreeNode node, double dx, double dy)
        {
            node.Xmm += dx;
            node.Ymm += dy;
            foreach (var c in node.Children)
            {
                ShiftTree(c, dx, dy);
            }
        }

        private void ApplyTransform(LayoutTreeNode node, double offsetX, double offsetY, double scale)
        {
            Visit(node, n =>
            {
                n.Xmm = n.Xmm * scale + offsetX;
                n.Ymm = n.Ymm * scale + offsetY;
            });
        }

        private GiaPhaRenderResult BuildResult(
            LayoutTreeNode root,
            double scale,
            double offsetXmm,
            double offsetYmm,
            double scaledW,
            double scaledH)
        {
            double canvasWmm;
            double canvasHmm;

            if (_options.FitContentToTree)
            {
                canvasWmm = scaledW + _options.MarginMm * 2;
                canvasHmm = scaledH + _options.MarginMm * 2 + TitleBlockHeightMm();
            }
            else
            {
                canvasWmm = _options.PageWidthMm;
                canvasHmm = _options.PageHeightMm;
            }

            double canvasWpx = PrintUnits.MmToPixels(canvasWmm, _dpi);
            double canvasHpx = PrintUnits.MmToPixels(canvasHmm, _dpi);

            var result = new GiaPhaRenderResult
            {
                Options = _options,
                Dpi = _dpi,
                Scale = scale,
                PageWidthMm = canvasWmm,
                PageHeightMm = canvasHmm,
                PageWidthPixels = canvasWpx,
                PageHeightPixels = canvasHpx,
                CanvasWidthPixels = canvasWpx,
                CanvasHeightPixels = canvasHpx,
                ContentWidthMm = _contentWidthMm * scale,
                ContentHeightMm = _contentHeightMm * scale
            };

            Visit(root, n =>
            {
                result.Nodes.Add(new GiaPhaPlacedNode
                {
                    Family = n.Family,
                    Metrics = n.Metrics,
                    Xmm = n.Xmm,
                    Ymm = n.Ymm,
                    Level = n.Level
                });
            });

            BuildConnectors(root, result);
            BuildGenerationBands(result);
            return result;
        }

        private void BuildConnectors(LayoutTreeNode node, GiaPhaRenderResult result)
        {
            if (node.Children.Count == 0)
            {
                return;
            }

            double parentCx = node.Xmm + node.WidthMm / 2.0;
            double parentBottom = node.Ymm + node.HeightMm;

            var childCenters = node.Children
                .Select(c => new { cx = c.Xmm + c.WidthMm / 2.0, top = c.Ymm })
                .ToList();

            double childTop = childCenters.Min(c => c.top);
            double gap = childTop - parentBottom;
            if (gap < _options.BusLineGapMm)
            {
                gap = _options.BusLineGapMm;
            }
            // Bus ngang nằm giữa khe hai đời
            double busY = parentBottom + gap * 0.5;

            // Thân dọc: đáy cha → bus
            result.Connectors.Add(new GiaPhaConnector
            {
                Kind = GiaPhaConnectorKind.Trunk,
                X1mm = parentCx,
                Y1mm = parentBottom,
                X2mm = parentCx,
                Y2mm = busY
            });

            // Bus ngang: luôn vẽ (kể cả 1 con — đời 1,2,3 thường chỉ có 1 nhánh)
            double busLeft = childCenters.Min(c => c.cx);
            double busRight = childCenters.Max(c => c.cx);
            busLeft = Math.Min(busLeft, parentCx);
            busRight = Math.Max(busRight, parentCx);
            double span = busRight - busLeft;
            if (span < _options.MinBusSpanMm)
            {
                double mid = (busLeft + busRight) / 2.0;
                busLeft = mid - _options.MinBusSpanMm / 2.0;
                busRight = mid + _options.MinBusSpanMm / 2.0;
            }

            result.Connectors.Add(new GiaPhaConnector
            {
                Kind = GiaPhaConnectorKind.Bus,
                X1mm = busLeft,
                Y1mm = busY,
                X2mm = busRight,
                Y2mm = busY
            });

            // Nhánh dọc: bus → đỉnh từng con
            foreach (var c in childCenters)
            {
                result.Connectors.Add(new GiaPhaConnector
                {
                    Kind = GiaPhaConnectorKind.Branch,
                    X1mm = c.cx,
                    Y1mm = busY,
                    X2mm = c.cx,
                    Y2mm = c.top
                });
            }

            foreach (var child in node.Children)
            {
                BuildConnectors(child, result);
            }
        }

        private void BuildGenerationBands(GiaPhaRenderResult result)
        {
            var byGen = result.Nodes
                .GroupBy(n => n.Family?.familyInfo?.FamilyLevel ?? n.Level)
                .OrderBy(g => g.Key);

            foreach (var g in byGen)
            {
                double yMin = g.Min(n => n.Ymm);
                double rowSlotMm = g.Max(n => n.Metrics.SlotHeightMm * result.Scale);
                int displayGen = g.Key;
                result.GenerationBands.Add(new GiaPhaGenerationBand
                {
                    Level = displayGen,
                    Ymm = yMin - 1,
                    // Vùng chứa = đúng một hàng đời (slot), không ăn sang khe đường nối
                    HeightMm = rowSlotMm + 2
                });
            }
        }

        private double TitleBlockHeightMm()
        {
            return PhaDoTitleStyleResolver.TitleBlockHeightMm(_options);
        }

        private static void Visit(LayoutTreeNode node, Action<LayoutTreeNode> action)
        {
            action(node);
            foreach (var c in node.Children)
            {
                Visit(c, action);
            }
        }
    }
}
