using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Vẽ SVG đã sanitize lên Canvas WPF (scale vào khung ô).</summary>
    public static class PhaDoBoxSvgWpfRenderer
    {
        public static FrameworkElement CreateBackgroundElement(
            string sanitizedSvgMarkup,
            double viewBoxWidth,
            double viewBoxHeight,
            double targetWidthPx,
            double targetHeightPx,
            FamilyViewModel family,
            string fillColorHex)
        {
            if (string.IsNullOrWhiteSpace(sanitizedSvgMarkup))
            {
                return CreateDefaultRectangle(targetWidthPx, targetHeightPx, family, fillColorHex);
            }

            var container = new Grid
            {
                Width = targetWidthPx,
                Height = targetHeightPx,
                Tag = new PhaDoBoxBackgroundTag(family)
            };

            if (!string.IsNullOrWhiteSpace(fillColorHex))
            {
                container.Children.Add(new Rectangle
                {
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillColorHex)),
                    RadiusX = 1,
                    RadiusY = 1
                });
            }

            var drawing = TryReadDrawing(sanitizedSvgMarkup);
            if (drawing != null)
            {
                var image = new Image
                {
                    Source = new DrawingImage(drawing),
                    Stretch = Stretch.Fill,
                    Width = targetWidthPx,
                    Height = targetHeightPx
                };
                container.Children.Add(image);
                Panel.SetZIndex(image, 1);
                return container;
            }

            return CreateDefaultRectangle(targetWidthPx, targetHeightPx, family, fillColorHex);
        }

        /// <summary>Xem trước khung SVG trong dialog (không cần FamilyViewModel).</summary>
        public static UIElement CreateDialogPreview(
            string sanitizedSvgMarkup,
            double viewBoxWidth,
            double viewBoxHeight,
            string fillColorHex,
            double previewWidthPx = 220,
            double previewHeightPx = 130)
        {
            var host = new Grid
            {
                Width = previewWidthPx,
                Height = previewHeightPx
            };

            if (!string.IsNullOrWhiteSpace(fillColorHex))
            {
                host.Children.Add(new Rectangle
                {
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillColorHex)),
                    RadiusX = 2,
                    RadiusY = 2
                });
            }

            if (string.IsNullOrWhiteSpace(sanitizedSvgMarkup))
            {
                host.Children.Add(new Rectangle
                {
                    RadiusX = 4,
                    RadiusY = 4,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                    Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                    StrokeThickness = 1,
                    Margin = new Thickness(8)
                });
                return host;
            }

            var drawing = TryReadDrawing(sanitizedSvgMarkup);
            if (drawing == null)
            {
                host.Children.Add(new TextBlock
                {
                    Text = "Không hiển thị được preview",
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                return host;
            }

            var viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Margin = new Thickness(4),
                Child = new Image
                {
                    Source = new DrawingImage(drawing),
                    Stretch = Stretch.Fill
                }
            };
            Panel.SetZIndex(viewbox, 1);
            host.Children.Add(viewbox);
            return host;
        }

        private static DrawingGroup TryReadDrawing(string sanitizedSvgMarkup)
        {
            try
            {
                var settings = new WpfDrawingSettings
                {
                    IncludeRuntime = true,
                    TextAsGeometry = false
                };
                var reader = new FileSvgReader(settings);
                string wrapped = WrapSvgDocument(sanitizedSvgMarkup);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(wrapped)))
                {
                    return reader.Read(stream);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string WrapSvgDocument(string innerOrRootSvg)
        {
            string trimmed = innerOrRootSvg.Trim();
            if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
            {
                return "<?xml version=\"1.0\" encoding=\"utf-8\"?>" + trimmed;
            }

            return "<?xml version=\"1.0\" encoding=\"utf-8\"?><svg xmlns=\"http://www.w3.org/2000/svg\">"
                + trimmed + "</svg>";
        }

        public static Rectangle CreateDefaultRectangle(
            double widthPx,
            double heightPx,
            FamilyViewModel family,
            string fillColorHex)
        {
            int familyId = family?.familyInfo?.FamilyId ?? 0;
            Brush fill;
            if (!string.IsNullOrWhiteSpace(fillColorHex))
            {
                fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fillColorHex));
            }
            else
            {
                fill = GetDefaultBranchFillBrush(familyId);
            }

            return new Rectangle
            {
                Width = widthPx,
                Height = heightPx,
                RadiusX = Math.Max(1, widthPx * 0.02),
                RadiusY = Math.Max(1, heightPx * 0.02),
                Fill = fill,
                Stroke = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                StrokeThickness = 1,
                Tag = new PhaDoBoxBackgroundTag(family)
            };
        }

        private static Brush GetDefaultBranchFillBrush(int familyId)
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
            return new SolidColorBrush(palette[Math.Abs(familyId) % palette.Length]);
        }
    }
}
