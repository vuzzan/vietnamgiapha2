using System;
using System.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>
    /// Sau offset kéo / resize ô — mở rộng (và nếu cần dịch) khổ canvas để mọi node nằm trong vùng cuộn.
    /// </summary>
    public static class GiaPhaRenderBoundsFitter
    {
        private const double ExtraPaddingMm = 1.5;

        public static void FitCanvasToContent(GiaPhaRenderResult result)
        {
            if (result?.Nodes == null || result.Nodes.Count == 0)
            {
                return;
            }

            var options = result.Options ?? GiaPhaRenderOptions.ForFitContent();
            double margin = options.MarginMm;
            double titleH = PhaDoTitleStyleResolver.TitleBlockHeightMm(options);
            double minContentTop = margin + titleH;

            if (!TryMeasureContentBounds(result, out double minX, out double minY, out double maxX, out double maxY))
            {
                return;
            }

            double shiftX = minX < margin ? margin - minX : 0;
            double shiftY = minY < minContentTop ? minContentTop - minY : 0;
            if (Math.Abs(shiftX) > 0.001 || Math.Abs(shiftY) > 0.001)
            {
                ShiftContent(result, shiftX, shiftY);
                minX += shiftX;
                maxX += shiftX;
                minY += shiftY;
                maxY += shiftY;
            }

            double canvasWmm = maxX + margin + ExtraPaddingMm;
            double canvasHmm = maxY + margin + ExtraPaddingMm;
            canvasWmm = Math.Max(canvasWmm, margin * 2 + ExtraPaddingMm);
            canvasHmm = Math.Max(canvasHmm, minContentTop + ExtraPaddingMm);

            result.PageWidthMm = canvasWmm;
            result.PageHeightMm = canvasHmm;
            result.CanvasWidthPixels = PrintUnits.MmToPixels(canvasWmm, result.Dpi);
            result.CanvasHeightPixels = PrintUnits.MmToPixels(canvasHmm, result.Dpi);
            result.ContentWidthMm = Math.Max(0, maxX - minX);
            result.ContentHeightMm = Math.Max(0, maxY - minY);
        }

        private static bool TryMeasureContentBounds(
            GiaPhaRenderResult result,
            out double minX,
            out double minY,
            out double maxX,
            out double maxY)
        {
            minX = double.MaxValue;
            minY = double.MaxValue;
            maxX = double.MinValue;
            maxY = double.MinValue;

            foreach (var node in result.Nodes)
            {
                if (node?.Metrics == null)
                {
                    continue;
                }

                double bottomMm = node.Ymm + Math.Max(node.Metrics.HeightMm, node.Metrics.SlotHeightMm);
                minX = Math.Min(minX, node.Xmm);
                minY = Math.Min(minY, node.Ymm);
                maxX = Math.Max(maxX, node.Xmm + node.Metrics.WidthMm);
                maxY = Math.Max(maxY, bottomMm);
            }

            foreach (var connector in result.Connectors)
            {
                minX = Math.Min(minX, Math.Min(connector.X1mm, connector.X2mm));
                minY = Math.Min(minY, Math.Min(connector.Y1mm, connector.Y2mm));
                maxX = Math.Max(maxX, Math.Max(connector.X1mm, connector.X2mm));
                maxY = Math.Max(maxY, Math.Max(connector.Y1mm, connector.Y2mm));
            }

            foreach (var band in result.GenerationBands)
            {
                minY = Math.Min(minY, band.Ymm);
                maxY = Math.Max(maxY, band.Ymm + band.HeightMm);
            }

            if (minX == double.MaxValue)
            {
                minX = minY = maxX = maxY = 0;
                return false;
            }

            return true;
        }

        private static void ShiftContent(GiaPhaRenderResult result, double shiftXmm, double shiftYmm)
        {
            foreach (var node in result.Nodes)
            {
                node.Xmm += shiftXmm;
                node.Ymm += shiftYmm;
            }

            foreach (var connector in result.Connectors)
            {
                connector.X1mm += shiftXmm;
                connector.Y1mm += shiftYmm;
                connector.X2mm += shiftXmm;
                connector.Y2mm += shiftYmm;
            }

            foreach (var band in result.GenerationBands)
            {
                band.Ymm += shiftYmm;
            }
        }
    }
}
