using System;
using System.IO;
using System.Printing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Xps;
using System.Windows.Xps.Packaging;
using vietnamgiapha;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>
    /// API công khai: layout + vẽ + xuất/in — khổ tự động theo cây (FitContent).
    /// Không phụ thuộc Draw/GraphData cũ.
    /// 
    /// using vietnamgiapha.GiaPhaRender;
//    var options = GiaPhaRenderOptions.ForA0LandscapePrint();
//    options.Title = viewModel.FamilyTree.GiaphaName;
//options.PrintDpi = 150; // 200 nếu in sắc hơn (file nặng hơn)
//// Preview trên Canvas riêng
//var result = GiaPhaRenderService.RenderToCanvas(myPrintCanvas, viewModel.FamilyTree.Family.RootPerson, options);
//    // Xuất PNG khổ A0
//    GiaPhaRenderService.ExportToPng(@"D:\output\giapha_A0.png", viewModel.FamilyTree.Family.RootPerson, options);
//// Xuất XPS / in (chọn máy in A0)
//GiaPhaRenderService.ExportToXps(@"D:\output\giapha_A0.xps", viewModel.FamilyTree.Family.RootPerson, options);
//GiaPhaRenderService.Print(viewModel.FamilyTree.Family.RootPerson, options);
    /// </summary>
    public static class GiaPhaRenderService
    {
        /// <summary>Tính layout — khổ theo cây (mặc định FitContent).</summary>
        public static GiaPhaRenderResult ComputeLayout(
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            options = options ?? GiaPhaRenderOptions.ForFitContent();
            var engine = new FamilyTreeLayoutEngine(options, options.PrintDpi);
            return engine.Layout(root);
        }

        /// <summary>Layout trên thread pool — không block UI / STA.</summary>
        public static Task<GiaPhaRenderResult> ComputeLayoutAsync(
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            options = options ?? GiaPhaRenderOptions.ForFitContent();
            return Task.Run(() => ComputeLayout(root, options));
        }

        /// <summary>Chỉ vẽ (nhanh) — phải gọi trên UI thread của canvas.</summary>
        public static void PaintToCanvas(Canvas canvas, GiaPhaRenderResult result)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            new FamilyTreeCanvasRenderer(result).RenderTo(canvas);
        }

        /// <summary>Layout nền + vẽ UI — tránh ContextSwitchDeadlock.</summary>
        public static async Task<GiaPhaRenderResult> RenderToCanvasAsync(
            Canvas canvas,
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            var result = await ComputeLayoutAsync(root, options).ConfigureAwait(true);

            await canvas.Dispatcher.InvokeAsync(
                () => PaintToCanvas(canvas, result),
                DispatcherPriority.Background).Task.ConfigureAwait(true);

            return result;
        }

        /// <summary>Đồng bộ: layout + vẽ (chỉ dùng khi đã trên UI thread và cây nhỏ).</summary>
        public static GiaPhaRenderResult RenderToCanvas(
            Canvas canvas,
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            var result = ComputeLayout(root, options);
            PaintToCanvas(canvas, result);
            return result;
        }

        /// <summary>Raster hóa layout đã tính — gọi trên UI thread (STA).</summary>
        public static byte[] RenderResultToPngBytes(
            GiaPhaRenderResult result,
            GiaPhaRenderOptions options,
            out int widthPx,
            out int heightPx)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            options = options ?? GiaPhaRenderOptions.ForFitContentPrint();

            var canvas = new Canvas();
            PaintToCanvas(canvas, result);

            double w = result.CanvasWidthPixels > 0 ? result.CanvasWidthPixels : result.PageWidthPixels;
            double h = result.CanvasHeightPixels > 0 ? result.CanvasHeightPixels : result.PageHeightPixels;
            canvas.Measure(new Size(w, h));
            canvas.Arrange(new Rect(0, 0, w, h));
            canvas.UpdateLayout();

            widthPx = (int)Math.Ceiling(w);
            heightPx = (int)Math.Ceiling(h);

            var rtb = new RenderTargetBitmap(
                widthPx,
                heightPx,
                options.PrintDpi,
                options.PrintDpi,
                PixelFormats.Pbgra32);

            rtb.Render(canvas);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var ms = new MemoryStream())
            {
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        /// <summary>Xuất PNG — khổ = đúng kích thước cây.</summary>
        public static GiaPhaRenderResult ExportToPng(
            string filePath,
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }

            options = options ?? GiaPhaRenderOptions.ForFitContentPrint();
            var result = ComputeLayout(root, options);
            var bytes = RenderResultToPngBytes(result, options, out _, out _);

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(filePath, bytes);

            return result;
        }

        /// <summary>Xuất XPS — khổ tùy theo cây.</summary>
        public static GiaPhaRenderResult ExportToXps(
            string filePath,
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }

            options = options ?? GiaPhaRenderOptions.ForFitContentPrint();
            var result = ComputeLayout(root, options);
            ExportResultToXps(filePath, result);
            return result;
        }

        /// <summary>Xuất XPS từ layout đã tính (dùng cho kéo-thả chỉnh tay).</summary>
        public static void ExportResultToXps(
            string filePath,
            GiaPhaRenderResult result)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            var canvas = new Canvas();
            PaintToCanvas(canvas, result);
            double w = result.CanvasWidthPixels > 0 ? result.CanvasWidthPixels : result.PageWidthPixels;
            double h = result.CanvasHeightPixels > 0 ? result.CanvasHeightPixels : result.PageHeightPixels;
            canvas.Measure(new Size(w, h));
            canvas.Arrange(new Rect(0, 0, w, h));
            canvas.UpdateLayout();

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            using (var xpsDoc = new XpsDocument(filePath, FileAccess.Write))
            {
                var writer = XpsDocument.CreateXpsDocumentWriter(xpsDoc);
                writer.Write(canvas);
            }
        }

        /// <summary>Xuất SVG vector (mm) — mở trên trình duyệt, Inkscape, Illustrator…</summary>
        public static GiaPhaRenderResult ExportToSvg(
            string filePath,
            FamilyViewModel root,
            GiaPhaRenderOptions options = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }

            options = options ?? GiaPhaRenderOptions.ForFitContentPrint();
            var result = ComputeLayout(root, options);
            ExportResultToSvg(filePath, result);
            return result;
        }

        /// <summary>Xuất SVG từ layout đã tính (dùng cho kéo-thả chỉnh tay).</summary>
        public static void ExportResultToSvg(
            string filePath,
            GiaPhaRenderResult result,
            Func<int, PhaDoBoxStyle> boxStyleForFamilyId = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("filePath required", nameof(filePath));
            }
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            GiaPhaSvgExportService.Export(filePath, result, boxStyleForFamilyId);
        }

        /// <summary>In — khổ giấy = đúng kích thước cây (custom page size).</summary>
        public static GiaPhaRenderResult Print(
            FamilyViewModel root,
            GiaPhaRenderOptions options = null,
            string jobName = "Gia phả")
        {
            options = options ?? GiaPhaRenderOptions.ForFitContentPrint();
            var result = ComputeLayout(root, options);

            var canvas = new Canvas();
            PaintToCanvas(canvas, result);
            double w = result.CanvasWidthPixels;
            double h = result.CanvasHeightPixels;
            canvas.Measure(new Size(w, h));
            canvas.Arrange(new Rect(0, 0, w, h));
            canvas.UpdateLayout();

            var pd = new PrintDialog();
            try
            {
                var ticket = pd.PrintTicket ?? new PrintTicket();
                // Kích thước trang tùy chỉnh theo cây (đơn vị 1/96 inch)
                ticket.PageMediaSize = new PageMediaSize(w, h);
                ticket.PageOrientation = w >= h
                    ? PageOrientation.Landscape
                    : PageOrientation.Portrait;
                pd.PrintTicket = ticket;
            }
            catch
            {
                // Máy in có thể không hỗ trợ custom size — vẫn in theo visual.
            }

            if (pd.ShowDialog() == true)
            {
                pd.PrintVisual(canvas, jobName);
            }

            return result;
        }
    }
}
