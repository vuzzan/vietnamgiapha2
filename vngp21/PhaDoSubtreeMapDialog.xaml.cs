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
            SetAnalysisDocument(analysisReport ?? "");
            RenderMap(rootBlock, subTrees, rootLevelMax, splitLevel);
        }

        /// <summary>Render nhanh 3 tầng: cha -> gia đình hiện tại -> các gia đình con trực tiếp.</summary>
        public void SetFocusedChainContent(
            string analysisReport,
            PhaDoSubtreeBranchBlock parentBlock,
            PhaDoSubtreeBranchBlock currentBlock,
            IReadOnlyList<PhaDoSubtreeBranchBlock> childBlocks)
        {
            SetAnalysisDocument(analysisReport ?? "");
            RenderFocusedChainMap(parentBlock, currentBlock, childBlocks ?? Array.Empty<PhaDoSubtreeBranchBlock>());
        }

        // ── Hiển thị báo cáo phân tích ────────────────────────────────────────

        private void SetAnalysisDocument(string text)
        {
            if (analysisViewer == null) return;
            analysisViewer.Document = BuildReportDocument(text);
        }

        /// <summary>
        /// Chuyển plain text báo cáo phân tích thành FlowDocument có định dạng:
        /// tiêu đề mục (màu xanh), key:value (bold key), bullet ▸, combo [N] (nền xanh),
        /// indent chi tiết → , separator ─────.
        /// </summary>
        private static FlowDocument BuildReportDocument(string plainText)
        {
            var doc = new FlowDocument
            {
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 12,
                Foreground  = Brushes.Black,
                LineHeight  = 20,
                PagePadding = new Thickness(10, 8, 10, 12)
            };

            if (string.IsNullOrWhiteSpace(plainText))
                return doc;

            // ── Màu palette ──
            var accentOrange  = new SolidColorBrush(Color.FromRgb(196, 92, 0));
            var sectionFore   = new SolidColorBrush(Color.FromRgb(25, 80, 155));
            var sectionBg     = new SolidColorBrush(Color.FromArgb(15, 25, 80, 200));
            var sectionBorder = new SolidColorBrush(Color.FromRgb(60, 120, 200));
            var keyFore       = new SolidColorBrush(Color.FromRgb(35, 35, 35));
            var arrowFore     = new SolidColorBrush(Color.FromRgb(155, 65, 0));
            var subNoteFore   = new SolidColorBrush(Color.FromRgb(110, 110, 110));
            var comboGreen    = new SolidColorBrush(Color.FromRgb(20, 100, 20));
            var comboBg       = new SolidColorBrush(Color.FromArgb(18, 20, 140, 20));
            var bigHeaderBg   = new SolidColorBrush(Color.FromRgb(30, 90, 160));
            var importantFore = new SolidColorBrush(Color.FromRgb(160, 60, 0));

            string[] lines = plainText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (string rawLine in lines)
            {
                string line    = rawLine.TrimEnd();
                string trimmed = line.Trim();

                // Dòng trống → khoảng cách nhỏ
                if (string.IsNullOrWhiteSpace(line))
                {
                    doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 2, 0, 2) });
                    continue;
                }

                // ── Dải separator (═══ / ─── / ===) ──
                if (IsReportSeparator(line))
                {
                    // Paragraph rỗng với border-top thay cho HR — hoạt động ổn trong FlowDocument
                    doc.Blocks.Add(new Paragraph
                    {
                        BorderBrush     = new SolidColorBrush(Color.FromRgb(170, 180, 210)),
                        BorderThickness = new Thickness(0, 1.5, 0, 0),
                        Margin          = new Thickness(0, 6, 0, 4),
                        Padding         = new Thickness(0, 4, 0, 0)
                    });
                    continue;
                }

                // ── Tiêu đề lớn KẾ HOẠCH VẼ ──
                if (trimmed.StartsWith("KẾ HOẠCH") || trimmed.StartsWith("KE HOACH"))
                {
                    var p = new Paragraph
                    {
                        Margin    = new Thickness(0, 8, 0, 6),
                        Padding   = new Thickness(10, 6, 10, 6),
                        Background = bigHeaderBg
                    };
                    p.Inlines.Add(new Run("📋  " + trimmed)
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize   = 13,
                        Foreground = Brushes.White
                    });
                    doc.Blocks.Add(p);
                    continue;
                }

                // ── Combo item [N] title ──
                if (trimmed.StartsWith("[") && trimmed.Contains("]"))
                {
                    int closeIdx  = trimmed.IndexOf(']');
                    string idxPart   = trimmed.Substring(0, closeIdx + 1);
                    string titlePart = trimmed.Substring(closeIdx + 1).Trim();
                    var p = new Paragraph
                    {
                        Margin          = new Thickness(0, 5, 0, 1),
                        Padding         = new Thickness(7, 4, 7, 4),
                        Background      = comboBg,
                        BorderBrush     = comboGreen,
                        BorderThickness = new Thickness(4, 0, 0, 0)
                    };
                    p.Inlines.Add(new Run(idxPart + " ")
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = comboGreen,
                        FontFamily = new FontFamily("Consolas, Courier New")
                    });
                    p.Inlines.Add(new Run(titlePart)
                    {
                        FontWeight = FontWeights.SemiBold,
                        Foreground = comboGreen
                    });
                    doc.Blocks.Add(p);
                    continue;
                }

                // ── Indent 4 spaces: chi tiết arrow / label (trong mục combo) ──
                if (line.StartsWith("    "))
                {
                    string inner    = line.Substring(4).TrimStart();
                    bool   hasArrow = inner.StartsWith("→");
                    var p = new Paragraph { Margin = new Thickness(18, 0, 0, 1) };
                    if (hasArrow)
                    {
                        p.Inlines.Add(new Run(" → ")
                            { Foreground = arrowFore, FontWeight = FontWeights.SemiBold });
                        string rest = inner.Length > 1 ? inner.Substring(1).TrimStart() : "";
                        p.Inlines.Add(new Run(rest) { Foreground = subNoteFore, FontSize = 11.5 });
                    }
                    else
                    {
                        p.Inlines.Add(new Run(inner)
                            { Foreground = subNoteFore, FontStyle = FontStyles.Italic, FontSize = 11.5 });
                    }
                    doc.Blocks.Add(p);
                    continue;
                }

                // ── Indent 2 spaces: ghi chú phụ ──
                if (line.StartsWith("  ") && !line.StartsWith("    "))
                {
                    var p = new Paragraph { Margin = new Thickness(8, 0, 0, 1) };
                    p.Inlines.Add(new Run(trimmed)
                        { Foreground = subNoteFore, FontStyle = FontStyles.Italic, FontSize = 11 });
                    doc.Blocks.Add(p);
                    continue;
                }

                // ── Bullet line "- ..." ──
                if (trimmed.StartsWith("- "))
                {
                    string body = trimmed.Substring(2).TrimStart();
                    var p = new Paragraph { Margin = new Thickness(8, 1, 0, 1) };
                    p.Inlines.Add(new Run("▸ ") { Foreground = accentOrange, FontWeight = FontWeights.Bold });
                    AddTextInlines(p, body, keyFore, arrowFore, importantFore);
                    doc.Blocks.Add(p);
                    continue;
                }

                // ── Section header: không thụt đầu, kết thúc ':' ──
                if (!line.StartsWith(" ") && !trimmed.StartsWith("-") && !trimmed.StartsWith("[")
                    && trimmed.EndsWith(":") && !trimmed.Contains(" → "))
                {
                    var p = new Paragraph
                    {
                        Margin          = new Thickness(0, 10, 0, 3),
                        Padding         = new Thickness(8, 3, 4, 3),
                        BorderBrush     = sectionBorder,
                        BorderThickness = new Thickness(4, 0, 0, 0),
                        Background      = sectionBg
                    };
                    p.Inlines.Add(new Run(trimmed.TrimEnd(':'))
                    {
                        FontWeight = FontWeights.SemiBold,
                        FontSize   = 12.5,
                        Foreground = sectionFore
                    });
                    doc.Blocks.Add(p);
                    continue;
                }

                // ── Dòng thường ──
                {
                    var p = new Paragraph { Margin = new Thickness(0, 1, 0, 1) };
                    AddTextInlines(p, trimmed, keyFore, arrowFore, importantFore);
                    doc.Blocks.Add(p);
                }
            }

            return doc;
        }

        /// <summary>Kiểm tra dòng là separator thuần (tất cả ký tự ═ ─ = hoặc space).</summary>
        private static bool IsReportSeparator(string s)
        {
            bool hasDelimChar = false;
            foreach (char c in s)
            {
                if (c == '═' || c == '─' || c == '=') { hasDelimChar = true; }
                else if (c != ' ') { return false; }
            }
            return hasDelimChar && s.Length >= 4;
        }

        /// <summary>
        /// Thêm inlines vào paragraph: phát hiện "key: value" và "text → result".
        /// Key quan trọng (Tổng số, Gợi ý, Đời cắt, Kích thước) hiển thị bold màu accent.
        /// </summary>
        private static void AddTextInlines(
            Paragraph p, string text,
            Brush keyFore, Brush arrowFore, Brush importantFore)
        {
            // Có mũi tên " → "
            int arrowIdx = text.IndexOf(" → ");
            if (arrowIdx > 0)
            {
                p.Inlines.Add(new Run(text.Substring(0, arrowIdx)));
                p.Inlines.Add(new Run(" → ") { Foreground = arrowFore, FontWeight = FontWeights.Bold });
                p.Inlines.Add(new Run(text.Substring(arrowIdx + 3)));
                return;
            }

            // Có dấu ": " và key không quá 70 ký tự
            int colonIdx = text.IndexOf(": ");
            if (colonIdx > 0 && colonIdx < 70)
            {
                string key = text.Substring(0, colonIdx);
                string val = text.Substring(colonIdx + 2);
                bool important = key.Contains("Tổng số") || key.Contains("Gợi ý")
                    || key.Contains("Đời cắt") || key.Contains("Kích thước");
                p.Inlines.Add(new Run(key)
                {
                    FontWeight = important ? FontWeights.Bold : FontWeights.SemiBold,
                    Foreground = important ? importantFore : keyFore
                });
                p.Inlines.Add(new Run(": "));
                p.Inlines.Add(new Run(val)
                    { FontWeight = important ? FontWeights.SemiBold : FontWeights.Normal });
                return;
            }

            // Plain text
            p.Inlines.Add(new Run(text));
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
            summaryText.Text = "(đời 1–" + rootLevelMax + ")  →  " + splitNote;

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
            const double padPx = 40;
            const double rowGapPx = 140;  // khoảng cách dọc giữa các đời
            const double hGapPx = 20;
            const double connectorStubPx = 24;

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

            // Vẽ connector cong trước để box đè lên nhìn sạch hơn.
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
                    bool childIsNonStop = node.Block != null && !node.Block.IsStop;

                    // Đường cong Bezier từ đáy cha → đỉnh con.
                    DrawCurvedConnector(pCx, pBottom, cCx, cTop, childIsNonStop);
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
                // Giữ cả STOP block (FamilyId > 0) lẫn non-STOP summary block (FamilyId <= 0 nhưng IsNonStopSummary).
                .Where(s => s != null && (s.FamilyId > 0 || s.IsNonStopSummary))
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

                // Ô tổng hợp non-STOP có SummaryParentId rõ ràng → dùng luôn, không cần tìm gần nhất.
                int parentId = block.SummaryParentId > 0
                    ? block.SummaryParentId
                    : ResolveParentByNearestCenterX(block, rows, level - 1, rootId);

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
            // Tìm row cha: ưu tiên đúng expectedParentLevel, nếu không có thì
            // lùi dần về row tổ tiên gần nhất thực sự tồn tại (< expectedParentLevel).
            // Tránh trường hợp root2 (đời 15) fallback thẳng về root0 vì đời 14 trống.
            SubtreeHierarchyRow parentRow = null;
            for (int lvl = expectedParentLevel; lvl >= 1; lvl--)
            {
                if (rows.TryGetValue(lvl, out var candidate) && candidate.Nodes.Count > 0)
                {
                    parentRow = candidate;
                    break;
                }
            }

            if (parentRow == null)
            {
                return fallbackRootId;
            }

            double cx = (block.MinXmm + block.MaxXmm) / 2.0;
            // Chỉ dùng STOP block thật (FamilyId > 0) làm cha — bỏ qua ô summary âm.
            var best = parentRow.Nodes
                .Where(n => (n.Block?.FamilyId ?? 0) > 0)
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
            bool isNonStop = block != null && !block.IsStop;

            // Màu: Root0 = xanh nhạt; STOP = cam đậm; non-STOP summary = xanh ngọc.
            Color fillColor;
            Color borderColor;
            Brush textFore;
            Brush subFore;

            if (isRoot)
            {
                fillColor = Color.FromArgb(60, 33, 150, 243);   // xanh blue nhạt
                borderColor = Color.FromRgb(25, 118, 210);
                textFore = Brushes.Black;
                subFore = Brushes.DimGray;
            }
            else if (isNonStop)
            {
                // Non-STOP summary: teal — nhánh nhỏ vẽ tiếp trong phả con cha.
                fillColor = Color.FromArgb(55, 0, 188, 212);    // cyan teal
                borderColor = Color.FromRgb(0, 151, 167);
                textFore = new SolidColorBrush(Color.FromRgb(0, 60, 70));
                subFore = new SolidColorBrush(Color.FromRgb(0, 96, 100));
            }
            else
            {
                // STOP: cam đậm — bắt đầu phả con riêng.
                fillColor = Color.FromArgb(230, 245, 124, 0);   // #F57C00 đặc
                borderColor = Color.FromRgb(230, 81, 0);
                textFore = Brushes.White;
                subFore = new SolidColorBrush(Color.FromRgb(255, 243, 224));
            }

            var rect = new Rectangle
            {
                Width = wPx,
                Height = hPx,
                Stroke = new SolidColorBrush(borderColor),
                StrokeThickness = isRoot ? 2.8 : (isNonStop ? 1.6 : 2.2),
                RadiusX = 6,
                RadiusY = 6,
                Fill = new SolidColorBrush(fillColor)
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            Panel.SetZIndex(rect, 1);
            mapCanvas.Children.Add(rect);

            double innerW = Math.Max(80, wPx - 14);
            double curY = y + 7;
            double lineSpacing = 0;

            void AddLine(string text, double fs, FontWeight fw, Brush fore)
            {
                if (string.IsNullOrWhiteSpace(text)) { return; }
                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = fs,
                    FontWeight = fw,
                    Foreground = fore,
                    TextWrapping = TextWrapping.Wrap,
                    Width = innerW
                };
                Canvas.SetLeft(tb, x + 7);
                Canvas.SetTop(tb, curY + lineSpacing);
                Panel.SetZIndex(tb, 2);
                mapCanvas.Children.Add(tb);
                lineSpacing += fs * 1.42;
            }

            if (isRoot)
            {
                string rootName = !string.IsNullOrWhiteSpace(block?.FamilyName)
                    ? block.FamilyName
                    : (block?.MainPersonName ?? "Root0");
                AddLine("★" + rootName, isRoot ? 14.2 : 13, FontWeights.Bold, textFore);
                AddLine("Đời 1–" + block?.Generation + " | " + block?.NodeCount + " GD", 12, FontWeights.Normal, subFore);
                AddLine(block?.SizeText ?? "", 11, FontWeights.Normal, subFore);
            }
            else if (isNonStop)
            {
                int grpCount = block.NonStopGroupCount > 0 ? block.NonStopGroupCount : 1;
                AddLine("◎ " + grpCount + " nhánh non-STOP (vẽ tiếp)", 12.5, FontWeights.SemiBold, textFore);
                AddLine("Đời " + block.Generation + " | " + block.NodeCount + " GD", 11.5, FontWeights.Normal, subFore);
                if (!string.IsNullOrWhiteSpace(block.MainPersonName))
                {
                    AddLine("đại diện: " + block.MainPersonName, 11, FontWeights.Normal, subFore);
                }
            }
            else
            {
                string familyName = !string.IsNullOrWhiteSpace(block?.FamilyName)
                    ? block.FamilyName
                    : (block?.MainPersonName ?? "Gia đình");
                AddLine("★ Bắt đầu phả con", 12, FontWeights.Bold, textFore);
                AddLine(familyName, 13, FontWeights.SemiBold, textFore);
                AddLine("Đời " + block?.Generation + " | " + block?.NodeCount + " GD", 11.5, FontWeights.Normal, subFore);
                AddLine(block?.SizeText ?? "", 11, FontWeights.Normal, subFore);
            }
        }

        // Tính kích thước box theo loại: root / STOP / non-STOP summary.
        private (double Width, double Height) CalculateHierarchyBoxSize(
            PhaDoSubtreeBranchBlock block,
            double pxPerCm,
            double titleFontSize,
            double generationFontSize)
        {
            List<(string text, double fs)> lines;

            if (block == null)
            {
                lines = new List<(string, double)> { ("?", titleFontSize) };
            }
            else if (!block.IsStop && block.IsNonStopSummary)
            {
                // Non-STOP summary box.
                int grpCount = block.NonStopGroupCount > 0 ? block.NonStopGroupCount : 1;
                lines = new List<(string, double)>
                {
                    ("◎ " + grpCount + " nhánh non-STOP (vẽ tiếp)", 12.5),
                    ("Đời " + block.Generation + " | " + block.NodeCount + " GD", 11.5),
                    ("đại diện: " + (block.MainPersonName ?? ""), 11)
                };
            }
            else
            {
                string familyName = !string.IsNullOrWhiteSpace(block.FamilyName)
                    ? block.FamilyName
                    : (block.MainPersonName ?? "Gia đình");
                lines = new List<(string, double)>
                {
                    ("★ Bắt đầu phả con", 12),
                    (familyName, titleFontSize),
                    ("Đời " + block.Generation + " | " + block.NodeCount + " GD", 11.5),
                    (block.SizeText ?? "", 11)
                };
            }

            double maxW = lines.Max(l => EstimateLineWidth(l.text, l.fs));
            double width = Math.Max(150, Math.Min(380, maxW + 24));
            double innerW = Math.Max(80, width - 16);
            double textH = lines.Sum(l => EstimateWrappedHeight(l.text, l.fs, innerW)) + 16;
            double height = Math.Max(88, Math.Min(160, textH));
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

        /// <summary>
        /// Vẽ đường cong Bezier bậc 3 từ (x1,y1) → (x2,y2).
        /// Control points kéo thẳng theo trục Y để tạo hình chữ S mượt.
        /// </summary>
        private void DrawCurvedConnector(double x1, double y1, double x2, double y2, bool isNonStop = false)
        {
            double dy = y2 - y1;
            double cp = Math.Abs(dy) * 0.55; // độ cong: 55% khoảng cách dọc

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(x1, y1), false, false);
                ctx.BezierTo(
                    new Point(x1, y1 + cp),   // control 1: kéo xuống từ cha
                    new Point(x2, y2 - cp),   // control 2: kéo lên đến con
                    new Point(x2, y2),
                    true, false);
            }

            geo.Freeze();
            var path = new System.Windows.Shapes.Path
            {
                Data = geo,
                Stroke = isNonStop
                    ? new SolidColorBrush(Color.FromRgb(0, 151, 167))   // teal cho non-STOP
                    : new SolidColorBrush(Color.FromRgb(120, 120, 120)), // xám cho STOP/root
                StrokeThickness = isNonStop ? 1.4 : 1.8,
                StrokeDashArray = isNonStop ? new DoubleCollection { 5, 3 } : null,
                Fill = null
            };
            Panel.SetZIndex(path, 0);
            mapCanvas.Children.Add(path);
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

            DrawCurvedConnector(parentCx, parentY + parentH, currentCx, currentY);

            if (childLayouts.Count > 0)
            {
                double childY = currentY + currentH + rowGapPx;
                double rowStartX = (canvasW - childRowW) / 2.0;
                var childCenters = new List<double>();
                double x = rowStartX;
                foreach (var child in childLayouts)
                {
                    childCenters.Add(x + child.Size.Width / 2.0);
                    x += child.Size.Width + hGapPx;
                }

                x = rowStartX;
                for (int i = 0; i < childLayouts.Count; i++)
                {
                    var child = childLayouts[i];
                    DrawCurvedConnector(currentCx, currentY + currentH, childCenters[i], childY);
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
