using System;
using System.Collections.Generic;
using System.Linq;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>SVG một vùng trang trí quanh khối tiêu đề (Top/Bottom/Left/Right).</summary>
    public sealed class PhaDoTitleZoneSvg
    {
        public string SvgContent  { get; set; }
        public string SvgId       { get; set; }
        public double ViewBoxW    { get; set; } = 100;
        public double ViewBoxH    { get; set; } = 100;
        /// <summary>Kích thước theo chiều vuông góc với cạnh (mm) — chiều song song = toàn cạnh box.</summary>
        public double SizeMm      { get; set; } = 8;

        public PhaDoTitleZoneSvg Clone() => new PhaDoTitleZoneSvg
        {
            SvgContent = SvgContent, SvgId = SvgId,
            ViewBoxW = ViewBoxW, ViewBoxH = ViewBoxH, SizeMm = SizeMm
        };
        public bool HasContent => !string.IsNullOrWhiteSpace(SvgContent) || !string.IsNullOrWhiteSpace(SvgId);
    }

    /// <summary>Kiểu khối tiêu đề phả đồ — 4 dòng + 4 vùng SVG trang trí quanh box.</summary>
    public sealed class PhaDoTitleStyle : IPhaDoSvgFrameStyle
    {
        /// <summary>Dòng 1 — để trống thì dùng tên gia phả (GiaphaName).</summary>
        public string Line1Text { get; set; }

        /// <summary>Dòng 2 — để trống thì dùng ở tại (RF_OTAI).</summary>
        public string Line2Text { get; set; }

        // Dòng 3 + 4 tự động từ layout — không lưu trong style (populate lúc render)

        public PhaDoPersonTextStyle Line1 { get; set; } = new PhaDoPersonTextStyle();
        public PhaDoPersonTextStyle Line2 { get; set; } = new PhaDoPersonTextStyle();

        public string FillColorHex { get; set; }

        public string ShapeSvgId { get; set; }
        public string CustomShapeSvg { get; set; }
        public double CustomShapeViewBoxWidth { get; set; } = 100;
        public double CustomShapeViewBoxHeight { get; set; } = 80;

        /// <summary>Kích thước thủ công (mm) — 0 = tự tính. Lưu khi kéo resize title block.</summary>
        public double ManualWidthMm  { get; set; } = 0;
        public double ManualHeightMm { get; set; } = 0;
        public double ManualLeftMm   { get; set; } = 0;
        public double ManualTopMm    { get; set; } = 0;

        /// <summary>Đã đặt vị trí thủ công (kéo/resize) — cho phép Left/Top = 0, không quay về MarginMm.</summary>
        public bool ManualPositionSet { get; set; }

        /// <summary>Lệch từng dòng chữ (0–3) so với vị trí layout mặc định (mm).</summary>
        public Dictionary<int, PhaDoPersonLayoutOffset> LineOffsetsByIndex { get; set; }
            = new Dictionary<int, PhaDoPersonLayoutOffset>();

        // ── 4 vùng SVG trang trí quanh box ──────────────────────────────
        public PhaDoTitleZoneSvg TopZone    { get; set; }
        public PhaDoTitleZoneSvg BottomZone { get; set; }
        public PhaDoTitleZoneSvg LeftZone   { get; set; }
        public PhaDoTitleZoneSvg RightZone  { get; set; }

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
                CustomShapeViewBoxHeight = CustomShapeViewBoxHeight,
                ManualWidthMm  = ManualWidthMm,
                ManualHeightMm = ManualHeightMm,
                ManualLeftMm   = ManualLeftMm,
                ManualTopMm    = ManualTopMm,
                ManualPositionSet = ManualPositionSet,
                LineOffsetsByIndex = LineOffsetsByIndex?.ToDictionary(
                    kv => kv.Key,
                    kv => new PhaDoPersonLayoutOffset
                    {
                        DeltaXmm = kv.Value?.DeltaXmm ?? 0,
                        DeltaYmm = kv.Value?.DeltaYmm ?? 0
                    }) ?? new Dictionary<int, PhaDoPersonLayoutOffset>(),
                TopZone    = TopZone?.Clone(),
                BottomZone = BottomZone?.Clone(),
                LeftZone   = LeftZone?.Clone(),
                RightZone  = RightZone?.Clone()
            };
        }

        /// <summary>Bản sao lưu session — không nhúng markup nếu đã có ShapeSvgId.</summary>
        public PhaDoTitleStyle CloneForSession()
        {
            var clone = Clone();
            if (!string.IsNullOrWhiteSpace(clone.ShapeSvgId))
                clone.CustomShapeSvg = null;
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

            // Override kích thước / vị trí thủ công (kéo resize trên canvas)
            options.ManualTitleWidthMm  = style?.ManualWidthMm  ?? 0;
            options.ManualTitleHeightMm = style?.ManualHeightMm ?? 0;
            options.ManualTitleLeftMm   = style?.ManualLeftMm   ?? 0;
            options.ManualTitleTopMm    = style?.ManualTopMm    ?? 0;
            options.ManualTitlePositionSet = style?.ManualPositionSet ?? false;

            // 4 vùng SVG trang trí
            ApplyZone(options, style?.TopZone,    z => {
                options.TitleTopSvg = z.SvgContent; options.TitleTopSvgViewBoxW = z.ViewBoxW;
                options.TitleTopSvgViewBoxH = z.ViewBoxH; options.TitleTopSvgSizeMm = z.SizeMm; });
            ApplyZone(options, style?.BottomZone, z => {
                options.TitleBottomSvg = z.SvgContent; options.TitleBottomSvgViewBoxW = z.ViewBoxW;
                options.TitleBottomSvgViewBoxH = z.ViewBoxH; options.TitleBottomSvgSizeMm = z.SizeMm; });
            ApplyZone(options, style?.LeftZone,   z => {
                options.TitleLeftSvg = z.SvgContent; options.TitleLeftSvgViewBoxW = z.ViewBoxW;
                options.TitleLeftSvgViewBoxH = z.ViewBoxH; options.TitleLeftSvgSizeMm = z.SizeMm; });
            ApplyZone(options, style?.RightZone,  z => {
                options.TitleRightSvg = z.SvgContent; options.TitleRightSvgViewBoxW = z.ViewBoxW;
                options.TitleRightSvgViewBoxH = z.ViewBoxH; options.TitleRightSvgSizeMm = z.SizeMm; });
        }

        private static void ApplyZone(GiaPhaRenderOptions options, PhaDoTitleZoneSvg zone, Action<PhaDoTitleZoneSvg> apply)
        {
            if (zone != null && zone.HasContent) apply(zone);
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
