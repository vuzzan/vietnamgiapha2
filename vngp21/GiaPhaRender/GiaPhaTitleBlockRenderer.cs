using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Tag trên visual khối tiêu đề phả đồ.</summary>
    public sealed class PhaDoTitleVisualTag
    {
    }

    /// <summary>Vẽ khối tiêu đề 4 dòng lên canvas WPF — khung theo bề rộng chữ.</summary>
    public static class GiaPhaTitleBlockRenderer
    {
        /// <summary>Vẽ khối tiêu đề và trả về layout để code ngoài có thể dùng (vd: selection handle).</summary>
        public static PhaDoTitleBlockLayout DrawToCanvas(Canvas canvas, GiaPhaRenderOptions options, double dpi, double pageWidthPx)
        {
            if (canvas == null || options == null)
                return null;

            bool has1 = !string.IsNullOrWhiteSpace(options.Title);
            bool has2 = !string.IsNullOrWhiteSpace(options.TitleLine2);
            bool has3 = !string.IsNullOrWhiteSpace(options.TitleLine3);
            bool has4 = !string.IsNullOrWhiteSpace(options.TitleLine4);
            if (!has1 && !has2 && !has3 && !has4)
                return null;

            var layout = PhaDoTitleBlockMetrics.Measure(options, dpi);
            if (layout.WidthMm < 0.1 || layout.HeightMm < 0.1)
                return null;

            double leftPx  = Mm(layout.LeftMm,  dpi);
            double topPx   = Mm(layout.TopMm,   dpi);
            double boxWPx  = Mm(layout.WidthMm,  dpi);
            double boxHPx  = Mm(layout.HeightMm, dpi);

            // ── Nền hoặc SVG chính ─────────────────────────────────────────
            bool hasSvg = !string.IsNullOrWhiteSpace(options.TitleCustomShapeSvg);
            if (hasSvg)
            {
                var frame = PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(
                    options.TitleCustomShapeSvg, options.TitleCustomShapeViewBoxWidth,
                    options.TitleCustomShapeViewBoxHeight, boxWPx, boxHPx, null, options.TitleFillColorHex);
                if (frame != null)
                {
                    frame.Tag = new PhaDoTitleVisualTag();
                    Canvas.SetLeft(frame, leftPx); Canvas.SetTop(frame, topPx);
                    Panel.SetZIndex(frame, 5); canvas.Children.Add(frame);
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.TitleFillColorHex))
            {
                var bg = new System.Windows.Shapes.Rectangle
                {
                    Width = boxWPx, Height = boxHPx,
                    Fill = BrushFromHex(options.TitleFillColorHex),
                    RadiusX = Mm(1.5, dpi), RadiusY = Mm(1.5, dpi),
                    Tag = new PhaDoTitleVisualTag()
                };
                Canvas.SetLeft(bg, leftPx); Canvas.SetTop(bg, topPx);
                Panel.SetZIndex(bg, 5); canvas.Children.Add(bg);
            }

            // ── Vùng SVG trang trí 4 phía ─────────────────────────────────
            DrawFrameZone(canvas, options.TitleTopSvg,    options.TitleTopSvgViewBoxW,    options.TitleTopSvgViewBoxH,
                          options.TitleTopSvgSizeMm,    leftPx, topPx, boxWPx, boxHPx, dpi, FrameZone.Top);
            DrawFrameZone(canvas, options.TitleBottomSvg, options.TitleBottomSvgViewBoxW, options.TitleBottomSvgViewBoxH,
                          options.TitleBottomSvgSizeMm, leftPx, topPx, boxWPx, boxHPx, dpi, FrameZone.Bottom);
            DrawFrameZone(canvas, options.TitleLeftSvg,   options.TitleLeftSvgViewBoxW,   options.TitleLeftSvgViewBoxH,
                          options.TitleLeftSvgSizeMm,   leftPx, topPx, boxWPx, boxHPx, dpi, FrameZone.Left);
            DrawFrameZone(canvas, options.TitleRightSvg,  options.TitleRightSvgViewBoxW,  options.TitleRightSvgViewBoxH,
                          options.TitleRightSvgSizeMm,  leftPx, topPx, boxWPx, boxHPx, dpi, FrameZone.Right);

            // ── Vùng hit-test trong suốt để nhận click chọn title block ──
            var hitRect = new System.Windows.Shapes.Rectangle
            {
                Width = boxWPx, Height = boxHPx,
                Fill = System.Windows.Media.Brushes.Transparent,
                Tag = new PhaDoTitleHitTag()
            };
            Canvas.SetLeft(hitRect, leftPx); Canvas.SetTop(hitRect, topPx);
            // Z thấp hơn chữ (8) — click cấp 1 vào vùng trống; khi đã chọn box thì click được từng dòng
            Panel.SetZIndex(hitRect, 7);
            canvas.Children.Add(hitRect);

            // ── 4 dòng chữ ────────────────────────────────────────────────
            double defaultSmallPt = Math.Max(7, (options.TitleLine2FontPt > 0 ? options.TitleLine2FontPt : 12) * 0.78);
            double textX = Mm(layout.TextLeftMm, dpi);

            if (has1) AddLine(canvas, options.Title, textX, Mm(layout.Line1TopMm, dpi),
                              options.TitleLine1FontFamily ?? options.FontFamilyName,
                              options.TitleFontPt, options.TitleLine1ForegroundHex, dpi, FontWeights.Bold, 0);

            if (has2) AddLine(canvas, options.TitleLine2, textX, Mm(layout.Line2TopMm, dpi),
                              options.TitleLine2FontFamily ?? options.FontFamilyName,
                              options.TitleLine2FontPt, options.TitleLine2ForegroundHex, dpi, FontWeights.Normal, 1);

            // Dòng 3: dùng FontPt/Fore từ options nếu user đã chỉnh, ngược lại dùng mặc định nhỏ
            double l3Pt = options.TitleLine3FontPt > 0 ? options.TitleLine3FontPt : defaultSmallPt;
            string l3Fore = !string.IsNullOrWhiteSpace(options.TitleLine3ForegroundHex)
                ? options.TitleLine3ForegroundHex : "#888888";
            if (has3) AddLine(canvas, options.TitleLine3, textX, Mm(layout.Line3TopMm, dpi),
                              options.TitleLine3FontFamily ?? options.FontFamilyName,
                              l3Pt, l3Fore, dpi, FontWeights.Normal, 2);

            double l4Pt = options.TitleLine4FontPt > 0 ? options.TitleLine4FontPt : defaultSmallPt;
            string l4Fore = !string.IsNullOrWhiteSpace(options.TitleLine4ForegroundHex)
                ? options.TitleLine4ForegroundHex : "#888888";
            if (has4) AddLine(canvas, options.TitleLine4, textX, Mm(layout.Line4TopMm, dpi),
                              options.TitleLine4FontFamily ?? options.FontFamilyName,
                              l4Pt, l4Fore, dpi, FontWeights.Normal, 3);

            return layout;
        }

        // ── Vùng trang trí 4 phía ─────────────────────────────────────────
        private enum FrameZone { Top, Bottom, Left, Right }

        private static void DrawFrameZone(Canvas canvas, string svg, double vbW, double vbH, double sizeMm,
                                          double leftPx, double topPx, double boxWPx, double boxHPx, double dpi,
                                          FrameZone zone)
        {
            if (string.IsNullOrWhiteSpace(svg)) return;
            if (sizeMm <= 0) sizeMm = 8; // mặc định 8mm
            double sizePx = Mm(sizeMm, dpi);

            double elW, elH, elLeft, elTop;
            switch (zone)
            {
                case FrameZone.Top:
                    elW = boxWPx; elH = sizePx;
                    elLeft = leftPx; elTop = topPx - sizePx;
                    break;
                case FrameZone.Bottom:
                    elW = boxWPx; elH = sizePx;
                    elLeft = leftPx; elTop = topPx + boxHPx;
                    break;
                case FrameZone.Left:
                    elW = sizePx; elH = boxHPx;
                    elLeft = leftPx - sizePx; elTop = topPx;
                    break;
                default: // Right
                    elW = sizePx; elH = boxHPx;
                    elLeft = leftPx + boxWPx; elTop = topPx;
                    break;
            }

            if (vbW <= 0) vbW = 100;
            if (vbH <= 0) vbH = 100;
            var el = PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(svg, vbW, vbH, elW, elH, null, null);
            if (el == null) return;
            el.Tag = new PhaDoTitleVisualTag();
            Canvas.SetLeft(el, elLeft); Canvas.SetTop(el, elTop);
            Panel.SetZIndex(el, 4);
            canvas.Children.Add(el);
        }

        private static void AddLine(
            Canvas canvas,
            string text,
            double x,
            double y,
            string fontFamily,
            double fontPt,
            string foregroundHex,
            double dpi,
            FontWeight weight,
            int lineIndex)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontFamily = new FontFamily(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily),
                FontSize = fontPt * dpi / 72.0,
                FontWeight = weight,
                Foreground = BrushFromHex(foregroundHex) ?? Brushes.Black,
                // Dán tag lineIndex để nhận click chọn từng dòng
                Tag = new PhaDoTitleTextLineTag(lineIndex),
                IsHitTestVisible = true,
                Background = System.Windows.Media.Brushes.Transparent,
                Padding = new Thickness(2, 1, 2, 1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            Panel.SetZIndex(tb, 8);
            canvas.Children.Add(tb);
        }

        private static Brush BrushFromHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            try
            {
                string h = hex.Trim();
                if (!h.StartsWith("#", StringComparison.Ordinal))
                {
                    h = "#" + h;
                }

                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(h));
            }
            catch
            {
                return null;
            }
        }

        private static double Mm(double mm, double dpi) => PrintUnits.MmToPixels(mm, dpi);
    }
}
