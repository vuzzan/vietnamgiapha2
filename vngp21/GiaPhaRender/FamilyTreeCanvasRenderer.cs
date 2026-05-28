using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
                double bandLabelPt = vertical
                    ? _options.VerticalGenerationLabelFontPt
                    : _options.HeaderFontPt;
                var label = new TextBlock
                {
                    Text = "Đời " + band.Level,
                    FontFamily = new FontFamily(_options.FontFamilyName),
                    FontSize = bandLabelPt * _dpi / 72.0,
                    FontWeight = vertical ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = vertical
                        ? new SolidColorBrush(Color.FromRgb(25, 55, 120))
                        : new SolidColorBrush(Color.FromRgb(90, 90, 90)),
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(label, Mm(_options.MarginMm));
                Canvas.SetTop(label, Mm(band.Ymm) + 2);
                Panel.SetZIndex(label, 1);
                canvas.Children.Add(label);
            }
        }

        private void DrawConnectors(Canvas canvas)
        {
            var stroke = new SolidColorBrush(Color.FromRgb(35, 35, 35));
            double thickness = Math.Max(1.4, Mm(0.45));
            var byFamily = _result.Nodes
                .Where(n => n.Family != null)
                .ToDictionary(n => n.Family, n => n);

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

        private void DrawCard(Canvas canvas, GiaPhaPlacedNode placed)
        {
            if (GiaPhaRenderOptions.IsVerticalCardLayout(_options.CardLayoutMode))
            {
                DrawCardVertical(canvas, placed);
                return;
            }

            DrawCardHorizontal(canvas, placed);
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
                    Width = columnWidthPx
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
            int branchHue = (placed.Family?.familyInfo?.FamilyId ?? 0) % 6;
            var fill = BranchFill(branchHue);
            var border = new SolidColorBrush(Color.FromRgb(80, 80, 80));

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
                    Padding = new Thickness(0)
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
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily(_options.FontFamilyName),
                FontSize = fontPt * _dpi / 72.0,
                FontWeight = weight,
                Foreground = Brushes.Black,
                MaxWidth = maxW,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
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
            return "★ " + (p.MANS_NAME_HUY ?? "");
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
