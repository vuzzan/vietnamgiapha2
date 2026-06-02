using System;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Kiểu đường liên kết giữa gia đình cha và con.</summary>
    public enum GiaPhaConnectorPathType
    {
        Orthogonal = 0,
        Curved = 1
    }

    /// <summary>Kiểu bố cục ô một gia đình.</summary>
    public enum GiaPhaCardLayoutMode
    {
        /// <summary>Thẻ rộng, tên xếp nhiều dòng ngang trong ô (mặc định).</summary>
        Horizontal = 0,

        /// <summary>Thẻ hẹp, mỗi người một cột — từng ký tự xếp dọc.</summary>
        Vertical = 1,

        /// <summary>Thẻ dọc kiểu Word: mỗi người một cột, mỗi từ một dòng ngang xếp dọc.</summary>
        VerticalWord = 2
    }

    public sealed class GiaPhaRenderOptions
    {
        /// <summary>Chiều rộng trang cố định (mm) — chỉ khi FitContentToTree = false.</summary>
        public double PageWidthMm { get; set; } = A0PrintSpecification.LandscapeWidthMm;

        /// <summary>Chiều cao trang cố định (mm) — chỉ khi FitContentToTree = false.</summary>
        public double PageHeightMm { get; set; } = A0PrintSpecification.LandscapeHeightMm;

        public double MarginMm { get; set; } = 15.0;
        public double PrintDpi { get; set; } = 150.0;

        /// <summary>
        /// Khổ canvas/in = đúng kích thước cây (100%, không thu nhỏ theo A0).
        /// Mỗi gia phả có Width×Height riêng.
        /// </summary>
        public bool FitContentToTree { get; set; } = true;

        /// <summary>Thu nhỏ nếu vượt PageWidth/PageHeight — chỉ khi FitContentToTree = false.</summary>
        public bool ScaleToFitPage { get; set; }

        public bool CenterContentOnPage { get; set; }

        public double HorizontalGapMm { get; set; } = 10.0;
        public double GenerationGapMm { get; set; } = 24.0;
        public double BusLineGapMm { get; set; } = 14.0;

        /// <summary>Độ dài tối thiểu đường bus ngang (mm) — đời chỉ có 1 con vẫn thấy gạch ngang.</summary>
        public double MinBusSpanMm { get; set; } = 14.0;

        /// <summary>Ngang (rộng) hoặc Dọc (hẹp, tiết kiệm width).</summary>
        public GiaPhaCardLayoutMode CardLayoutMode { get; set; } = GiaPhaCardLayoutMode.Horizontal;
        public GiaPhaConnectorPathType ConnectorPathType { get; set; } = GiaPhaConnectorPathType.Orthogonal;

        public double CardMinWidthMm { get; set; } = 26.0;
        public double CardMaxWidthMm { get; set; } = 72.0;

        /// <summary>Hệ số thu chiều rộng ô theo ước lượng chữ (≈0,58 — ô vừa khít, tránh rộng ~180%).</summary>
        public double CardWidthTextFactor { get; set; } = 0.58;

        /// <summary>Thẻ dọc: chiều rộng tối thiểu / tối đa (mm).</summary>
        public double CardVerticalMinWidthMm { get; set; } = 20.0;
        public double CardVerticalMaxWidthMm { get; set; } = 36.0;
        public double CardPaddingMm { get; set; } = 2.5;
        public double CardLineHeightMm { get; set; } = 5.0;
        public double CardHeaderHeightMm { get; set; } = 6.0;
        public double CardHeightSafetyFactor { get; set; } = 1.28;
        public double CardBottomPaddingMm { get; set; } = 2.0;

        public int MaxSpouseLinesShown { get; set; } = 4;
        public string Title { get; set; } = "";

        /// <summary>Dòng 2 khối tiêu đề (ở tại / OTAI).</summary>
        public string TitleLine2 { get; set; } = "";

        /// <summary>Dòng 3 — tự động: số gia đình + số người (vd "120 gia đình · 380 người").</summary>
        public string TitleLine3 { get; set; } = "";

        /// <summary>Dòng 4 — tự động: kích thước phả (vd "245.6 cm × 112.3 cm").</summary>
        public string TitleLine4 { get; set; } = "";

        /// <summary>Override chiều rộng khối tiêu đề (mm) — 0 = tự tính theo chữ.</summary>
        public double ManualTitleWidthMm { get; set; } = 0;
        /// <summary>Override chiều cao khối tiêu đề (mm) — 0 = tự tính theo dòng.</summary>
        public double ManualTitleHeightMm { get; set; } = 0;
        /// <summary>Override vị trí trái (mm) — chỉ dùng khi ManualTitlePositionSet.</summary>
        public double ManualTitleLeftMm { get; set; } = 0;
        /// <summary>Override vị trí trên (mm) — chỉ dùng khi ManualTitlePositionSet.</summary>
        public double ManualTitleTopMm { get; set; } = 0;
        /// <summary>Đã kéo/resize vị trí title — cho phép sát mép trái (Left = 0).</summary>
        public bool ManualTitlePositionSet { get; set; }

        // ── 4 vùng SVG trang trí quanh khối tiêu đề ─────────────────────
        public string TitleTopSvg    { get; set; } public double TitleTopSvgViewBoxW    { get; set; } = 100; public double TitleTopSvgViewBoxH    { get; set; } = 100; public double TitleTopSvgSizeMm    { get; set; } = 8;
        public string TitleBottomSvg { get; set; } public double TitleBottomSvgViewBoxW { get; set; } = 100; public double TitleBottomSvgViewBoxH { get; set; } = 100; public double TitleBottomSvgSizeMm { get; set; } = 8;
        public string TitleLeftSvg   { get; set; } public double TitleLeftSvgViewBoxW   { get; set; } = 100; public double TitleLeftSvgViewBoxH   { get; set; } = 100; public double TitleLeftSvgSizeMm   { get; set; } = 8;
        public string TitleRightSvg  { get; set; } public double TitleRightSvgViewBoxW  { get; set; } = 100; public double TitleRightSvgViewBoxH  { get; set; } = 100; public double TitleRightSvgSizeMm  { get; set; } = 8;

        public string FontFamilyName { get; set; } = "Segoe UI";

        public double TitleFontPt { get; set; } = 18;          // dòng 1
        public double TitleLine2FontPt { get; set; } = 12;      // dòng 2
        public double TitleLine3FontPt { get; set; } = 0;        // 0 = tự tính ~ 0.78*Line2
        public double TitleLine4FontPt { get; set; } = 0;
        public string TitleLine1FontFamily { get; set; }
        public string TitleLine2FontFamily { get; set; }
        public string TitleLine3FontFamily { get; set; }
        public string TitleLine4FontFamily { get; set; }
        public string TitleLine1ForegroundHex { get; set; }
        public string TitleLine2ForegroundHex { get; set; }
        public string TitleLine3ForegroundHex { get; set; }     // null = "#888888"
        public string TitleLine4ForegroundHex { get; set; }
        public string TitleFillColorHex { get; set; }
        public string TitleShapeSvgId { get; set; }
        public string TitleCustomShapeSvg { get; set; }
        public double TitleCustomShapeViewBoxWidth { get; set; } = 100;
        public double TitleCustomShapeViewBoxHeight { get; set; } = 80;
        public double HeaderFontPt { get; set; } = 7;
        /// <summary>Nhãn "Đời X" trên thẻ dọc — lớn hơn HeaderFontPt để dễ đọc.</summary>
        public double VerticalGenerationLabelFontPt { get; set; } = 10;
        /// <summary>Style tùy chỉnh nhãn "Đời X" — null = dùng mặc định theo layout mode.</summary>
        public PhaDoGenLabelStyle GenLabelStyle { get; set; }
        public double MainNameFontPt { get; set; } = 9;
        public double SpouseFontPt { get; set; } = 7.5;
        /// <summary>Ghi chú trong ô (phả con, kích thước cm…).</summary>
        public double NoteFontPt { get; set; } = 6.5;
        /// <summary>Khoảng trống trước khối ghi chú (mm).</summary>
        public double CardNoteTopGapMm { get; set; } = 1.5;

        /// <summary>Ghi chú theo FamilyId — null nếu không có.</summary>
        public System.Func<int, System.Collections.Generic.IReadOnlyList<string>> GetFamilyBoxNotes { get; set; }

        /// <summary>
        /// Nhãn hiển thị trong box gốc ảo của phả con đa gốc (FamilyId &lt; 0).
        /// Ví dụ: "Non-STOP [1/28] | đời 11 | 12 nhánh | ~197 GD".
        /// </summary>
        public string MultiRootScopeLabel { get; set; }

        public double ContentWidthMm => PageWidthMm - 2 * MarginMm;
        public double ContentHeightMm => PageHeightMm - 2 * MarginMm;

        /// <summary>Khổ tự động theo cây — mặc định cho xem + in.</summary>
        public static GiaPhaRenderOptions ForFitContent(double dpi = 96)
        {
            return new GiaPhaRenderOptions
            {
                PrintDpi = dpi,
                FitContentToTree = true,
                ScaleToFitPage = false,
                CenterContentOnPage = false,
                MarginMm = 15,
                HorizontalGapMm = 10,
                GenerationGapMm = 32,
                BusLineGapMm = 16,
                CardMinWidthMm = 26,
                CardWidthTextFactor = 0.58,
                CardHeightSafetyFactor = 1.32
            };
        }

        /// <summary>In ấn chất lượng cao — vẫn khổ theo cây, DPI 150.</summary>
        public static GiaPhaRenderOptions ForFitContentPrint()
        {
            var o = ForFitContent(150);
            o.PrintDpi = 150;
            return o;
        }

        /// <summary>Áp thông số thẻ dọc (hẹp) lên options hiện có.</summary>
        public static void ApplyVerticalCardLayout(GiaPhaRenderOptions options)
        {
            if (options == null)
            {
                return;
            }

            options.CardLayoutMode = GiaPhaCardLayoutMode.Vertical;
            options.CardMinWidthMm = options.CardVerticalMinWidthMm;
            options.CardMaxWidthMm = options.CardVerticalMaxWidthMm;
            options.HorizontalGapMm = Math.Max(8, options.HorizontalGapMm * 0.65);
            options.VerticalGenerationLabelFontPt = 10;
        }

        /// <summary>Thẻ dọc tách theo từ (Word) — vẫn hẹp như Vertical.</summary>
        public static void ApplyVerticalWordCardLayout(GiaPhaRenderOptions options)
        {
            ApplyVerticalCardLayout(options);
            options.CardLayoutMode = GiaPhaCardLayoutMode.VerticalWord;
        }

        /// <summary>Có phải chế độ thẻ dọc (ký tự hoặc Word) không.</summary>
        public static bool IsVerticalCardLayout(GiaPhaCardLayoutMode mode)
        {
            return mode == GiaPhaCardLayoutMode.Vertical
                || mode == GiaPhaCardLayoutMode.VerticalWord;
        }

        /// <summary>Khổ A0 cố định (tùy chọn, không dùng mặc định).</summary>
        public static GiaPhaRenderOptions ForA0LandscapePrint()
        {
            return new GiaPhaRenderOptions
            {
                FitContentToTree = false,
                ScaleToFitPage = true,
                CenterContentOnPage = true,
                PageWidthMm = A0PrintSpecification.LandscapeWidthMm,
                PageHeightMm = A0PrintSpecification.LandscapeHeightMm,
                PrintDpi = A0PrintSpecification.DefaultPrintDpi
            };
        }

        [System.Obsolete("Dùng ForFitContent")]
        public static GiaPhaRenderOptions ForScreenPreview(double dpi = 96) => ForFitContent(dpi);
    }
}
