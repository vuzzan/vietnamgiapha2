using System;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Khung SVG + nền — dùng chung cho ô gia đình và khối tiêu đề.</summary>
    public interface IPhaDoSvgFrameStyle
    {
        string ShapeSvgId { get; set; }
        string CustomShapeSvg { get; set; }
        double CustomShapeViewBoxWidth { get; set; }
        double CustomShapeViewBoxHeight { get; set; }
        string FillColorHex { get; set; }
    }

    public static class PhaDoSvgFrameStyleExtensions
    {
        public static bool HasCustomShape(this IPhaDoSvgFrameStyle style) =>
            style != null
            && (!string.IsNullOrWhiteSpace(style.CustomShapeSvg)
                || !string.IsNullOrWhiteSpace(style.ShapeSvgId));

        public static void ClearFrame(this IPhaDoSvgFrameStyle style)
        {
            if (style == null)
            {
                return;
            }

            style.ShapeSvgId = null;
            style.CustomShapeSvg = null;
        }

        public static void ApplyResolvedMarkup(
            this IPhaDoSvgFrameStyle style,
            string markup,
            double viewBoxWidth,
            double viewBoxHeight,
            string svgId = null)
        {
            if (style == null)
            {
                return;
            }

            style.ShapeSvgId = svgId;
            style.CustomShapeSvg = markup;
            style.CustomShapeViewBoxWidth = viewBoxWidth;
            style.CustomShapeViewBoxHeight = viewBoxHeight;
        }
    }
}
