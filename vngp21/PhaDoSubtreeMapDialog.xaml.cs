using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    public partial class PhaDoSubtreeMapDialog : MetroWindow
    {
        private readonly double _dpi;
        private bool _pendingFitToView = true;
        private bool _isFullScreen;

        /// <summary>Tỉ lệ px/cm trên sơ đồ — tự co nếu quá rộng.</summary>
        private const double BasePxPerCm = 7.0;
        private const double MinBlockWidthPx = 220;
        private const double MinBlockHeightPx = 84;

        public PhaDoSubtreeMapDialog(double dpi = 96)
        {
            InitializeComponent();
            _dpi = dpi <= 0 ? 96 : dpi;
            Loaded += (_, __) =>
            {
                _pendingFitToView = true;
                ApplyFitToView();
            };
        }

        public void SetContent(
            string analysisReport,
            PhaDoSubtreeBranchBlock rootBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> subTrees,
            int rootLevelMax,
            int splitLevel)
        {
            if (analysisText != null)
            {
                analysisText.Text = analysisReport ?? "";
            }

            RenderMap(rootBlock, subTrees, rootLevelMax, splitLevel);
        }

        /// <summary>Render nhanh 3 tầng: cha -> gia đình hiện tại -> các gia đình con trực tiếp.</summary>
        public void SetFocusedChainContent(
            string analysisReport,
            PhaDoSubtreeBranchBlock parentBlock,
            PhaDoSubtreeBranchBlock currentBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> childBlocks)
        {
            if (analysisText != null)
            {
                analysisText.Text = analysisReport ?? "";
            }

            RenderFocusedChainMap(parentBlock, currentBlock, childBlocks ?? Array.Empty<PhaDoSubtreeBranchBlock>());
        }

        /// <summary>Sơ đồ root0 → các phả con (đời N): tên người chính + kích thước cm.</summary>
        private void RenderMap(
            PhaDoSubtreeBranchBlock rootBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> subTrees,
            int rootLevelMax,
            int splitLevel)
        {
            mapCanvas.Children.Clear();
            rootBlock = rootBlock ?? new PhaDoSubtreeBranchBlock { MainPersonName = "Root0" };
            subTrees = subTrees ?? Array.Empty<PhaDoSubtreeBranchBlock>();

            string splitNote;
            if (splitLevel <= 0 || subTrees.Count <= 1)
            {
                splitNote = subTrees.Count == 1
                    ? "Không tách phả con (chỉ 1 nhánh — coi như phả chính Root0)"
                    : "Không tách phả con";
            }
            else
            {
                splitNote = "Tách đời " + splitLevel + " → " + subTrees.Count + " phả con";
            }
            summaryText.Text = "Root0 (đời 1–" + rootLevelMax + ")  →  " + splitNote;

            RenderSubtreeHierarchyMap(rootBlock, subTrees);
        }

        /// <summary>
        /// Flow 2 (phả con): dựng cây theo cấp đời từ root0.
        ///  - root0 là gia đình 0
        ///  - cấp 1 là con của root0
        ///  - cấp 2 là con của cấp 1 (gán theo vị trí X gần nhất)
        /// Sau đó mới vẽ rect + connector.
        /// </summary>
        private void RenderSubtreeHierarchyMap(
            PhaDoSubtreeBranchBlock rootBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> subTrees)
        {
            const double padPx = 24;
            const double rowGapPx = 56;
            const double hGapPx = 16;
            const double connectorStubPx = 18;

            var allBlocks = new List<PhaDoSubtreeBranchBlock> { rootBlock };
            allBlocks.AddRange(subTrees.Where(s => s != null));
            double pxPerCm = ComputePxPerCm(rootBlock, allBlocks, padPx, hGapPx);

            var levelRows = BuildHierarchyRows(rootBlock, subTrees);
            var sizeById = new Dictionary<int, (double Width, double Height)>();
            foreach (var node in levelRows.SelectMany(r => r.Nodes))
            {
                sizeById[node.Block.FamilyId] = CalculateHierarchyBoxSize(
                    node.Block,
                    pxPerCm,
                    titleFontSize: node.IsRoot ? 15 : 13.5,
                    generationFontSize: 12.5);
            }

            double maxRowWidth = 0;
            foreach (var row in levelRows)
            {
                double rowW = row.Nodes.Sum(n => sizeById[n.Block.FamilyId].Width)
                    + Math.Max(0, row.Nodes.Count - 1) * hGapPx;
                maxRowWidth = Math.Max(maxRowWidth, rowW);
            }

            var yByRow = new Dictionary<int, double>();
            double currentY = padPx;
            foreach (var row in levelRows)
            {
                yByRow[row.Level] = currentY;
                double rowMaxH = row.Nodes.Max(n => sizeById[n.Block.FamilyId].Height);
                currentY += rowMaxH + rowGapPx;
            }

            double canvasW = maxRowWidth + padPx * 2;
            double canvasH = currentY - rowGapPx + padPx;
            mapCanvas.Width = Math.Max(canvasW, 640);
            mapCanvas.Height = Math.Max(canvasH, 420);

            var boxMap = new Dictionary<int, (double X, double Y, double W, double H)>();
            foreach (var row in levelRows)
            {
                double rowWidth = row.Nodes.Sum(n => sizeById[n.Block.FamilyId].Width)
                    + Math.Max(0, row.Nodes.Count - 1) * hGapPx;
                double x = (mapCanvas.Width - rowWidth) / 2.0;
                double y = yByRow[row.Level];
                foreach (var node in row.Nodes)
                {
                    var size = sizeById[node.Block.FamilyId];
                    boxMap[node.Block.FamilyId] = (x, y, size.Width, size.Height);
                    x += size.Width + hGapPx;
                }
            }

            // Vẽ connector trước để box đè lên nhìn sạch hơn.
            foreach (var row in levelRows)
            {
                foreach (var node in row.Nodes)
                {
                    if (node.ParentId <= 0 || !boxMap.TryGetValue(node.ParentId, out var parentRect))
                    {
                        continue;
                    }
                    if (!boxMap.TryGetValue(node.Block.FamilyId, out var childRect))
                    {
                        continue;
                    }

                    double pCx = parentRect.X + parentRect.W / 2.0;
                    double pBottom = parentRect.Y + parentRect.H;
                    double cCx = childRect.X + childRect.W / 2.0;
                    double cTop = childRect.Y;
                    double midY = Math.Min(cTop - 4, pBottom + connectorStubPx);
                    DrawConnector(pCx, pBottom, pCx, midY);
                    DrawConnector(pCx, midY, cCx, midY);
                    DrawConnector(cCx, midY, cCx, cTop);
                }
            }

            foreach (var row in levelRows)
            {
                foreach (var node in row.Nodes)
                {
                    var rect = boxMap[node.Block.FamilyId];
                    DrawSubtreeHierarchyBox(
                        rect.X,
                        rect.Y,
                        rect.W,
                        rect.H,
                        node.Block,
                        node.IsRoot);
                }
            }

            mapHost.Width = mapCanvas.Width;
            mapHost.Height = mapCanvas.Height;
            ScheduleFitToView();
        }

        private sealed class SubtreeHierarchyNode
        {
            public PhaDoSubtreeBranchBlock Block { get; set; }
            public int ParentId { get; set; }
            public bool IsRoot { get; set; }
        }

        private sealed class SubtreeHierarchyRow
        {
            public int Level { get; set; }
            public List<SubtreeHierarchyNode> Nodes { get; } = new List<SubtreeHierarchyNode>();
        }

        private static List<SubtreeHierarchyRow> BuildHierarchyRows(
            PhaDoSubtreeBranchBlock rootBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> subTrees)
        {
            int rootGeneration = Math.Max(1, rootBlock?.Generation ?? 1);
            int rootId = rootBlock?.FamilyId ?? 0;
            var rows = new Dictionary<int, SubtreeHierarchyRow>();

            var rootRow = new SubtreeHierarchyRow { Level = rootGeneration };
            rootRow.Nodes.Add(new SubtreeHierarchyNode
            {
                Block = rootBlock,
                ParentId = 0,
                IsRoot = true
            });
            rows[rootGeneration] = rootRow;

            var candidates = (subTrees ?? Array.Empty<PhaDoSubtreeBranchBlock>())
                .Where(s => s != null && s.FamilyId > 0)
                .OrderBy(s => s.Generation <= 0 ? int.MaxValue : s.Generation)
                .ThenBy(s => (s.MinXmm + s.MaxXmm) / 2.0)
                .ToList();

            foreach (var block in candidates)
            {
                int level = block.Generation > 0 ? block.Generation : rootGeneration + 1;
                if (!rows.TryGetValue(level, out var row))
                {
                    row = new SubtreeHierarchyRow { Level = level };
                    rows[level] = row;
                }

                int parentId = ResolveParentByNearestCenterX(block, rows, level - 1, rootId);
                row.Nodes.Add(new SubtreeHierarchyNode
                {
                    Block = block,
                    ParentId = parentId,
                    IsRoot = false
                });
            }

            return rows.Values
                .OrderBy(r => r.Level)
                .Select(r =>
                {
                    r.Nodes.Sort((a, b) =>
                    {
                        double ax = (a.Block.MinXmm + a.Block.MaxXmm) / 2.0;
                        double bx = (b.Block.MinXmm + b.Block.MaxXmm) / 2.0;
                        return ax.CompareTo(bx);
                    });
                    return r;
                })
                .ToList();
        }

        private static int ResolveParentByNearestCenterX(
            PhaDoSubtreeBranchBlock block,
            Dictionary<int, SubtreeHierarchyRow> rows,
            int expectedParentLevel,
            int fallbackRootId)
        {
            if (!rows.TryGetValue(expectedParentLevel, out var parentRow) || parentRow.Nodes.Count == 0)
            {
                return fallbackRootId;
            }

            double cx = (block.MinXmm + block.MaxXmm) / 2.0;
            var best = parentRow.Nodes
                .OrderBy(n => Math.Abs(((n.Block.MinXmm + n.Block.MaxXmm) / 2.0) - cx))
                .FirstOrDefault();
            return best?.Block?.FamilyId ?? fallbackRootId;
        }

        private void DrawSubtreeHierarchyBox(
            double x,
            double y,
            double wPx,
            double hPx,
            PhaDoSubtreeBranchBlock block,
            bool isRoot)
        {
            int generation = block?.Generation ?? 1;
            Color fillColor = BuildGenerationFillColor(generation, isCurrent: isRoot);
            Color borderColor = BuildGenerationBorderColor(generation, isCurrent: isRoot);

            var rect = new Rectangle
            {
                Width = wPx,
                Height = hPx,
                Stroke = new SolidColorBrush(borderColor),
                StrokeThickness = isRoot ? 2.8 : 2.0,
                RadiusX = 6,
                RadiusY = 6,
                Fill = new SolidColorBrush(fillColor)
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            Panel.SetZIndex(rect, 1);
            mapCanvas.Children.Add(rect);

            string familyName = !string.IsNullOrWhiteSpace(block?.FamilyName)
                ? block.FamilyName
                : (block?.MainPersonName ?? "Gia đình");
            string line1 = "Phả con: " + familyName;
            string line2 = "Bắt đầu đời thứ: " + (block?.Generation > 0 ? block.Generation.ToString() : "?");
            string line3 = "Tổng số gia đình: " + (block?.NodeCount ?? 0);
            string line4 = "Kích thước: " + (block?.WidthCm ?? 0).ToString("0.#")
                + " cm x " + (block?.HeightCm ?? 0).ToString("0.#") + " cm";
            var nameTb = new TextBlock
            {
                Text = line1,
                FontSize = isRoot ? 14.2 : 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(80, wPx - 14)
            };
            Canvas.SetLeft(nameTb, x + 7);
            Canvas.SetTop(nameTb, y + 7);
            Panel.SetZIndex(nameTb, 2);
            mapCanvas.Children.Add(nameTb);

            var genTb = new TextBlock
            {
                Text = line2,
                FontSize = 12.2,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(80, wPx - 14)
            };
            Canvas.SetLeft(genTb, x + 7);
            Canvas.SetTop(genTb, y + 31);
            Panel.SetZIndex(genTb, 2);
            mapCanvas.Children.Add(genTb);

            var countTb = new TextBlock
            {
                Text = line3,
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.SlateGray,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(80, wPx - 14)
            };
            Canvas.SetLeft(countTb, x + 7);
            Canvas.SetTop(countTb, y + 52);
            Panel.SetZIndex(countTb, 2);
            mapCanvas.Children.Add(countTb);

            var sizeTb = new TextBlock
            {
                Text = line4,
                FontSize = 11.3,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.SlateGray,
                TextWrapping = TextWrapping.Wrap,
                Width = Math.Max(80, wPx - 14)
            };
            Canvas.SetLeft(sizeTb, x + 7);
            Canvas.SetTop(sizeTb, y + 72);
            Panel.SetZIndex(sizeTb, 2);
            mapCanvas.Children.Add(sizeTb);
        }

        // Riêng flow phả con cấp cây: width chỉ cần vừa "Tên người" + "Đời thứ".
        private (double Width, double Height) CalculateHierarchyBoxSize(
            PhaDoSubtreeBranchBlock block,
            double pxPerCm,
            double titleFontSize,
            double generationFontSize)
        {
            string familyName = !string.IsNullOrWhiteSpace(block?.FamilyName)
                ? block.FamilyName
                : (block?.MainPersonName ?? "Gia đình");
            string line1 = "Phả con: " + familyName;
            string line2 = "Bắt đầu đời thứ: " + (block?.Generation > 0 ? block.Generation.ToString() : "?");
            string line3 = "Tổng số gia đình: " + (block?.NodeCount ?? 0);
            string line4 = "Kích thước: " + (block?.WidthCm ?? 0).ToString("0.#")
                + " cm x " + (block?.HeightCm ?? 0).ToString("0.#") + " cm";

            // Flow này ưu tiên box gọn: width theo text, không scale theo WidthCm của layout.
            double w1 = Math.Max(1, (line1 ?? "").Trim().Length) * titleFontSize * 0.46;
            double w2 = Math.Max(1, (line2 ?? "").Trim().Length) * generationFontSize * 0.48;
            double w3 = Math.Max(1, (line3 ?? "").Trim().Length) * (generationFontSize - 0.5) * 0.49;
            double w4 = Math.Max(1, (line4 ?? "").Trim().Length) * (generationFontSize - 1) * 0.49;
            double textWidth = Math.Max(Math.Max(w1, w2), Math.Max(w3, w4)) + 20;
            double width = Math.Max(150, Math.Min(340, textWidth));
            if ((line1 ?? "").Length >= 34)
            {
                width = Math.Min(380, Math.Max(width, 220));
            }

            double innerW = Math.Max(80, width - 16);
            double textHeight = EstimateWrappedHeight(line1, titleFontSize, innerW)
                + EstimateWrappedHeight(line2, generationFontSize, innerW)
                + EstimateWrappedHeight(line3, generationFontSize - 0.5, innerW)
                + EstimateWrappedHeight(line4, generationFontSize - 1, innerW)
                + 18;
            double height = Math.Max(96, Math.Min(150, textHeight));
            return (width, height);
        }

        private double ComputePxPerCm(
            PhaDoSubtreeBranchBlock root,
            IReadOnlyList<PhaDoSubtreeBranchBlock> children,
            double padPx,
            double hGapPx)
        {
            double maxWcm = root?.WidthCm ?? 1;
            double maxHcm = root?.HeightCm ?? 1;
            foreach (var c in children)
            {
                maxWcm = Math.Max(maxWcm, c.WidthCm);
                maxHcm = Math.Max(maxHcm, c.HeightCm);
            }

            double totalWcm = children.Sum(c => Math.Max(c.WidthCm, 1))
                + Math.Max(0, children.Count - 1) * (hGapPx / BasePxPerCm);
            totalWcm = Math.Max(totalWcm, maxWcm);

            double pxPerCmW = (900 - padPx * 2) / Math.Max(totalWcm, 1);
            double pxPerCmH = (500 - padPx * 2) / (Math.Max(root?.HeightCm ?? 1, maxHcm) + 8);
            double scale = Math.Min(BasePxPerCm, Math.Min(pxPerCmW, pxPerCmH));
            return Math.Max(2.5, scale);
        }

        private void DrawConnector(double x1, double y1, double x2, double y2)
        {
            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = Brushes.Gray,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            Panel.SetZIndex(line, 0);
            mapCanvas.Children.Add(line);
        }

        private void DrawBranchBlock(
            double x,
            double y,
            double wPx,
            double hPx,
            PhaDoSubtreeBranchBlock block,
            Brush border,
            bool isRoot)
        {
            Color fill = BuildGenerationFillColor(block?.Generation ?? 1, isCurrent: false);
            DrawHorizontalInfoBlock(
                x, y, wPx, hPx, block, border, new SolidColorBrush(fill),
                strokeThickness: isRoot ? 2.6 : 2.1,
                contentFontSize: isRoot ? 14 : 13);
        }

        private void RenderFocusedChainMap(
            PhaDoSubtreeBranchBlock parentBlock,
            PhaDoSubtreeBranchBlock currentBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> childBlocks)
        {
            mapCanvas.Children.Clear();
            parentBlock = parentBlock ?? currentBlock ?? new PhaDoSubtreeBranchBlock { MainPersonName = "Gia đình" };
            currentBlock = currentBlock ?? parentBlock;
            childBlocks = childBlocks ?? Array.Empty<PhaDoSubtreeBranchBlock>();

            summaryText.Text = "Chuỗi nhánh: Cha -> Hiện tại -> Con trực tiếp (" + childBlocks.Count + " gia đình con)";

            const double padPx = 24;
            const double rowGapPx = 44;
            const double hGapPx = 16;
            const double connectorStubPx = 20;

            // Ưu tiên tỉ lệ gần phả đồ: chiều cao vừa phải, giữ được sự khác biệt kích thước theo cm.
            var all = new List<PhaDoSubtreeBranchBlock> { parentBlock, currentBlock };
            all.AddRange(childBlocks);
            double pxPerCm = ComputePxPerCm(parentBlock, all, padPx, hGapPx);

            var parentSize = CalculateBlockSize(parentBlock, pxPerCm, contentFontSize: 13.5);
            var currentSize = CalculateBlockSize(currentBlock, pxPerCm, contentFontSize: 15);
            double parentW = parentSize.Width;
            double parentH = parentSize.Height;
            double currentW = currentSize.Width;
            double currentH = currentSize.Height;

            var childLayouts = childBlocks
                .Select(c => (
                    Block: c,
                    Size: CalculateBlockSize(c, pxPerCm, contentFontSize: 13.2)))
                .ToList();

            double childRowW = childLayouts.Count > 0
                ? childLayouts.Sum(c => c.Size.Width) + hGapPx * (childLayouts.Count - 1)
                : 0;
            double canvasW = Math.Max(Math.Max(parentW, currentW), childRowW) + padPx * 2;
            double childRowH = childLayouts.Count > 0 ? childLayouts.Max(c => c.Size.Height) : 0;
            double canvasH = padPx + parentH + rowGapPx + currentH
                + (childLayouts.Count > 0 ? rowGapPx + childRowH : 0) + padPx;

            mapCanvas.Width = canvasW;
            mapCanvas.Height = canvasH;

            double parentX = (canvasW - parentW) / 2.0;
            double parentY = padPx;
            double parentCx = parentX + parentW / 2.0;

            double currentX = (canvasW - currentW) / 2.0;
            double currentY = parentY + parentH + rowGapPx;
            double currentCx = currentX + currentW / 2.0;

            DrawConnector(parentCx, parentY + parentH, currentCx, currentY - connectorStubPx / 2.0);

            if (childLayouts.Count > 0)
            {
                double childY = currentY + currentH + rowGapPx;
                double rowStartX = (canvasW - childRowW) / 2.0;
                double busY = childY - connectorStubPx;
                var childCenters = new List<double>();
                double x = rowStartX;
                foreach (var child in childLayouts)
                {
                    childCenters.Add(x + child.Size.Width / 2.0);
                    x += child.Size.Width + hGapPx;
                }

                DrawConnector(currentCx, currentY + currentH, currentCx, busY);
                if (childCenters.Count > 1)
                {
                    DrawConnector(childCenters.First(), busY, childCenters.Last(), busY);
                }

                x = rowStartX;
                for (int i = 0; i < childLayouts.Count; i++)
                {
                    var child = childLayouts[i];
                    double cx = childCenters[i];
                    DrawConnector(cx, busY, cx, childY);
                    DrawBranchBlockEx(x, childY, child.Size.Width, child.Size.Height, child.Block, isCurrent: false, isParent: false);
                    x += child.Size.Width + hGapPx;
                }
            }

            DrawBranchBlockEx(parentX, parentY, parentW, parentH, parentBlock, isCurrent: false, isParent: true);
            DrawBranchBlockEx(currentX, currentY, currentW, currentH, currentBlock, isCurrent: true, isParent: false);

            mapHost.Width = mapCanvas.Width;
            mapHost.Height = mapCanvas.Height;
            ScheduleFitToView();
        }

        private void DrawBranchBlockEx(
            double x,
            double y,
            double wPx,
            double hPx,
            PhaDoSubtreeBranchBlock block,
            bool isCurrent,
            bool isParent)
        {
            Color fill = BuildGenerationFillColor(block?.Generation ?? 1, isCurrent);
            Brush border = new SolidColorBrush(BuildGenerationBorderColor(block?.Generation ?? 1, isCurrent));
            DrawHorizontalInfoBlock(
                x, y, wPx, hPx, block, border, new SolidColorBrush(fill),
                strokeThickness: isCurrent ? 3.0 : 2.2,
                contentFontSize: isCurrent ? 15 : 13.5);
        }

        private void DrawHorizontalInfoBlock(
            double x,
            double y,
            double wPx,
            double hPx,
            PhaDoSubtreeBranchBlock block,
            Brush border,
            Brush fill,
            double strokeThickness,
            double contentFontSize)
        {
            var rect = new Rectangle
            {
                Width = wPx,
                Height = hPx,
                Stroke = border,
                StrokeThickness = strokeThickness,
                RadiusX = 6,
                RadiusY = 6,
                Fill = fill
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            Panel.SetZIndex(rect, 1);
            mapCanvas.Children.Add(rect);

            // Box ngang: đời/chính/phối rõ ràng để đọc nhanh khi phả con ít.
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = Math.Max(80, wPx - 14)
            };
            panel.Children.Add(BuildInfoLine("", block?.GenerationText ?? "Đời ?",
                contentFontSize, FontWeights.Bold));
            panel.Children.Add(BuildInfoLine("★", block?.MainPersonName ?? "Chưa rõ",
                contentFontSize, FontWeights.SemiBold));
            panel.Children.Add(BuildInfoLine("♀ ", string.IsNullOrWhiteSpace(block?.SpouseNamesText) ? "(không)" : block.SpouseNamesText,
                contentFontSize - 0.5, FontWeights.Normal));

            //var footer = new TextBlock
            //{
            //    Text = (block?.NodeCount ?? 0) + " GD  ·  " + (block?.SizeText ?? ""),
            //    Foreground = Brushes.DimGray,
            //    FontSize = Math.Max(10, contentFontSize - 2),
            //    Margin = new Thickness(0, 4, 0, 0),
            //    TextWrapping = TextWrapping.Wrap
            //};
            //panel.Children.Add(footer);

            Canvas.SetLeft(panel, x + 7);
            Canvas.SetTop(panel, y + 6);
            Panel.SetZIndex(panel, 2);
            mapCanvas.Children.Add(panel);
        }

        private (double Width, double Height) CalculateBlockSize(
            PhaDoSubtreeBranchBlock block,
            double pxPerCm,
            double contentFontSize)
        {
            double baseWidth = Math.Max(MinBlockWidthPx, (block?.WidthCm ?? 0) * pxPerCm);
            double baseHeight = Math.Max(MinBlockHeightPx, (block?.HeightCm ?? 0) * pxPerCm);

            // Đo gần đúng theo text để tránh tràn khi tên/phối dài.
            string line1 = "" + (block?.GenerationText ?? "Đời ?");
            // Gắn icon ngôi sao vào trước tên người chính nếu là trưởng CN hiện tại
            string line2 = ("★ ") + (block?.MainPersonName ?? "Chưa rõ");
    
            // Hiển thị icon ♀ nếu block là nữ chính (dành cho trường hợp người chính là nữ hoặc block.IsMainFemale == true)
            string spouseText = string.IsNullOrWhiteSpace(block?.SpouseNamesText) ? "(không)" : block.SpouseNamesText;
            string nuIcon = "♀ ";
            string line3 = nuIcon + spouseText;
    
            string line4 = "";// (block?.NodeCount ?? 0) + " GD  ·  " + (block?.SizeText ?? "");

            double maxLineW = Math.Max(
                Math.Max(EstimateLineWidth(line1, contentFontSize), EstimateLineWidth(line2, contentFontSize)),
                Math.Max(EstimateLineWidth(line3, contentFontSize - 0.5), EstimateLineWidth(line4, Math.Max(10, contentFontSize - 2))));
            double textDrivenWidth = Math.Min(640, Math.Max(MinBlockWidthPx, maxLineW + 28));
            double width = Math.Max(baseWidth, textDrivenWidth);
            double innerWidth = Math.Max(80, width - 16);

            double textHeight =
                EstimateWrappedHeight(line1, contentFontSize, innerWidth) +
                EstimateWrappedHeight(line2, contentFontSize, innerWidth) +
                EstimateWrappedHeight(line3, contentFontSize - 0.5, innerWidth) +
                EstimateWrappedHeight(line4, Math.Max(10, contentFontSize - 2), innerWidth) + 16;
            double height = Math.Max(baseHeight, textHeight);
            return (width, height);
        }

        private static double EstimateLineWidth(string text, double fontSize)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return text.Trim().Length * fontSize * 0.58;
        }

        private static double EstimateWrappedHeight(string text, double fontSize, double width)
        {
            if (fontSize <= 0)
            {
                fontSize = 12;
            }

            double lineWidth = Math.Max(1, EstimateLineWidth(text, fontSize));
            int lines = Math.Max(1, (int)Math.Ceiling(lineWidth / Math.Max(1, width)));
            return lines * (fontSize * 1.35);
        }

        // Màu theo đời để nhìn tuyến hệ rõ hơn, đời khác nhau tông khác nhau.
        private static Color BuildGenerationFillColor(int generation, bool isCurrent)
        {
            var baseColor = BuildGenerationBaseColor(generation);
            byte alpha = (byte)(isCurrent ? 52 : 34);
            return Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        }

        private static Color BuildGenerationBorderColor(int generation, bool isCurrent)
        {
            var c = BuildGenerationBaseColor(generation);
            double k = isCurrent ? 0.64 : 0.72;
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Min(255, c.R * k)),
                (byte)Math.Max(0, Math.Min(255, c.G * k)),
                (byte)Math.Max(0, Math.Min(255, c.B * k)));
        }

        private static Color BuildGenerationBaseColor(int generation)
        {
            int g = Math.Max(1, generation);
            double hue = (g * 47) % 360;
            return ColorFromHsl(hue, 0.58, 0.60);
        }

        private static Color ColorFromHsl(double h, double s, double l)
        {
            h = ((h % 360) + 360) % 360;
            s = Math.Max(0, Math.Min(1, s));
            l = Math.Max(0, Math.Min(1, l));

            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
            double m = l - c / 2;
            double r1, g1, b1;
            if (h < 60) { r1 = c; g1 = x; b1 = 0; }
            else if (h < 120) { r1 = x; g1 = c; b1 = 0; }
            else if (h < 180) { r1 = 0; g1 = c; b1 = x; }
            else if (h < 240) { r1 = 0; g1 = x; b1 = c; }
            else if (h < 300) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            return Color.FromRgb(
                (byte)Math.Round((r1 + m) * 255),
                (byte)Math.Round((g1 + m) * 255),
                (byte)Math.Round((b1 + m) * 255));
        }

        private static TextBlock BuildInfoLine(
            string label,
            string value,
            double fontSize,
            FontWeight valueWeight)
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 1, 0, 1),
                FontSize = fontSize
            };
            tb.Inlines.Add(new Run(label + " ")
            {
                Foreground = Brushes.MidnightBlue,
                FontWeight = FontWeights.Bold
            });
            tb.Inlines.Add(new Run(value ?? "")
            {
                Foreground = Brushes.Black,
                FontWeight = valueWeight
            });
            return tb;
        }

        private void ScheduleFitToView()
        {
            _pendingFitToView = true;
            Dispatcher.BeginInvoke(new Action(ApplyFitToView), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void MapScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_pendingFitToView)
            {
                ApplyFitToView();
            }
        }

        private void FitView_Click(object sender, RoutedEventArgs e)
        {
            _pendingFitToView = true;
            ApplyFitToView();
        }

        private void Zoom100_Click(object sender, RoutedEventArgs e)
        {
            _pendingFitToView = false;
            mapScale.ScaleX = 1;
            mapScale.ScaleY = 1;
            UpdateZoomLabel();
        }

        private void ToggleFullScreen_Click(object sender, RoutedEventArgs e)
        {
            _isFullScreen = !_isFullScreen;
            if (_isFullScreen)
            {
                WindowState = WindowState.Maximized;
                if (fullScreenBtn != null)
                {
                    fullScreenBtn.Content = "Thoát full";
                }
                return;
            }

            WindowState = WindowState.Normal;
            if (fullScreenBtn != null)
            {
                fullScreenBtn.Content = "Toàn màn hình";
            }
        }

        private void ZoomIn_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoomStep(0.12);
        }

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
        {
            ApplyZoomStep(-0.12);
        }

        private void MapScroll_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (mapScale == null)
            {
                return;
            }

            // Zoom bằng con lăn để xem nhanh từng nhánh phả con.
            const double step = 0.10;
            const double minZoom = 0.05;
            const double maxZoom = 4.0;
            double next = mapScale.ScaleX + (e.Delta > 0 ? step : -step);
            next = Math.Max(minZoom, Math.Min(maxZoom, next));

            _pendingFitToView = false;
            mapScale.ScaleX = next;
            mapScale.ScaleY = next;
            UpdateZoomLabel();
            e.Handled = true;
        }

        private void ApplyZoomStep(double delta)
        {
            if (mapScale == null)
            {
                return;
            }

            const double minZoom = 0.05;
            const double maxZoom = 4.0;
            double next = mapScale.ScaleX + delta;
            next = Math.Max(minZoom, Math.Min(maxZoom, next));

            _pendingFitToView = false;
            mapScale.ScaleX = next;
            mapScale.ScaleY = next;
            UpdateZoomLabel();
        }

        private void ApplyFitToView()
        {
            if (mapCanvas == null || mapScroll == null || mapScale == null)
            {
                return;
            }

            mapHost.UpdateLayout();
            mapScroll.UpdateLayout();

            double contentW = mapCanvas.Width;
            double contentH = mapCanvas.Height;
            if (contentW <= 0 || contentH <= 0)
            {
                return;
            }

            double vw = mapScroll.ViewportWidth;
            double vh = mapScroll.ViewportHeight;
            if (vw <= 0 || vh <= 0)
            {
                return;
            }

            const double margin = 0.92;
            double scale = Math.Min(vw / contentW, vh / contentH) * margin;
            scale = Math.Min(1.0, scale);
            if (scale < 0.05)
            {
                scale = 0.05;
            }

            mapScale.ScaleX = scale;
            mapScale.ScaleY = scale;
            _pendingFitToView = false;
            UpdateZoomLabel();
        }

        private void UpdateZoomLabel()
        {
            if (zoomLabel == null)
            {
                return;
            }

            zoomLabel.Text = ((int)Math.Round(mapScale.ScaleX * 100)) + "%";
        }
    }
}
