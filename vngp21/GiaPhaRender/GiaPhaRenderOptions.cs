using System;

namespace vietnamgiapha.GiaPhaRender
{
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

        public string FontFamilyName { get; set; } = "Segoe UI";

        public double TitleFontPt { get; set; } = 18;
        public double TitleLine2FontPt { get; set; } = 12;
        public string TitleLine1FontFamily { get; set; }
        public string TitleLine2FontFamily { get; set; }
        public string TitleLine1ForegroundHex { get; set; }
        public string TitleLine2ForegroundHex { get; set; }
        public string TitleFillColorHex { get; set; }
        public string TitleShapeSvgId { get; set; }
        public string TitleCustomShapeSvg { get; set; }
        public double TitleCustomShapeViewBoxWidth { get; set; } = 100;
        public double TitleCustomShapeViewBoxHeight { get; set; } = 80;
        public double HeaderFontPt { get; set; } = 7;
        /// <summary>Nhãn "Đời X" trên thẻ dọc — lớn hơn HeaderFontPt để dễ đọc.</summary>
        public double VerticalGenerationLabelFontPt { get; set; } = 10;
        public double MainNameFontPt { get; set; } = 9;
        public double SpouseFontPt { get; set; } = 7.5;

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
