using System;
using System.Globalization;
using System.Windows;
using MahApps.Metro.Controls;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    public partial class SettingsDialog : MetroWindow
    {
        public GiaPhaLayoutSettings Settings { get; private set; }

        public SettingsDialog(GiaPhaLayoutSettings initial)
        {
            InitializeComponent();
            Settings = initial?.Clone() ?? GiaPhaLayoutSettings.CreateDefault();
            LoadToUi(Settings);
        }

        private void LoadToUi(GiaPhaLayoutSettings s)
        {
            marginCmBox.Text = FormatCm(s.MarginCm);
            horizontalGapCmBox.Text = FormatCm(s.HorizontalGapCm);
            generationGapCmBox.Text = FormatCm(s.GenerationGapCm);
            busLineGapCmBox.Text = FormatCm(s.BusLineGapCm);
            minBusSpanCmBox.Text = FormatCm(s.MinBusSpanCm);

            cardMinWidthCmBox.Text = FormatCm(s.CardMinWidthCm);
            cardMaxWidthCmBox.Text = FormatCm(s.CardMaxWidthCm);
            cardPaddingCmBox.Text = FormatCm(s.CardPaddingCm);
            cardLineHeightCmBox.Text = FormatCm(s.CardLineHeightCm);
            cardHeaderHeightCmBox.Text = FormatCm(s.CardHeaderHeightCm);
            cardBottomPaddingCmBox.Text = FormatCm(s.CardBottomPaddingCm);
            cardWidthTextFactorBox.Text = FormatFactor(s.CardWidthTextFactor);
            cardHeightSafetyFactorBox.Text = FormatFactor(s.CardHeightSafetyFactor);

            cardVerticalMinWidthCmBox.Text = FormatCm(s.CardVerticalMinWidthCm);
            cardVerticalMaxWidthCmBox.Text = FormatCm(s.CardVerticalMaxWidthCm);

            headerFontPtBox.Text = FormatPt(s.HeaderFontPt);
            mainNameFontPtBox.Text = FormatPt(s.MainNameFontPt);
            spouseFontPtBox.Text = FormatPt(s.SpouseFontPt);
            verticalGenFontPtBox.Text = FormatPt(s.VerticalGenerationLabelFontPt);
            maxSpouseLinesBox.Text = s.MaxSpouseLinesShown.ToString(CultureInfo.InvariantCulture);

            statusText.Text = "";
        }

        private bool TryReadFromUi(out GiaPhaLayoutSettings settings, out string error)
        {
            settings = Settings?.Clone() ?? GiaPhaLayoutSettings.CreateDefault();
            error = null;

            if (!TryReadCm(marginCmBox.Text, "Lề trang", 0.1, 10, out double marginCm, ref error)
                || !TryReadCm(horizontalGapCmBox.Text, "Khoảng cách ngang", 0.1, 5, out double horizontalGapCm, ref error)
                || !TryReadCm(generationGapCmBox.Text, "Khoảng cách đời", 0.5, 15, out double generationGapCm, ref error)
                || !TryReadCm(busLineGapCmBox.Text, "Khoảng bus", 0.1, 5, out double busLineGapCm, ref error)
                || !TryReadCm(minBusSpanCmBox.Text, "Bus ngang tối thiểu", 0.1, 5, out double minBusSpanCm, ref error)
                || !TryReadCm(cardMinWidthCmBox.Text, "Rộng tối thiểu ô", 0.5, 20, out double cardMinWidthCm, ref error)
                || !TryReadCm(cardMaxWidthCmBox.Text, "Rộng tối đa ô", 1, 30, out double cardMaxWidthCm, ref error)
                || !TryReadCm(cardPaddingCmBox.Text, "Padding ô", 0, 2, out double cardPaddingCm, ref error)
                || !TryReadCm(cardLineHeightCmBox.Text, "Chiều cao dòng", 0.1, 3, out double cardLineHeightCm, ref error)
                || !TryReadCm(cardHeaderHeightCmBox.Text, "Chiều cao header", 0.1, 3, out double cardHeaderHeightCm, ref error)
                || !TryReadCm(cardBottomPaddingCmBox.Text, "Padding đáy", 0, 2, out double cardBottomPaddingCm, ref error)
                || !TryReadFactor(cardWidthTextFactorBox.Text, "Hệ số thu rộng", 0.3, 1.5, out double cardWidthTextFactor, ref error)
                || !TryReadFactor(cardHeightSafetyFactorBox.Text, "Hệ số cao", 1.0, 2.0, out double cardHeightSafetyFactor, ref error)
                || !TryReadCm(cardVerticalMinWidthCmBox.Text, "Rộng tối thiểu (dọc)", 0.5, 15, out double cardVerticalMinWidthCm, ref error)
                || !TryReadCm(cardVerticalMaxWidthCmBox.Text, "Rộng tối đa (dọc)", 0.5, 20, out double cardVerticalMaxWidthCm, ref error)
                || !TryReadPt(headerFontPtBox.Text, "Cỡ chữ header", 4, 24, out double headerFontPt, ref error)
                || !TryReadPt(mainNameFontPtBox.Text, "Cỡ chữ chủ", 4, 24, out double mainNameFontPt, ref error)
                || !TryReadPt(spouseFontPtBox.Text, "Cỡ chữ phối", 4, 24, out double spouseFontPt, ref error)
                || !TryReadPt(verticalGenFontPtBox.Text, "Cỡ chữ Đời (dọc)", 4, 24, out double verticalGenFontPt, ref error)
                || !TryReadInt(maxSpouseLinesBox.Text, "Số dòng phối", 1, 20, out int maxSpouseLines, ref error))
            {
                return false;
            }

            if (cardMinWidthCm > cardMaxWidthCm)
            {
                error = "Rộng tối thiểu ô không được lớn hơn rộng tối đa.";
                return false;
            }

            if (cardVerticalMinWidthCm > cardVerticalMaxWidthCm)
            {
                error = "Rộng tối thiểu (dọc) không được lớn hơn rộng tối đa (dọc).";
                return false;
            }

            settings.MarginCm = marginCm;
            settings.HorizontalGapCm = horizontalGapCm;
            settings.GenerationGapCm = generationGapCm;
            settings.BusLineGapCm = busLineGapCm;
            settings.MinBusSpanCm = minBusSpanCm;
            settings.CardMinWidthCm = cardMinWidthCm;
            settings.CardMaxWidthCm = cardMaxWidthCm;
            settings.CardPaddingCm = cardPaddingCm;
            settings.CardLineHeightCm = cardLineHeightCm;
            settings.CardHeaderHeightCm = cardHeaderHeightCm;
            settings.CardBottomPaddingCm = cardBottomPaddingCm;
            settings.CardWidthTextFactor = cardWidthTextFactor;
            settings.CardHeightSafetyFactor = cardHeightSafetyFactor;
            settings.CardVerticalMinWidthCm = cardVerticalMinWidthCm;
            settings.CardVerticalMaxWidthCm = cardVerticalMaxWidthCm;
            settings.HeaderFontPt = headerFontPt;
            settings.MainNameFontPt = mainNameFontPt;
            settings.SpouseFontPt = spouseFontPt;
            settings.VerticalGenerationLabelFontPt = verticalGenFontPt;
            settings.MaxSpouseLinesShown = maxSpouseLines;
            return true;
        }

        private void ResetDefaults_Click(object sender, RoutedEventArgs e)
        {
            LoadToUi(GiaPhaLayoutSettings.CreateDefault());
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadFromUi(out var settings, out string error))
            {
                statusText.Text = error ?? "Giá trị không hợp lệ.";
                return;
            }

            Settings = settings;
            DialogResult = true;
            Close();
        }

        private static string FormatCm(double cm)
        {
            return cm.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatFactor(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatPt(double pt)
        {
            return pt.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private static bool TryReadCm(string text, string label, double min, double max, out double cm, ref string error)
        {
            cm = 0;
            if (!TryParseNumber(text, out cm))
            {
                error = label + ": nhập số (cm), ví dụ 1,2 hoặc 1.2";
                return false;
            }

            if (cm < min || cm > max)
            {
                error = label + ": giá trị từ " + FormatCm(min) + " đến " + FormatCm(max) + " cm.";
                return false;
            }

            return true;
        }

        private static bool TryReadFactor(string text, string label, double min, double max, out double value, ref string error)
        {
            value = 0;
            if (!TryParseNumber(text, out value))
            {
                error = label + ": nhập số, ví dụ 0,58";
                return false;
            }

            if (value < min || value > max)
            {
                error = label + ": giá trị từ " + FormatFactor(min) + " đến " + FormatFactor(max) + ".";
                return false;
            }

            return true;
        }

        private static bool TryReadPt(string text, string label, double min, double max, out double pt, ref string error)
        {
            pt = 0;
            if (!TryParseNumber(text, out pt))
            {
                error = label + ": nhập số (pt).";
                return false;
            }

            if (pt < min || pt > max)
            {
                error = label + ": từ " + FormatPt(min) + " đến " + FormatPt(max) + " pt.";
                return false;
            }

            return true;
        }

        private static bool TryReadInt(string text, string label, int min, int max, out int value, ref string error)
        {
            value = 0;
            if (!int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && !int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.GetCultureInfo("vi-VN"), out value))
            {
                error = label + ": nhập số nguyên.";
                return false;
            }

            if (value < min || value > max)
            {
                error = label + ": từ " + min + " đến " + max + ".";
                return false;
            }

            return true;
        }

        private static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim().Replace(',', '.');
            return double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }
    }
}
