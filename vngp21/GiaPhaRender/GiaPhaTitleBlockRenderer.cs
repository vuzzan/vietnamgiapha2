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

    /// <summary>Vẽ khối tiêu đề 2 dòng lên canvas WPF — khung theo bề rộng chữ.</summary>
    public static class GiaPhaTitleBlockRenderer
    {
        public static void DrawToCanvas(Canvas canvas, GiaPhaRenderOptions options, double dpi, double pageWidthPx)
        {
            if (canvas == null || options == null)
            {
                return;
            }

            bool has1 = !string.IsNullOrWhiteSpace(options.Title);
            bool has2 = !string.IsNullOrWhiteSpace(options.TitleLine2);
            if (!has1 && !has2)
            {
                return;
            }

            var layout = PhaDoTitleBlockMetrics.Measure(options, dpi);
            if (layout.WidthMm < 0.1 || layout.HeightMm < 0.1)
            {
                return;
            }

            double leftPx = Mm(layout.LeftMm, dpi);
            double topPx = Mm(layout.TopMm, dpi);
            double boxWPx = Mm(layout.WidthMm, dpi);
            double boxHPx = Mm(layout.HeightMm, dpi);

            bool hasSvg = !string.IsNullOrWhiteSpace(options.TitleCustomShapeSvg);
            if (hasSvg)
            {
                var frame = PhaDoBoxSvgWpfRenderer.CreateBackgroundElement(
                    options.TitleCustomShapeSvg,
                    options.TitleCustomShapeViewBoxWidth,
                    options.TitleCustomShapeViewBoxHeight,
                    boxWPx,
                    boxHPx,
                    null,
                    options.TitleFillColorHex);
                if (frame != null)
                {
                    frame.Tag = new PhaDoTitleVisualTag();
                    Canvas.SetLeft(frame, leftPx);
                    Canvas.SetTop(frame, topPx);
                    Panel.SetZIndex(frame, 5);
                    canvas.Children.Add(frame);
                }
            }
            else if (!string.IsNullOrWhiteSpace(options.TitleFillColorHex))
            {
                var bg = new System.Windows.Shapes.Rectangle
                {
                    Width = boxWPx,
                    Height = boxHPx,
                    Fill = BrushFromHex(options.TitleFillColorHex),
                    RadiusX = Mm(1.5, dpi),
                    RadiusY = Mm(1.5, dpi),
                    Tag = new PhaDoTitleVisualTag()
                };
                Canvas.SetLeft(bg, leftPx);
                Canvas.SetTop(bg, topPx);
                Panel.SetZIndex(bg, 5);
                canvas.Children.Add(bg);
            }

            double textX = Mm(layout.TextLeftMm, dpi);

            if (has1)
            {
                AddLine(canvas, options.Title, textX, Mm(layout.Line1TopMm, dpi),
                    options.TitleLine1FontFamily ?? options.FontFamilyName,
                    options.TitleFontPt, options.TitleLine1ForegroundHex, dpi, FontWeights.Bold);
            }

            if (has2)
            {
                AddLine(canvas, options.TitleLine2, textX, Mm(layout.Line2TopMm, dpi),
                    options.TitleLine2FontFamily ?? options.FontFamilyName,
                    options.TitleLine2FontPt, options.TitleLine2ForegroundHex, dpi, FontWeights.Normal);
            }
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
            FontWeight weight)
        {
            var tb = new TextBlock
            {
                Text = text ?? "",
                FontFamily = new FontFamily(string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily),
                FontSize = fontPt * dpi / 72.0,
                FontWeight = weight,
                Foreground = BrushFromHex(foregroundHex) ?? Brushes.Black,
                Tag = new PhaDoTitleVisualTag()
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            Panel.SetZIndex(tb, 6);
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
