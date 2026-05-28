using System;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Kiểu khối tiêu đề phả đồ (2 dòng trên cùng).</summary>
    public sealed class PhaDoTitleStyle : IPhaDoSvgFrameStyle
    {
        /// <summary>Dòng 1 — để trống thì dùng tên gia phả (GiaphaName).</summary>
        public string Line1Text { get; set; }

        /// <summary>Dòng 2 — để trống thì dùng ở tại (RF_OTAI).</summary>
        public string Line2Text { get; set; }

        public PhaDoPersonTextStyle Line1 { get; set; } = new PhaDoPersonTextStyle();
        public PhaDoPersonTextStyle Line2 { get; set; } = new PhaDoPersonTextStyle();

        public string FillColorHex { get; set; }

        public string ShapeSvgId { get; set; }
        public string CustomShapeSvg { get; set; }
        public double CustomShapeViewBoxWidth { get; set; } = 100;
        public double CustomShapeViewBoxHeight { get; set; } = 80;

        public bool HasCustomShape =>
            !string.IsNullOrWhiteSpace(CustomShapeSvg) || !string.IsNullOrWhiteSpace(ShapeSvgId);

        public PhaDoTitleStyle Clone()
        {
            return new PhaDoTitleStyle
            {
                Line1Text = Line1Text,
                Line2Text = Line2Text,
                Line1 = Line1?.Clone() ?? new PhaDoPersonTextStyle(),
                Line2 = Line2?.Clone() ?? new PhaDoPersonTextStyle(),
                FillColorHex = FillColorHex,
                ShapeSvgId = ShapeSvgId,
                CustomShapeSvg = CustomShapeSvg,
                CustomShapeViewBoxWidth = CustomShapeViewBoxWidth,
                CustomShapeViewBoxHeight = CustomShapeViewBoxHeight
            };
        }

        /// <summary>Bản sao lưu session — không nhúng markup nếu đã có ShapeSvgId.</summary>
        public PhaDoTitleStyle CloneForSession()
        {
            var clone = Clone();
            if (!string.IsNullOrWhiteSpace(clone.ShapeSvgId))
            {
                clone.CustomShapeSvg = null;
            }

            return clone;
        }
    }

    /// <summary>Áp dụng PhaDoTitleStyle → GiaPhaRenderOptions và tính chiều cao khối tiêu đề.</summary>
    public static class PhaDoTitleStyleResolver
    {
        public const double DefaultLine1FontPt = 18;
        public const double DefaultLine2FontPt = 12;

        public static void ApplyToOptions(
            GiaPhaRenderOptions options,
            PhaDoTitleStyle style,
            string giaphaName,
            string otai)
        {
            if (options == null)
            {
                return;
            }

            string line1 = !string.IsNullOrWhiteSpace(style?.Line1Text)
                ? style.Line1Text.Trim()
                : (giaphaName ?? "").Trim();
            string line2 = !string.IsNullOrWhiteSpace(style?.Line2Text)
                ? style.Line2Text.Trim()
                : (otai ?? "").Trim();

            options.Title = line1;
            options.TitleLine2 = line2;
            options.TitleFontPt = style?.Line1?.FontPt ?? DefaultLine1FontPt;
            options.TitleLine2FontPt = style?.Line2?.FontPt ?? DefaultLine2FontPt;
            options.TitleLine1FontFamily = string.IsNullOrWhiteSpace(style?.Line1?.FontFamilyName)
                ? options.FontFamilyName
                : style.Line1.FontFamilyName;
            options.TitleLine2FontFamily = string.IsNullOrWhiteSpace(style?.Line2?.FontFamilyName)
                ? options.FontFamilyName
                : style.Line2.FontFamilyName;
            options.TitleLine1ForegroundHex = style?.Line1?.ForegroundHex;
            options.TitleLine2ForegroundHex = style?.Line2?.ForegroundHex;
            options.TitleFillColorHex = style?.FillColorHex;
            options.TitleShapeSvgId = style?.ShapeSvgId;
            options.TitleCustomShapeSvg = style?.CustomShapeSvg;
            options.TitleCustomShapeViewBoxWidth = style?.CustomShapeViewBoxWidth ?? 100;
            options.TitleCustomShapeViewBoxHeight = style?.CustomShapeViewBoxHeight ?? 80;
        }

        public static double TitleBlockHeightMm(GiaPhaRenderOptions options)
        {
            if (options == null)
            {
                return 0;
            }

            double dpi = options.PrintDpi > 0 ? options.PrintDpi : 96;
            return PhaDoTitleBlockMetrics.Measure(options, dpi).HeightMm;
        }
    }
}
