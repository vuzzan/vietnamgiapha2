using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    public partial class PhaDoTitleStyleDialog : MetroWindow
    {
        public PhaDoTitleStyle ResultStyle { get; private set; }

        /// <summary>Khung mới từ editor — sau Áp dụng có thể hỏi lưu catalog.</summary>
        public bool IsNewSvgFromEditor { get; private set; }

        private readonly double _defaultLine1Pt;
        private readonly double _defaultLine2Pt;
        private readonly List<PhaDoSvgFrameListItem> _svgFrameItems = new List<PhaDoSvgFrameListItem>();
        private PhaDoBoxSvgSanitizeResult _lastSvgSanitize;
        private readonly DispatcherTimer _svgAutoCheckTimer;
        private bool _suppressSvgAutoRefresh;
        private bool _suppressSvgFrameSelectionChanged;

        public PhaDoTitleStyleDialog(
            PhaDoTitleStyle current,
            GiaPhaRenderOptions renderDefaults,
            IDictionary<string, PhaDoSvgShape> svgCatalog)
        {
            InitializeComponent();

            _defaultLine1Pt = renderDefaults?.TitleFontPt ?? PhaDoTitleStyleResolver.DefaultLine1FontPt;
            _defaultLine2Pt = renderDefaults?.TitleLine2FontPt ?? PhaDoTitleStyleResolver.DefaultLine2FontPt;

            fillColorCombo.ItemsSource = PhaDoBoxStyleDialog.PresetFillColors;
            line1ColorCombo.ItemsSource = PhaDoBoxStyleDialog.PresetTextColors;
            line2ColorCombo.ItemsSource = PhaDoBoxStyleDialog.PresetTextColors;

            var fonts = PhaDoBoxStyleDialog.PresetFontFamilies.ToList();
            line1FontCombo.ItemsSource = fonts;
            line2FontCombo.ItemsSource = fonts;

            _svgAutoCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            _svgAutoCheckTimer.Tick += SvgAutoCheckTimer_Tick;
            customSvgBox.TextChanged += CustomSvgBox_TextChanged;
            fillColorCombo.SelectionChanged += FillColorCombo_SelectionChanged;

            RebuildSvgFrameList(svgCatalog);
            LoadForm(current ?? new PhaDoTitleStyle());
        }

        private void RebuildSvgFrameList(IDictionary<string, PhaDoSvgShape> catalog)
        {
            _svgFrameItems.Clear();
            _svgFrameItems.AddRange(PhaDoSvgFrameListBuilder.Build(catalog));
            _suppressSvgFrameSelectionChanged = true;
            try
            {
                svgFrameCombo.ItemsSource = null;
                svgFrameCombo.ItemsSource = _svgFrameItems;
            }
            finally
            {
                _suppressSvgFrameSelectionChanged = false;
            }
        }

        private void LoadForm(PhaDoTitleStyle style)
        {
            _suppressSvgAutoRefresh = true;
            _suppressSvgFrameSelectionChanged = true;
            try
            {
                line1TextBox.Text = style.Line1Text ?? "";
                line2TextBox.Text = style.Line2Text ?? "";
                fillColorCombo.SelectedIndex = IndexForFill(style.FillColorHex);
                SetPersonRow(style.Line1, line1FontCombo, line1SizeBox, line1ColorCombo, _defaultLine1Pt);
                SetPersonRow(style.Line2, line2FontCombo, line2SizeBox, line2ColorCombo, _defaultLine2Pt);
                customSvgBox.Text = style.CustomShapeSvg ?? "";

                if (!string.IsNullOrWhiteSpace(style.ShapeSvgId))
                {
                    SelectFrameBySvgId(style.ShapeSvgId);
                }
                else if (!string.IsNullOrWhiteSpace(style.CustomShapeSvg))
                {
                    SelectFrameKind(PhaDoSvgFrameListItem.FrameKind.CreateNew);
                }
                else
                {
                    SelectFrameKind(PhaDoSvgFrameListItem.FrameKind.DefaultRect);
                }
            }
            finally
            {
                _suppressSvgAutoRefresh = false;
                _suppressSvgFrameSelectionChanged = false;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadForm(out var style))
            {
                return;
            }

            var frame = GetSelectedFrameItem();
            IsNewSvgFromEditor = frame?.Kind == PhaDoSvgFrameListItem.FrameKind.CreateNew
                && !string.IsNullOrWhiteSpace(style.CustomShapeSvg);

            ResultStyle = style;
            DialogResult = true;
            Close();
        }

        private bool TryReadForm(out PhaDoTitleStyle style)
        {
            style = new PhaDoTitleStyle
            {
                Line1Text = string.IsNullOrWhiteSpace(line1TextBox.Text) ? null : line1TextBox.Text.Trim(),
                Line2Text = string.IsNullOrWhiteSpace(line2TextBox.Text) ? null : line2TextBox.Text.Trim()
            };

            var fill = fillColorCombo.SelectedItem as PhaDoBoxStyleDialog.ColorOption;
            if (fill != null && !fill.IsDefault)
            {
                style.FillColorHex = fill.Hex;
            }

            if (!TryReadPersonRow(line1FontCombo, line1SizeBox, line1ColorCombo, _defaultLine1Pt, out var line1))
            {
                return false;
            }

            if (!TryReadPersonRow(line2FontCombo, line2SizeBox, line2ColorCombo, _defaultLine2Pt, out var line2))
            {
                return false;
            }

            style.Line1 = line1;
            style.Line2 = line2;

            return TryApplySvgFrameToStyle(style);
        }

        private bool TryApplySvgFrameToStyle(PhaDoTitleStyle style)
        {
            var frame = GetSelectedFrameItem();
            if (frame == null)
            {
                return true;
            }

            switch (frame.Kind)
            {
                case PhaDoSvgFrameListItem.FrameKind.DefaultRect:
                    style.ClearFrame();
                    return true;

                case PhaDoSvgFrameListItem.FrameKind.Catalog:
                    if (frame.Shape == null)
                    {
                        MessageBox.Show("Không đọc được khung đã chọn.", "Khung SVG");
                        return false;
                    }

                    string markup = frame.Shape.GetSvgMarkup();
                    if (string.IsNullOrWhiteSpace(markup))
                    {
                        MessageBox.Show("SVG trong catalog không hợp lệ.", "Khung SVG");
                        return false;
                    }

                    style.ApplyResolvedMarkup(markup, frame.Shape.ViewBoxWidth, frame.Shape.ViewBoxHeight, frame.SvgId);
                    return true;

                case PhaDoSvgFrameListItem.FrameKind.CreateNew:
                    return TryApplyNewSvgFromEditor(style);

                default:
                    return true;
            }
        }

        private bool TryApplyNewSvgFromEditor(PhaDoTitleStyle style)
        {
            string raw = customSvgBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
            {
                style.ClearFrame();
                return true;
            }

            var sanitized = PhaDoBoxSvgSanitizer.Sanitize(raw);
            _lastSvgSanitize = sanitized;
            UpdateSvgStatus(sanitized);
            UpdateSvgPreviewVisual();

            if (!sanitized.Success)
            {
                MessageBox.Show(sanitized.Message ?? "SVG không hợp lệ.", "Khung SVG");
                return false;
            }

            style.ApplyResolvedMarkup(sanitized.SanitizedSvgMarkup, sanitized.ViewBoxWidth, sanitized.ViewBoxHeight);
            return true;
        }

        private PhaDoSvgFrameListItem GetSelectedFrameItem() =>
            svgFrameCombo?.SelectedItem as PhaDoSvgFrameListItem;

        private void SvgFrameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSvgFrameSelectionChanged)
            {
                return;
            }

            ApplySvgFrameSelection();
        }

        private void ApplySvgFrameSelection()
        {
            var item = GetSelectedFrameItem();
            if (item == null)
            {
                return;
            }

            switch (item.Kind)
            {
                case PhaDoSvgFrameListItem.FrameKind.DefaultRect:
                    newSvgPanel.Visibility = Visibility.Collapsed;
                    _lastSvgSanitize = PhaDoBoxSvgSanitizer.Sanitize("");
                    UpdateSvgStatus(_lastSvgSanitize);
                    UpdateSvgPreviewVisual();
                    break;

                case PhaDoSvgFrameListItem.FrameKind.Catalog:
                    newSvgPanel.Visibility = Visibility.Collapsed;
                    PreviewCatalogShape(item.Shape);
                    break;

                case PhaDoSvgFrameListItem.FrameKind.CreateNew:
                    newSvgPanel.Visibility = Visibility.Visible;
                    if (string.IsNullOrWhiteSpace(customSvgBox.Text))
                    {
                        _lastSvgSanitize = PhaDoBoxSvgSanitizer.Sanitize("");
                        UpdateSvgStatus(_lastSvgSanitize);
                        UpdateSvgPreviewVisual();
                    }
                    else
                    {
                        RunSvgValidateAndPreview();
                    }

                    customSvgBox.Focus();
                    break;
            }
        }

        private void PreviewCatalogShape(PhaDoSvgShape shape)
        {
            if (shape == null)
            {
                _lastSvgSanitize = new PhaDoBoxSvgSanitizeResult { Success = false, Message = "Không đọc được khung." };
            }
            else
            {
                string markup = shape.GetSvgMarkup();
                _lastSvgSanitize = string.IsNullOrWhiteSpace(markup)
                    ? new PhaDoBoxSvgSanitizeResult { Success = false, Message = "SVG không hợp lệ." }
                    : new PhaDoBoxSvgSanitizeResult
                    {
                        Success = true,
                        SanitizedSvgMarkup = markup,
                        ViewBoxWidth = shape.ViewBoxWidth,
                        ViewBoxHeight = shape.ViewBoxHeight,
                        Message = "Khung: " + (shape.SvgId ?? "")
                    };
            }

            UpdateSvgStatus(_lastSvgSanitize);
            UpdateSvgPreviewVisual();
        }

        private void SelectFrameBySvgId(string svgId)
        {
            if (string.IsNullOrWhiteSpace(svgId))
            {
                SelectFrameKind(PhaDoSvgFrameListItem.FrameKind.DefaultRect);
                return;
            }

            var item = _svgFrameItems.FirstOrDefault(
                i => i.Kind == PhaDoSvgFrameListItem.FrameKind.Catalog
                    && string.Equals(i.SvgId, svgId, StringComparison.Ordinal));
            if (item != null)
            {
                _suppressSvgFrameSelectionChanged = true;
                try
                {
                    svgFrameCombo.SelectedItem = item;
                }
                finally
                {
                    _suppressSvgFrameSelectionChanged = false;
                }

                ApplySvgFrameSelection();
                return;
            }

            SelectFrameKind(PhaDoSvgFrameListItem.FrameKind.CreateNew);
        }

        private void SelectFrameKind(PhaDoSvgFrameListItem.FrameKind kind)
        {
            var item = _svgFrameItems.FirstOrDefault(i => i.Kind == kind);
            if (item == null)
            {
                return;
            }

            _suppressSvgFrameSelectionChanged = true;
            try
            {
                svgFrameCombo.SelectedItem = item;
            }
            finally
            {
                _suppressSvgFrameSelectionChanged = false;
            }

            ApplySvgFrameSelection();
        }

        private void CustomSvgBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressSvgAutoRefresh || GetSelectedFrameItem()?.Kind != PhaDoSvgFrameListItem.FrameKind.CreateNew)
            {
                return;
            }

            _svgAutoCheckTimer.Stop();
            _svgAutoCheckTimer.Start();
        }

        private void SvgAutoCheckTimer_Tick(object sender, EventArgs e)
        {
            _svgAutoCheckTimer.Stop();
            RunSvgValidateAndPreview();
        }

        private void FillColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
            UpdateSvgPreviewVisual();

        private void ValidateSvg_Click(object sender, RoutedEventArgs e) => RunSvgValidateAndPreview();

        private void ClearSvg_Click(object sender, RoutedEventArgs e)
        {
            _suppressSvgAutoRefresh = true;
            try
            {
                customSvgBox.Text = "";
            }
            finally
            {
                _suppressSvgAutoRefresh = false;
            }

            RunSvgValidateAndPreview();
        }

        private void LoadSvgFromFile_Click(object sender, RoutedEventArgs e)
        {
            var openDlg = new OpenFileDialog
            {
                Title = "Chọn file SVG",
                Filter = "SVG (*.svg)|*.svg|XML/HTML (*.xml;*.html)|*.xml;*.html|Tất cả (*.*)|*.*",
                CheckFileExists = true
            };

            if (openDlg.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                string text = File.ReadAllText(openDlg.FileName);
                if (GetSelectedFrameItem()?.Kind != PhaDoSvgFrameListItem.FrameKind.CreateNew)
                {
                    SelectFrameKind(PhaDoSvgFrameListItem.FrameKind.CreateNew);
                }

                _suppressSvgAutoRefresh = true;
                try
                {
                    customSvgBox.Text = text;
                }
                finally
                {
                    _suppressSvgAutoRefresh = false;
                }

                RunSvgValidateAndPreview();
                svgStatusText.Text = "Đã tải: " + Path.GetFileName(openDlg.FileName);
                svgStatusText.Foreground = Brushes.DarkGreen;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không đọc được file: " + ex.Message, "Tải SVG");
            }
        }

        private void RunSvgValidateAndPreview()
        {
            if (GetSelectedFrameItem()?.Kind == PhaDoSvgFrameListItem.FrameKind.Catalog)
            {
                return;
            }

            _lastSvgSanitize = PhaDoBoxSvgSanitizer.Sanitize(customSvgBox?.Text ?? "");
            UpdateSvgStatus(_lastSvgSanitize);
            UpdateSvgPreviewVisual();
        }

        private void UpdateSvgStatus(PhaDoBoxSvgSanitizeResult result)
        {
            svgStatusText.Text = result?.Message ?? "";
            svgStatusText.Foreground = result != null && result.Success ? Brushes.DarkGreen : Brushes.Gray;
        }

        private void UpdateSvgPreviewVisual()
        {
            svgPreviewHost.Children.Clear();
            string fillHex = null;
            if (fillColorCombo.SelectedItem is PhaDoBoxStyleDialog.ColorOption fill && !fill.IsDefault)
            {
                fillHex = fill.Hex;
            }

            if (_lastSvgSanitize != null && _lastSvgSanitize.Success
                && !string.IsNullOrWhiteSpace(_lastSvgSanitize.SanitizedSvgMarkup))
            {
                var preview = PhaDoBoxSvgWpfRenderer.CreateDialogPreview(
                    _lastSvgSanitize.SanitizedSvgMarkup,
                    _lastSvgSanitize.ViewBoxWidth,
                    _lastSvgSanitize.ViewBoxHeight,
                    fillHex,
                    200,
                    100);
                svgPreviewHost.Children.Add(preview);
                return;
            }

            var placeholder = PhaDoBoxSvgWpfRenderer.CreateDialogPreview("", 100, 80, fillHex, 200, 100);
            svgPreviewHost.Children.Add(placeholder);
        }

        private static void SetPersonRow(
            PhaDoPersonTextStyle person,
            ComboBox fontCombo,
            TextBox sizeBox,
            ComboBox colorCombo,
            double defaultPt)
        {
            string font = string.IsNullOrWhiteSpace(person?.FontFamilyName) ? "Segoe UI" : person.FontFamilyName;
            int fontIdx = PhaDoBoxStyleDialog.PresetFontFamilies.ToList()
                .FindIndex(f => string.Equals(f, font, StringComparison.OrdinalIgnoreCase));
            fontCombo.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;
            sizeBox.Text = (person?.FontPt ?? defaultPt).ToString("0.##", CultureInfo.InvariantCulture);
            colorCombo.SelectedIndex = IndexForTextColor(person?.ForegroundHex);
        }

        private static bool TryReadPersonRow(
            ComboBox fontCombo,
            TextBox sizeBox,
            ComboBox colorCombo,
            double defaultPt,
            out PhaDoPersonTextStyle person)
        {
            person = new PhaDoPersonTextStyle();
            if (fontCombo.SelectedItem is string fontName)
            {
                person.FontFamilyName = fontName;
            }

            if (!double.TryParse(sizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pt)
                && !double.TryParse(sizeBox.Text, out pt))
            {
                pt = defaultPt;
            }

            person.FontPt = Math.Max(6, Math.Min(48, pt));

            var color = colorCombo.SelectedItem as PhaDoBoxStyleDialog.ColorOption;
            if (color != null && !color.IsDefault)
            {
                person.ForegroundHex = color.Hex;
            }

            return true;
        }

        private static int IndexForFill(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return 0;
            }

            var list = PhaDoBoxStyleDialog.PresetFillColors;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        private static int IndexForTextColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return 0;
            }

            var list = PhaDoBoxStyleDialog.PresetTextColors;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
