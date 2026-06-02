using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Vẽ kết quả layout lên WPF Canvas (preview hoặc in).</summary>
    public sealed class FamilyTreeCanvasRenderer
    {
        private readonly GiaPhaRenderResult _result;
        private readonly GiaPhaRenderOptions _options;
        private readonly double _dpi;

        public FamilyTreeCanvasRenderer(GiaPhaRenderResult result)
        {
            _result = result ?? throw new ArgumentNullException(nameof(result));
            _options = result.Options;
            _dpi = result.Dpi;
        }

        public void RenderTo(Canvas canvas)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            canvas.Children.Clear();
            canvas.Width = _result.CanvasWidthPixels > 0 ? _result.CanvasWidthPixels : _result.PageWidthPixels;
            canvas.Height = _result.CanvasHeightPixels > 0 ? _result.CanvasHeightPixels : _result.PageHeightPixels;
            canvas.Background = Brushes.White;
            canvas.SnapsToDevicePixels = true;
            canvas.ClipToBounds = false;

            DrawTitle(canvas);
            DrawGenerationBands(canvas);
            DrawCards(canvas);
            DrawConnectors(canvas);

            canvas.Measure(new Size(canvas.Width, canvas.Height));
            canvas.Arrange(new Rect(0, 0, canvas.Width, canvas.Height));
            canvas.UpdateLayout();
        }

        /// <summary>Vẽ lại một thẻ gia đình (không xóa canvas, không vẽ connector).</summary>
        public void DrawSingleCard(Canvas canvas, GiaPhaPlacedNode placed)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            if (placed == null)
            {
                throw new ArgumentNullException(nameof(placed));
            }

            DrawCard(canvas, placed);
        }

        private void DrawTitle(Canvas canvas)
        {
            double pageW = canvas.Width > 0 ? canvas.Width : Mm(_options.PageWidthMm);
            GiaPhaTitleBlockRenderer.DrawToCanvas(canvas, _options, _dpi, pageW);
        }

        private void DrawGenerationBands(Canvas canvas)
        {
            double pageW = canvas.Width > 0 ? canvas.Width : Mm(_options.PageWidthMm);
            for (int i = 0; i < _result.GenerationBands.Count; i++)
            {
                var band = _result.GenerationBands[i];
                double bandTop = Mm(band.Ymm);
                double bandH = Mm(band.HeightMm);
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = pageW,
                    Height = bandH,
                    Fill = i % 2 == 0
                        ? new SolidColorBrush(Color.FromRgb(248, 250, 252))
                        : new SolidColorBrush(Color.FromRgb(235, 241, 245)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(rect, 0);
                Canvas.SetTop(rect, bandTop);
                Panel.SetZIndex(rect, 0);
                canvas.Children.Add(rect);

                bool vertical = GiaPhaRenderOptions.IsVerticalCardLayout(_options.CardLayoutMode);
                var custom = _options.GenLabelStyle;

                double bandLabelPt = custom?.FontPt > 0
                    ? custom.FontPt
                    : (vertical ? _options.VerticalGenerationLabelFontPt : _options.HeaderFontPt);

                string fontFamily = !string.IsNullOrWhiteSpace(custom?.FontFamily)
                    ? custom.FontFamily
                    : _options.FontFamilyName;

                FontWeight fw = custom != null
                    ? (custom.Bold ? FontWeights.Bold : FontWeights.Normal)
                    : (vertical ? FontWeights.SemiBold : FontWeights.Normal);

                FontStyle fs = custom?.Italic == true ? FontStyles.Italic : FontStyles.Normal;

                Brush fg;
                if (!string.IsNullOrWhiteSpace(custom?.ForegroundHex))
                {
                    try { fg = new SolidColorBrush((Color)ColorConverter.ConvertFromString(custom.ForegroundHex)); }
                    catch { fg = new SolidColorBrush(Color.FromRgb(90, 90, 90)); }
                }
                else
                {
                    fg = vertical
                        ? new SolidColorBrush(Color.FromRgb(25, 55, 120))
                        : new SolidColorBrush(Color.FromRgb(90, 90, 90));
                }

                var labelLevelDoi = new TextBlock
                {
                    Text = "Đời " + band.Level,
                    FontFamily = new FontFamily(fontFamily),
                    FontSize = bandLabelPt * _dpi / 72.0,
                    FontWeight = fw,
                    FontStyle = fs,
                    Foreground = fg,
                    // IsHitTestVisible = true để nhận click chọn
                    IsHitTestVisible = true,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = new PhaDoGenLabelTag(band.Level)
                };
                Canvas.SetLeft(labelLevelDoi, Mm(_options.MarginMm));
                Canvas.SetTop(labelLevelDoi, Mm(band.Ymm) + 2);
                Panel.SetZIndex(labelLevelDoi, 1);
                canvas.Children.Add(labelLevelDoi);
            }
        }

        private void DrawConnectors(Canvas canvas)
        {
            var stroke = new SolidColorBrush(Color.FromRgb(35, 35, 35));
            double thickness = Math.Max(1.4, Mm(0.45));
            var byFamily = _result.Nodes
                .Where(n => n.Family != null)
                .ToDictionary(n => n.Family, n => n);

            if (_options.ConnectorPathType == GiaPhaConnectorPathType.Curved)
            {
                DrawCurvedConnectors(canvas, stroke, thickness, byFamily);
                return;
            }

            foreach (var parent in _result.Nodes.Where(n => n.Family != null))
            {
                var childNodes = parent.Family.Children
                    .Where(byFamily.ContainsKey)
                    .Select(c => byFamily[c])
                    .ToList();
                if (childNodes.Count == 0)
                {
                    continue;
                }

                int parentId = parent.Family.familyInfo?.FamilyId ?? 0;
                double parentCx = parent.Xmm + parent.Metrics.WidthMm / 2.0;
                double parentBottom = parent.Ymm + parent.Metrics.HeightMm;
                double childTop = childNodes.Min(c => c.Ymm);
                double gap = childTop - parentBottom;
                if (gap < _options.BusLineGapMm)
                {
                    gap = _options.BusLineGapMm;
                }
                double busY = parentBottom + gap * 0.5;

                AddConnectorLine(
                    canvas,
                    parentCx, parentBottom, parentCx, busY,
                    thickness, stroke,
                    new GiaPhaCanvasConnectorTag
                    {
                        ParentFamilyId = parentId,
                        ChildFamilyId = 0,
                        LineKind = GiaPhaCanvasConnectorLineKind.Trunk
                    });

                double busLeft = childNodes.Min(c => c.Xmm + c.Metrics.WidthMm / 2.0);
                double busRight = childNodes.Max(c => c.Xmm + c.Metrics.WidthMm / 2.0);
                busLeft = Math.Min(busLeft, parentCx);
                busRight = Math.Max(busRight, parentCx);
                double span = busRight - busLeft;
                if (span < _options.MinBusSpanMm)
                {
                    double mid = (busLeft + busRight) / 2.0;
                    busLeft = mid - _options.MinBusSpanMm / 2.0;
                    busRight = mid + _options.MinBusSpanMm / 2.0;
                }

                AddConnectorLine(
                    canvas,
                    busLeft, busY, busRight, busY,
                    thickness + 0.3, stroke,
                    new GiaPhaCanvasConnectorTag
                    {
                        ParentFamilyId = parentId,
                        ChildFamilyId = 0,
                        LineKind = GiaPhaCanvasConnectorLineKind.Bus
                    });

                foreach (var child in childNodes)
                {
                    int childId = child.Family?.familyInfo?.FamilyId ?? 0;
                    double childCx = child.Xmm + child.Metrics.WidthMm / 2.0;
                    AddConnectorLine(
                        canvas,
                        childCx, busY, childCx, child.Ymm,
                        thickness, stroke,
                        new GiaPhaCanvasConnectorTag
                        {
                            ParentFamilyId = parentId,
                            ChildFamilyId = childId,
                            LineKind = GiaPhaCanvasConnectorLineKind.Branch
                        });
                }
            }
        }

        /// <summary>Path type 2: dùng đường cong cha-con, bỏ bus/trunk kiểu cũ.</summary>
        private void DrawCurvedConnectors(
            Canvas canvas,
            Brush stroke,
            double thickness,
            Dictionary<FamilyViewModel, GiaPhaPlacedNode> byFamily)
        {
            foreach (var parent in _result.Nodes.Where(n => n.Family != null))
            {
                var childNodes = parent.Family.Children
                    .Where(byFamily.ContainsKey)
                    .Select(c => byFamily[c])
                    .ToList();
                if (childNodes.Count == 0)
                {
                    continue;
                }

                int parentId = parent.Family.familyInfo?.FamilyId ?? 0;
                double parentCx = parent.Xmm + parent.Metrics.WidthMm / 2.0;
                double parentBottom = parent.Ymm + parent.Metrics.HeightMm;
                foreach (var child in childNodes)
                {
                    int childId = child.Family?.familyInfo?.FamilyId ?? 0;
                    double childCx = child.Xmm + child.Metrics.WidthMm / 2.0;
                    double childTop = child.Ymm;
                    AddConnectorCurve(
                        canvas,
                        parentCx, parentBottom,
                        childCx, childTop,
                        thickness,
                        stroke,
                        new GiaPhaCanvasConnectorTag
                        {
                            ParentFamilyId = parentId,
                            ChildFamilyId = childId,
                            LineKind = GiaPhaCanvasConnectorLineKind.CurvedBranch
                        });
                }
            }
        }

        private void DrawCards(Canvas canvas)
        {
            foreach (var placed in _result.Nodes)
            {
                DrawCard(canvas, placed);
            }
        }

        private void AddConnectorLine(
            Canvas canvas,
            double x1mm,
            double y1mm,
            double x2mm,
            double y2mm,
            double strokeThicknessPx,
            Brush stroke,
            object tag)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = Mm(x1mm),
                Y1 = Mm(y1mm),
                X2 = Mm(x2mm),
                Y2 = Mm(y2mm),
                Stroke = stroke,
                StrokeThickness = strokeThicknessPx,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false,
                Tag = tag
            };
            Panel.SetZIndex(line, 25);
            canvas.Children.Add(line);
        }

        private void AddConnectorCurve(
            Canvas canvas,
            double x1mm,
            double y1mm,
            double x2mm,
            double y2mm,
            double strokeThicknessPx,
            Brush stroke,
            object tag)
        {
            double x1 = Mm(x1mm);
            double y1 = Mm(y1mm);
            double x2 = Mm(x2mm);
            double y2 = Mm(y2mm);
            double midY = (y1 + y2) / 2.0;
            var figure = new PathFigure { StartPoint = new Point(x1, y1), IsFilled = false, IsClosed = false };
            figure.Segments.Add(new BezierSegment(
                new Point(x1, midY),
                new Point(x2, midY),
                new Point(x2, y2),
                true));
            var path = new Path
            {
                Data = new PathGeometry(new[] { figure }),
                Stroke = stroke,
                StrokeThickness = strokeThicknessPx,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false,
                Tag = tag
            };
            Panel.SetZIndex(path, 25);
            canvas.Children.Add(path);
        }

        private void DrawCard(Canvas canvas, GiaPhaPlacedNode placed)
        {
            int familyId = placed.Family?.familyInfo?.FamilyId ?? 0;

            // Gia đình ảo (FamilyId < 0): vẽ box đặc biệt thay vì layout thường.
            if (familyId < 0)
            {
                DrawVirtualRootCard(canvas, placed);
                return;
            }

            if (GiaPhaRenderOptions.IsVerticalCardLayout(_options.CardLayoutMode))
            {
                DrawCardVertical(canvas, placed);
                return;
            }

            DrawCardHorizontal(canvas, placed);
        }

        /// <summary>
        /// Vẽ box gia đình ảo (FamilyId &lt; 0): viền đứt nét, nền xám nhạt,
        /// hiển thị tên scope, kích thước canvas và tổng GD con.
        /// </summary>
        private void DrawVirtualRootCard(Canvas canvas, GiaPhaPlacedNode placed)
        {
            double x = Mm(placed.Xmm);
            double y = Mm(placed.Ymm);
            double w = Mm(placed.Metrics.WidthMm);
            double h = Mm(placed.Metrics.HeightMm);
            double pad = Mm(_options.CardPaddingMm);

            // Nền xám rất nhạt, viền đứt nét để phân biệt với box thật.
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = w,
                Height = h,
                RadiusX = Mm(2.0),
                RadiusY = Mm(2.0),
                Fill = new SolidColorBrush(Color.FromArgb(30, 100, 100, 100)),
                Stroke = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                StrokeThickness = Math.Max(1.0, Mm(0.3)),
                StrokeDashArray = new DoubleCollection { 4, 3 },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            Panel.SetZIndex(rect, 10);
            canvas.Children.Add(rect);

            // Dòng 1: nhãn scope (RenderPlanSummary).
            double lineY = y + pad;
            string scopeLabel = _options?.MultiRootScopeLabel ?? "Phả con đa gốc";
            lineY = AddLine(canvas, scopeLabel, x + pad, lineY, w - 2 * pad,
                _options.HeaderFontPt > 0 ? _options.HeaderFontPt : 7.0,
                FontWeights.Bold,
                new SolidColorBrush(Color.FromRgb(80, 80, 120)),
                null);

            // Dòng 2: tổng GD con + kích thước canvas.
            int totalGd = CountFamiliesInPlacedNode(placed) - 1; // trừ chính box ảo
            double wCm = _result.ContentWidthMm / 10.0;
            double hCm = _result.ContentHeightMm / 10.0;
            string infoLine = totalGd + " GD | "
                + wCm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "×"
                + hCm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " cm";
            AddLine(canvas, infoLine, x + pad, lineY, w - 2 * pad,
                _options.NoteFontPt > 0 ? _options.NoteFontPt : 6.5,
                FontWeights.Normal,
                new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                null);
        }

        /// <summary>Đếm số node trong cây con từ FamilyViewModel của một placed node (BFS).</summary>
        private static int CountFamiliesInPlacedNode(GiaPhaPlacedNode root)
        {
            if (root?.Family == null)
            {
                return 0;
            }

            int count = 0;
            var stack = new Stack<FamilyViewModel>();
            stack.Push(root.Family);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                count++;
                if (cur.Children == null)
                {
                    continue;
                }

                foreach (var child in cur.Children)
                {
                    stack.Push(child);
                }
            }

            return count;
        }

        private void DrawCardHorizontal(Canvas canvas, GiaPhaPlacedNode placed)
        {
            double x = Mm(placed.Xmm);
            double y = Mm(placed.Ymm);
            double w = Mm(placed.Metrics.WidthMm);
            double h = Mm(placed.Metrics.HeightMm);

            DrawCardBackground(canvas, placed, x, y, w, h);

            double pad = Mm(_options.CardPaddingMm);
            double lineY = y + Mm(_options.CardHeaderHeightMm) + pad * 0.5;

            var doiTag = new PhaDoBoxVisualTag(
                placed.Family,
                PhaDoPersonTextRole.Main,
                PhaDoBoxElementKind.GenerationLabel,
                PhaDoBoxVisualTag.FamilyLabelSlotIndex);
            AddHorizontalText(canvas, placed.Metrics.FamilyLabel, x + pad, y + pad * 0.4,
                _options.HeaderFontPt, FontWeights.Normal, new SolidColorBrush(Color.FromRgb(70, 70, 70)), doiTag);

            int personSlot = PhaDoBoxVisualTag.MainPersonSlotIndex;
            var mainTag = new PhaDoBoxVisualTag(
                placed.Family, PhaDoPersonTextRole.Main, PhaDoBoxElementKind.Person, personSlot);
            if (placed.Metrics.MainPerson != null)
            {
                lineY = AddLine(canvas, FormatMain(placed.Metrics.MainPerson), x + pad, lineY, w - 2 * pad,
                    _options.MainNameFontPt, FontWeights.Bold, mainTag);
                personSlot++;
            }
            else if (placed.Family != null)
            {
                string label = placed.Family.Name0 ?? placed.Family.Name ?? "Gia đình";
                lineY = AddLine(canvas, label, x + pad, lineY, w - 2 * pad,
                    _options.MainNameFontPt, FontWeights.Bold, mainTag);
                personSlot++;
            }

            foreach (var spouse in placed.Metrics.Spouses)
            {
                var spouseTag = new PhaDoBoxVisualTag(
                    placed.Family, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.Person, personSlot);
                lineY = AddLine(canvas, FormatSpouse(spouse), x + pad, lineY, w - 2 * pad,
                    _options.SpouseFontPt, FontWeights.Normal, spouseTag);
                personSlot++;
            }

            foreach (var overflow in placed.Metrics.SpouseOverflow)
            {
                var overflowTag = new PhaDoBoxVisualTag(
                    placed.Family, PhaDoPersonTextRole.Spouse, PhaDoBoxElementKind.Person, personSlot);
                lineY = AddLine(canvas, overflow, x + pad, lineY, w - 2 * pad,
                    _options.SpouseFontPt, FontWeights.Normal, overflowTag);
                personSlot++;
            }

            // Branch-head (FamilyUp == -1): ghi "Nhánh phả con: X GD" trước extra notes.
            if (placed.Family?.familyInfo?.FamilyUp == -1)
            {
                int branchGd = CountFamiliesInPlacedNode(placed);
                string branchNote = "Nhánh phả con: " + branchGd + " GD";
                double notePt = _options.NoteFontPt > 0 ? _options.NoteFontPt : 6.5;
                lineY = AddLine(canvas, branchNote, x + pad, lineY + Mm(0.5), w - 2 * pad,
                    notePt, FontWeights.Bold,
                    new SolidColorBrush(Color.FromRgb(0, 96, 100)),
                    null);
            }

            DrawCardExtraNotes(canvas, placed, x, y, w, h, pad, ref lineY, personSlot);
        }

        /// <summary>Phần 4: ghi chú (phả con, kích thước cm…) — chữ nhỏ, màu xám.</summary>
        private void DrawCardExtraNotes(
            Canvas canvas,
            GiaPhaPlacedNode placed,
            double x,
            double y,
            double w,
            double h,
            double pad,
            ref double lineY,
            int personSlot)
        {
            var notes = placed.Metrics.ExtraNotes;
            if (notes == null || notes.Count == 0)
            {
                return;
            }

            lineY += Mm(_options.CardNoteTopGapMm);
            // double sepY = lineY - Mm(_options.CardNoteTopGapMm * 0.35);
            // var sep = new System.Windows.Shapes.Line
            // {
            //     X1 = x + pad,
            //     Y1 = sepY,
            //     X2 = x + w - pad,
            //     Y2 = sepY,
            //     Stroke = new SolidColorBrush(Color.FromRgb(190, 198, 210)),
            //     StrokeThickness = 0.8,
            //     IsHitTestVisible = false
            // };
            // Panel.SetZIndex(sep, 12);
            // canvas.Children.Add(sep);

            double notePt = _options.NoteFontPt > 0 ? _options.NoteFontPt : 6.5;
            var noteBrush = new SolidColorBrush(Color.FromRgb(90, 96, 108));
            int noteSlot = 0;
            foreach (var note in notes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                var noteTag = new PhaDoBoxVisualTag(
                    placed.Family,
                    PhaDoPersonTextRole.Spouse,
                    PhaDoBoxElementKind.ExtraNote,
                    personSlot + noteSlot);
                lineY = AddLine(canvas, note.Trim(), x + pad, lineY, w - 2 * pad,
                    notePt, FontWeights.Normal, noteTag, noteBrush);
                noteSlot++;
            }
        }

        private void DrawCardVertical(Canvas canvas, GiaPhaPlacedNode placed)
        {
            double x = Mm(placed.Xmm);
            double y = Mm(placed.Ymm);
            double w = Mm(placed.Metrics.WidthMm);
            double h = Mm(placed.Metrics.HeightMm);

            DrawCardBackground(canvas, placed, x, y, w, h);

            double pad = Mm(_options.CardPaddingMm);
            // Thẻ dọc: không có dải header "Đời" trên box — đời hiển thị ở nhãn dải ngang bên trái canvas
            double contentTop = y + pad * 0.5;
            double contentHeightPx = h - pad - Mm(_options.CardBottomPaddingMm);
            double colX = x + pad;

            bool isWordLayout = _options.CardLayoutMode == GiaPhaCardLayoutMode.VerticalWord;
            var columns = isWordLayout
                ? GiaPhaVerticalWordCardLayout.BuildColumns(placed.Metrics, _options, placed.Family)
                : GiaPhaVerticalCardLayout.BuildColumns(placed.Metrics, _options, placed.Family);

            if (!isWordLayout && !string.IsNullOrWhiteSpace(placed.Metrics.FamilyLabel))
            {
                double doiFontPt = _options.VerticalGenerationLabelFontPt > 0
                    ? _options.VerticalGenerationLabelFontPt
                    : _options.HeaderFontPt;
                double doiColW = Mm(GiaPhaVerticalCardLayout.ColumnWidthMm(doiFontPt, _options));
                var doiTag = new PhaDoBoxVisualTag(
                    placed.Family,
                    PhaDoPersonTextRole.Main,
                    PhaDoBoxElementKind.GenerationLabel,
                    PhaDoBoxVisualTag.FamilyLabelSlotIndex);
                AddVerticalText(canvas, placed.Metrics.FamilyLabel, colX, contentTop, doiColW, contentHeightPx,
                    doiFontPt, FontWeights.SemiBold,
                    new SolidColorBrush(Color.FromRgb(70, 70, 70)), doiTag);
                colX += doiColW + Mm(GiaPhaVerticalCardLayout.ColumnGapMm);
            }

            int personSlot = PhaDoBoxVisualTag.MainPersonSlotIndex;
            foreach (var col in columns)
            {
                double colW = Mm(col.WidthMm);
                var role = col.Bold ? PhaDoPersonTextRole.Main : PhaDoPersonTextRole.Spouse;
                var textTag = new PhaDoBoxVisualTag(
                    placed.Family, role, PhaDoBoxElementKind.Person, personSlot);
                personSlot++;

                if (col.IsWordStack)
                {
                    AddVerticalWordStackText(canvas, col.HorizontalWordLines, colX, contentTop, colW,
                        col.FontPt, col.Bold ? FontWeights.Bold : FontWeights.Normal, Brushes.Black, textTag);
                }
                else
                {
                    AddVerticalText(canvas, col.Text, colX, contentTop, colW, contentHeightPx, col.FontPt,
                        col.Bold ? FontWeights.Bold : FontWeights.Normal, Brushes.Black,
                        textTag);
                }

                colX += colW + Mm(GiaPhaVerticalCardLayout.ColumnGapMm);
            }

            // Branch-head (FamilyUp == -1): ghi "Nhánh phả con: X GD" ở đáy thẻ dọc.
            if (placed.Family?.familyInfo?.FamilyUp == -1)
            {
                int branchGd = CountFamiliesInPlacedNode(placed);
                string branchNote = "Nhánh phả con: " + branchGd + " GD";
                double noteY = y + h - Mm(4.5);
                double notePt = _options.NoteFontPt > 0 ? _options.NoteFontPt : 6.5;
                AddHorizontalText(canvas, branchNote, x + pad, noteY, notePt,
                    FontWeights.Bold, new SolidColorBrush(Color.FromRgb(0, 96, 100)), null);
            }

            DrawCardExtraNotesVertical(canvas, placed, x, y, w, h, pad);
        }

        /// <summary>Ghi chú cuối thẻ dọc — chữ ngang nhỏ ở đáy ô.</summary>
        private void DrawCardExtraNotesVertical(
            Canvas canvas,
            GiaPhaPlacedNode placed,
            double x,
            double y,
            double w,
            double h,
            double pad)
        {
            var notes = placed.Metrics.ExtraNotes;
            if (notes == null || notes.Count == 0)
            {
                return;
            }

            double notePt = _options.NoteFontPt > 0 ? _options.NoteFontPt : 6.5;
            double innerMaxMm = Math.Max(8, placed.Metrics.WidthMm - _options.CardPaddingMm * 2);
            double innerW = w - 2 * pad;
            double noteBlockMm = _options.CardNoteTopGapMm;
            foreach (var note in notes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                noteBlockMm += FamilyCardMetrics.EstimateWrappedLineHeightMm(
                    note.Trim(), notePt, innerMaxMm, _options);
            }

            double lineY = y + Mm(placed.Metrics.HeightMm - _options.CardBottomPaddingMm - noteBlockMm
                + _options.CardNoteTopGapMm);
            int noteSlot = 0;
            var noteBrush = new SolidColorBrush(Color.FromRgb(90, 96, 108));
            foreach (var note in notes)
            {
                if (string.IsNullOrWhiteSpace(note))
                {
                    continue;
                }

                var noteTag = new PhaDoBoxVisualTag(
                    placed.Family,
                    PhaDoPersonTextRole.Spouse,
                    PhaDoBoxElementKind.ExtraNote,
                    PhaDoBoxVisualTag.MainPersonSlotIndex + 100 + noteSlot);
                lineY = AddLine(canvas, note.Trim(), x + pad, lineY, innerW,
                    notePt, FontWeights.Normal, noteTag, noteBrush);
                noteSlot++;
            }
        }

        /// <summary>Kiểu Word: trong một cột, mỗi từ là một dòng chữ ngang.</summary>
        private void AddVerticalWordStackText(
            Canvas canvas,
            string[] words,
            double columnLeft,
            double columnTop,
            double columnWidthPx,
            double fontPt,
            FontWeight weight,
            Brush foreground,
            object tag)
        {
            if (words == null || words.Length == 0)
            {
                return;
            }

            double linePx = Mm(GiaPhaVerticalWordCardLayout.WordLineHeightMm(fontPt, _options));
            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = columnWidthPx,
                // Mở rộng vùng hit-test của cả cột để kéo/thả chữ dễ hơn.
                Background = Brushes.Transparent,
                Tag = tag
            };

            foreach (string word in words)
            {
                var tb = new TextBlock
                {
                    Text = word,
                    FontFamily = new FontFamily(_options.FontFamilyName),
                    FontSize = fontPt * _dpi / 72.0,
                    FontWeight = weight,
                    Foreground = foreground,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Width = columnWidthPx,
                    Background = Brushes.Transparent
                };
                column.Children.Add(tb);
            }

            column.Measure(new Size(columnWidthPx, double.PositiveInfinity));
            double stackW = column.DesiredSize.Width;
            Canvas.SetLeft(column, columnLeft + Math.Max(0, (columnWidthPx - stackW) / 2.0));
            Canvas.SetTop(column, columnTop);
            Panel.SetZIndex(column, 11);
            canvas.Children.Add(column);
        }

        private void DrawCardBackground(
            Canvas canvas,
            GiaPhaPlacedNode placed,
            double x,
            double y,
            double w,
            double h)
        {
            int familyId = placed.Family?.familyInfo?.FamilyId ?? 0;
            int familyUp = placed.Family?.familyInfo?.FamilyUp ?? 0;
            // FamilyId < 0 → gia đình ảo (đã vẽ riêng, không vào đây).
            bool isVirtual = familyId < 0;
            // FamilyUp == -1 → gốc nhánh non-STOP trực tiếp dưới gia đình ảo.
            bool isBranchHead = !isVirtual && familyUp == -1;
            int branchHue = Math.Abs(familyId) % 6;
            Brush fill;
            Brush border;
            if (isVirtual)
            {
                fill = Brushes.Transparent;
                border = Brushes.Transparent;
            }
            else if (isBranchHead)
            {
                // Màu xanh lá đậm để nổi bật là gốc nhánh phả con.
                fill = new SolidColorBrush(Color.FromRgb(178, 235, 242));
                border = new SolidColorBrush(Color.FromRgb(0, 131, 143));
            }
            else
            {
                fill = BranchFill(branchHue);
                border = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            }

            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = w,
                Height = h,
                RadiusX = Mm(1.5),
                RadiusY = Mm(1.5),
                Fill = fill,
                Stroke = border,
                StrokeThickness = Math.Max(1.0, Mm(0.25)),
                Tag = new PhaDoBoxBackgroundTag(placed.Family)
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            Panel.SetZIndex(rect, 10);
            canvas.Children.Add(rect);
        }

        private void AddHorizontalText(
            Canvas canvas,
            string text,
            double x,
            double y,
            double fontPt,
            FontWeight weight,
            Brush foreground,
            object tag,
            double? widthPx = null,
            TextAlignment alignment = TextAlignment.Left)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily(_options.FontFamilyName),
                FontSize = fontPt * _dpi / 72.0,
                FontWeight = weight,
                Foreground = foreground,
                TextAlignment = alignment,
                Background = Brushes.Transparent,
                Tag = tag
            };
            if (widthPx.HasValue)
            {
                tb.Width = widthPx.Value;
            }

            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            Panel.SetZIndex(tb, 11);
            canvas.Children.Add(tb);
        }

        /// <summary>Chữ dọc: xếp từng ký tự trong cột (không xoay — tránh lệch khỏi ô).</summary>
        private void AddVerticalText(
            Canvas canvas,
            string text,
            double columnLeft,
            double columnTop,
            double columnWidthPx,
            double maxRunHeightPx,
            double fontPt,
            FontWeight weight,
            Brush foreground,
            object tag)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            double fontPx = fontPt * _dpi / 72.0;
            double linePx = Mm(GiaPhaVerticalCardLayout.CharStepMm(fontPt));
            var column = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Width = columnWidthPx,
                // Cả cột bắt chuột được để chọn/kéo text dọc đỡ bị "trượt".
                Background = Brushes.Transparent,
                Tag = tag
            };

            if (maxRunHeightPx > 1)
            {
                column.MaxHeight = maxRunHeightPx;
                column.ClipToBounds = true;
            }

            foreach (char ch in text)
            {
                column.Children.Add(new TextBlock
                {
                    Text = ch.ToString(),
                    FontFamily = new FontFamily(_options.FontFamilyName),
                    FontSize = fontPx,
                    FontWeight = weight,
                    Foreground = foreground,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    LineHeight = linePx,
                    Height = linePx,
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent
                });
            }

            column.Measure(new Size(columnWidthPx, maxRunHeightPx > 1 ? maxRunHeightPx : double.PositiveInfinity));
            double stackW = column.DesiredSize.Width;
            Canvas.SetLeft(column, columnLeft + Math.Max(0, (columnWidthPx - stackW) / 2.0));
            Canvas.SetTop(column, columnTop);
            Panel.SetZIndex(column, 11);
            canvas.Children.Add(column);
        }

        private double AddLine(Canvas canvas, string text, double x, double y, double maxW, double fontPt, FontWeight weight, object tag)
        {
            return AddLine(canvas, text, x, y, maxW, fontPt, weight, tag, Brushes.Black);
        }

        private double AddLine(
            Canvas canvas,
            string text,
            double x,
            double y,
            double maxW,
            double fontPt,
            FontWeight weight,
            object tag,
            Brush foreground)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily(_options.FontFamilyName),
                FontSize = fontPt * _dpi / 72.0,
                FontWeight = weight,
                Foreground = foreground ?? Brushes.Black,
                // Dòng ngang dùng full bề rộng nội dung để vùng kéo/thả rộng, dễ thao tác hơn.
                Width = maxW,
                MaxWidth = maxW,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Background = Brushes.Transparent,
                Tag = tag
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            Panel.SetZIndex(tb, 11);
            canvas.Children.Add(tb);
            tb.Measure(new Size(maxW, double.PositiveInfinity));
            double lineMm = PrintUnits.PixelsToMm(tb.DesiredSize.Height, _dpi);
            if (lineMm < _options.CardLineHeightMm)
            {
                lineMm = _options.CardLineHeightMm;
            }
            return y + Mm(lineMm);
        }

        private static string FormatMain(PersonInfo p)
        {
            return p.MANS_NAME_HUY ?? "";
        }

        private static string FormatSpouse(PersonInfo p)
        {
            string g = p.MANS_GENDER == "Nữ" ? "♀" : "♂";
            return g + " " + (p.MANS_NAME_HUY ?? "");
        }

        private static Brush BranchFill(int branchIndex)
        {
            Color[] palette =
            {
                Color.FromRgb(255, 243, 224),
                Color.FromRgb(232, 245, 233),
                Color.FromRgb(227, 242, 253),
                Color.FromRgb(252, 228, 236),
                Color.FromRgb(237, 231, 246),
                Color.FromRgb(255, 249, 196)
            };
            return new SolidColorBrush(palette[branchIndex % palette.Length]);
        }

        private double Mm(double mm)
        {
            return PrintUnits.MmToPixels(mm, _dpi);
        }
    }
}
