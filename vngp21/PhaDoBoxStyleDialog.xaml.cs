using MahApps.Metro.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    /// <summary>Dialog chỉnh nền + font/size/màu chữ chính/phụ — Apply xem trước, Close mới thoát.</summary>
    public partial class PhaDoBoxStyleDialog : MetroWindow
    {
        public sealed class ColorOption
        {
            public string Label { get; set; }
            public string Hex { get; set; }
            public bool IsDefault { get; set; }
        }

        public sealed class SvgFrameListItem
        {
            public enum FrameKind
            {
                DefaultRect = 0,
                Catalog = 1,
                CreateNew = 2
            }

            public FrameKind Kind { get; set; }
            public string SvgId { get; set; }
            public string Display { get; set; }
            public PhaDoSvgShape Shape { get; set; }
        }

        public sealed class StyleApplyEventArgs : EventArgs
        {
            public PhaDoStyleApplyScope Scope { get; set; }
            public PhaDoBoxStyle Style { get; set; }

            /// <summary>Khung mới từ dán/file — sau Apply hỏi lưu catalog với tên.</summary>
            public bool IsNewSvgFromEditor { get; set; }
        }

        public event EventHandler<StyleApplyEventArgs> StyleApplyRequested;

        private readonly double _defaultMainPt;
        private readonly double _defaultSpousePt;
        private readonly string _defaultFontFamily;

        private readonly List<SvgFrameListItem> _svgFrameItems = new List<SvgFrameListItem>();
        private PhaDoBoxSvgSanitizeResult _lastSvgSanitize;
        private readonly DispatcherTimer _svgAutoCheckTimer;
        private bool _suppressSvgAutoRefresh;
        private bool _suppressSvgFrameSelectionChanged;

        public static IReadOnlyList<ColorOption> PresetFillColors { get; } = new List<ColorOption>
        {
            new ColorOption { Label = "Mặc định (nhánh)", Hex = null, IsDefault = true },
            new ColorOption { Label = "Kem", Hex = "#FFF3E0" },
            new ColorOption { Label = "Xanh lá", Hex = "#E8F5E9" },
            new ColorOption { Label = "Xanh dương", Hex = "#E3F2FD" },
            new ColorOption { Label = "Hồng", Hex = "#FCE4EC" },
            new ColorOption { Label = "Tím", Hex = "#EDE7F6" },
            new ColorOption { Label = "Vàng", Hex = "#FFF9C4" },
            new ColorOption { Label = "Trắng", Hex = "#FFFFFF" }
        };

        public static IReadOnlyList<ColorOption> PresetTextColors { get; } = new List<ColorOption>
        {
            new ColorOption { Label = "Mặc định", Hex = null, IsDefault = true },
            new ColorOption { Label = "Đen", Hex = "#000000" },
            new ColorOption { Label = "Xanh đậm", Hex = "#1A237E" },
            new ColorOption { Label = "Đỏ", Hex = "#B71C1C" },
            new ColorOption { Label = "Xanh lá đậm", Hex = "#2E7D32" },
            new ColorOption { Label = "Tím", Hex = "#4A148C" },
            new ColorOption { Label = "Cam", Hex = "#E65100" }
        };

        public static IReadOnlyList<string> PresetFontFamilies { get; } = new[]
        {
            "Segoe UI",
            "Times New Roman",
            "Arial",
            "Tahoma",
            "Verdana"
        };

        public PhaDoBoxStyleDialog(
            PhaDoBoxStyle current,
            GiaPhaRenderOptions renderDefaults,
            IDictionary<string, PhaDoSvgShape> svgCatalog)
        {
            InitializeComponent();

            _defaultMainPt = renderDefaults?.MainNameFontPt ?? 9;
            _defaultSpousePt = renderDefaults?.SpouseFontPt ?? 7.5;
            _defaultFontFamily = renderDefaults?.FontFamilyName ?? "Segoe UI";

            fillColorCombo.ItemsSource = PresetFillColors;
            mainColorCombo.ItemsSource = PresetTextColors;
            spouseColorCombo.ItemsSource = PresetTextColors;

            var fonts = PresetFontFamilies.ToList();
            mainFontCombo.ItemsSource = fonts;
            spouseFontCombo.ItemsSource = fonts;

            _svgAutoCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            _svgAutoCheckTimer.Tick += SvgAutoCheckTimer_Tick;
            customSvgBox.TextChanged += CustomSvgBox_TextChanged;
            fillColorCombo.SelectionChanged += FillColorCombo_SelectionChanged;

            RebuildSvgFrameList(svgCatalog);
            LoadForm(current ?? new PhaDoBoxStyle());
        }

        /// <summary>Cập nhật danh sách khung sau Apply (catalog file đã đổi).</summary>
        public void RefreshSvgFrameCatalog(IDictionary<string, PhaDoSvgShape> svgCatalog, string selectSvgId = null)
        {
            string keepId = selectSvgId;
            if (string.IsNullOrWhiteSpace(keepId))
            {
                var current = GetSelectedFrameItem();
                if (current?.Kind == SvgFrameListItem.FrameKind.Catalog)
                {
                    keepId = current.SvgId;
                }
            }

            RebuildSvgFrameList(svgCatalog);
            if (!string.IsNullOrWhiteSpace(keepId))
            {
                SelectFrameBySvgId(keepId);
            }
        }

        private void RebuildSvgFrameList(IDictionary<string, PhaDoSvgShape> svgCatalog)
        {
            _svgFrameItems.Clear();
            _svgFrameItems.Add(new SvgFrameListItem
            {
                Kind = SvgFrameListItem.FrameKind.DefaultRect,
                Display = "Mặc định (rect bo góc)"
            });

            if (svgCatalog != null)
            {
                foreach (var kv in svgCatalog.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    var shape = kv.Value;
                    if (shape == null || string.IsNullOrWhiteSpace(shape.SvgBase64))
                    {
                        continue;
                    }

                    string id = string.IsNullOrWhiteSpace(shape.SvgId) ? kv.Key : shape.SvgId;
                    _svgFrameItems.Add(new SvgFrameListItem
                    {
                        Kind = SvgFrameListItem.FrameKind.Catalog,
                        SvgId = id,
                        Shape = shape,
                        Display = FormatCatalogDisplay(id, shape)
                    });
                }
            }

            _svgFrameItems.Add(new SvgFrameListItem
            {
                Kind = SvgFrameListItem.FrameKind.CreateNew,
                Display = "+ Tạo khung mới..."
            });

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

        private static string FormatCatalogDisplay(string svgId, PhaDoSvgShape shape)
        {
            string label = PhaDoSvgCatalog.IsAutoHashSvgId(svgId)
                ? (svgId.Length > 14 ? svgId.Substring(0, 14) + "…" : svgId)
                : svgId;
            return label + " (" + shape.ViewBoxWidth.ToString("0.##", CultureInfo.InvariantCulture)
                + "×" + shape.ViewBoxHeight.ToString("0.##", CultureInfo.InvariantCulture) + ")";
        }

        private SvgFrameListItem GetSelectedFrameItem()
        {
            return svgFrameCombo?.SelectedItem as SvgFrameListItem;
        }

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
                case SvgFrameListItem.FrameKind.DefaultRect:
                    newSvgPanel.Visibility = Visibility.Collapsed;
                    _lastSvgSanitize = PhaDoBoxSvgSanitizer.Sanitize("");
                    UpdateSvgStatus(_lastSvgSanitize);
                    UpdateSvgPreviewVisual();
                    break;

                case SvgFrameListItem.FrameKind.Catalog:
                    newSvgPanel.Visibility = Visibility.Collapsed;
                    PreviewCatalogShape(item.Shape);
                    break;

                case SvgFrameListItem.FrameKind.CreateNew:
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
                _lastSvgSanitize = new PhaDoBoxSvgSanitizeResult
                {
                    Success = false,
                    Message = "Không đọc được khung trong catalog."
                };
                UpdateSvgStatus(_lastSvgSanitize);
                UpdateSvgPreviewVisual();
                return;
            }

            string markup = shape.GetSvgMarkup();
            if (string.IsNullOrWhiteSpace(markup))
            {
                _lastSvgSanitize = new PhaDoBoxSvgSanitizeResult
                {
                    Success = false,
                    Message = "SVG trong catalog không hợp lệ."
                };
            }
            else
            {
                _lastSvgSanitize = new PhaDoBoxSvgSanitizeResult
                {
                    Success = true,
                    SanitizedSvgMarkup = markup,
                    ViewBoxWidth = shape.ViewBoxWidth,
                    ViewBoxHeight = shape.ViewBoxHeight,
                    Message = "Khung từ file gia phả: " + (shape.SvgId ?? "")
                };
            }

            UpdateSvgStatus(_lastSvgSanitize);
            UpdateSvgPreviewVisual();
        }

        private void SelectFrameBySvgId(string svgId)
        {
            if (string.IsNullOrWhiteSpace(svgId))
            {
                SelectFrameKind(SvgFrameListItem.FrameKind.DefaultRect);
                return;
            }

            var item = _svgFrameItems.FirstOrDefault(
                i => i.Kind == SvgFrameListItem.FrameKind.Catalog
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

            SelectFrameKind(SvgFrameListItem.FrameKind.CreateNew);
        }

        private void SelectFrameKind(SvgFrameListItem.FrameKind kind)
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
            if (_suppressSvgAutoRefresh || GetSelectedFrameItem()?.Kind != SvgFrameListItem.FrameKind.CreateNew)
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

        private void FillColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSvgPreviewVisual();
        }

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
                if (GetSelectedFrameItem()?.Kind != SvgFrameListItem.FrameKind.CreateNew)
                {
                    SelectFrameKind(SvgFrameListItem.FrameKind.CreateNew);
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
            if (GetSelectedFrameItem()?.Kind == SvgFrameListItem.FrameKind.Catalog)
            {
                return;
            }

            string raw = customSvgBox?.Text ?? "";
            _lastSvgSanitize = PhaDoBoxSvgSanitizer.Sanitize(raw);
            UpdateSvgStatus(_lastSvgSanitize);
            UpdateSvgPreviewVisual();
        }

        private string GetSelectedFillHex()
        {
            var fill = fillColorCombo?.SelectedItem as ColorOption;
            if (fill != null && !fill.IsDefault)
            {
                return fill.Hex;
            }

            return null;
        }

        private void UpdateSvgPreviewVisual()
        {
            if (svgPreviewHost == null)
            {
                return;
            }

            svgPreviewHost.Children.Clear();

            if (_lastSvgSanitize == null)
            {
                svgPreviewHost.Children.Add(PhaDoBoxSvgWpfRenderer.CreateDialogPreview(
                    null, 100, 80, GetSelectedFillHex()));
                return;
            }

            if (!_lastSvgSanitize.Success)
            {
                svgPreviewHost.Children.Add(new TextBlock
                {
                    Text = _lastSvgSanitize.Message ?? "SVG chưa hợp lệ",
                    Foreground = Brushes.DarkRed,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8),
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                return;
            }

            if (string.IsNullOrWhiteSpace(_lastSvgSanitize.SanitizedSvgMarkup))
            {
                svgPreviewHost.Children.Add(PhaDoBoxSvgWpfRenderer.CreateDialogPreview(
                    null, 100, 80, GetSelectedFillHex()));
                return;
            }

            svgPreviewHost.Children.Add(PhaDoBoxSvgWpfRenderer.CreateDialogPreview(
                _lastSvgSanitize.SanitizedSvgMarkup,
                _lastSvgSanitize.ViewBoxWidth,
                _lastSvgSanitize.ViewBoxHeight,
                GetSelectedFillHex()));
        }

        private void UpdateSvgStatus(PhaDoBoxSvgSanitizeResult result)
        {
            if (svgStatusText == null)
            {
                return;
            }

            if (result == null)
            {
                svgStatusText.Text = "";
                svgStatusText.Foreground = Brushes.Gray;
                return;
            }

            svgStatusText.Text = result.Message ?? "";
            svgStatusText.Foreground = result.Success ? Brushes.DarkGreen : Brushes.DarkRed;
        }

        private void ApplyBox_Click(object sender, RoutedEventArgs e) => TryApply(PhaDoStyleApplyScope.SingleBox);

        private void ApplyLevel_Click(object sender, RoutedEventArgs e) => TryApply(PhaDoStyleApplyScope.AllBoxesInLevel);

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void TryApply(PhaDoStyleApplyScope scope)
        {
            if (!TryReadForm(out PhaDoBoxStyle style))
            {
                return;
            }

            var frame = GetSelectedFrameItem();
            bool isNewSvg = frame?.Kind == SvgFrameListItem.FrameKind.CreateNew
                && !string.IsNullOrWhiteSpace(style.CustomShapeSvg);

            StyleApplyRequested?.Invoke(this, new StyleApplyEventArgs
            {
                Scope = scope,
                Style = style,
                IsNewSvgFromEditor = isNewSvg
            });
        }

        private void LoadForm(PhaDoBoxStyle style)
        {
            _suppressSvgAutoRefresh = true;
            _suppressSvgFrameSelectionChanged = true;
            try
            {
                fillColorCombo.SelectedIndex = IndexForFill(style.FillColorHex);
                SetPersonRow(style.Main, mainFontCombo, mainSizeBox, mainColorCombo, _defaultMainPt);
                SetPersonRow(style.Spouse, spouseFontCombo, spouseSizeBox, spouseColorCombo, _defaultSpousePt);

                customSvgBox.Text = style.CustomShapeSvg ?? "";

                if (!string.IsNullOrWhiteSpace(style.ShapeSvgId))
                {
                    SelectFrameBySvgId(style.ShapeSvgId);
                }
                else if (!string.IsNullOrWhiteSpace(style.CustomShapeSvg))
                {
                    SelectFrameKind(SvgFrameListItem.FrameKind.CreateNew);
                }
                else
                {
                    SelectFrameKind(SvgFrameListItem.FrameKind.DefaultRect);
                }
            }
            finally
            {
                _suppressSvgAutoRefresh = false;
                _suppressSvgFrameSelectionChanged = false;
            }

            if (GetSelectedFrameItem()?.Kind == SvgFrameListItem.FrameKind.CreateNew)
            {
                RunSvgValidateAndPreview();
            }
        }

        private void SetPersonRow(
            PhaDoPersonTextStyle person,
            System.Windows.Controls.ComboBox fontCombo,
            System.Windows.Controls.TextBox sizeBox,
            System.Windows.Controls.ComboBox colorCombo,
            double defaultPt)
        {
            string font = string.IsNullOrWhiteSpace(person?.FontFamilyName) ? _defaultFontFamily : person.FontFamilyName;
            int fontIdx = PresetFontFamilies.ToList().FindIndex(f => string.Equals(f, font, StringComparison.OrdinalIgnoreCase));
            fontCombo.SelectedIndex = fontIdx >= 0 ? fontIdx : 0;

            double pt = person?.FontPt ?? defaultPt;
            sizeBox.Text = pt.ToString("0.##", CultureInfo.InvariantCulture);
            colorCombo.SelectedIndex = IndexForTextColor(person?.ForegroundHex);
        }

        private bool TryReadForm(out PhaDoBoxStyle style)
        {
            style = new PhaDoBoxStyle();

            var fill = fillColorCombo.SelectedItem as ColorOption;
            if (fill != null && !fill.IsDefault)
            {
                style.FillColorHex = fill.Hex;
            }

            if (!TryReadPersonRow(mainFontCombo, mainSizeBox, mainColorCombo, _defaultMainPt, out var main))
            {
                return false;
            }

            if (!TryReadPersonRow(spouseFontCombo, spouseSizeBox, spouseColorCombo, _defaultSpousePt, out var spouse))
            {
                return false;
            }

            style.Main = main;
            style.Spouse = spouse;

            if (!TryApplySvgFrameToStyle(style))
            {
                return false;
            }

            return true;
        }

        private bool TryApplySvgFrameToStyle(PhaDoBoxStyle style)
        {
            var frame = GetSelectedFrameItem();
            if (frame == null)
            {
                return true;
            }

            switch (frame.Kind)
            {
                case SvgFrameListItem.FrameKind.DefaultRect:
                    style.ShapeSvgId = null;
                    style.CustomShapeSvg = null;
                    return true;

                case SvgFrameListItem.FrameKind.Catalog:
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

                    style.ShapeSvgId = frame.SvgId;
                    style.CustomShapeSvg = markup;
                    style.CustomShapeViewBoxWidth = frame.Shape.ViewBoxWidth;
                    style.CustomShapeViewBoxHeight = frame.Shape.ViewBoxHeight;
                    return true;

                case SvgFrameListItem.FrameKind.CreateNew:
                    return TryApplyNewSvgFromEditor(style);

                default:
                    return true;
            }
        }

        private bool TryApplyNewSvgFromEditor(PhaDoBoxStyle style)
        {
            string raw = customSvgBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
            {
                style.ShapeSvgId = null;
                style.CustomShapeSvg = null;
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

            style.ShapeSvgId = null;
            style.CustomShapeSvg = sanitized.SanitizedSvgMarkup;
            style.CustomShapeViewBoxWidth = sanitized.ViewBoxWidth;
            style.CustomShapeViewBoxHeight = sanitized.ViewBoxHeight;
            return true;
        }

        private bool TryReadPersonRow(
            System.Windows.Controls.ComboBox fontCombo,
            System.Windows.Controls.TextBox sizeBox,
            System.Windows.Controls.ComboBox colorCombo,
            double defaultPt,
            out PhaDoPersonTextStyle person)
        {
            person = new PhaDoPersonTextStyle();

            if (fontCombo.SelectedItem is string fontName
                && !string.Equals(fontName, _defaultFontFamily, StringComparison.OrdinalIgnoreCase))
            {
                person.FontFamilyName = fontName;
            }

            if (!double.TryParse(sizeBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double pt)
                && !double.TryParse(sizeBox.Text, out pt))
            {
                MessageBox.Show("Cỡ chữ không hợp lệ (6 – 28 pt).", "Có lỗi");
                return false;
            }

            pt = Math.Max(6, Math.Min(28, pt));
            if (Math.Abs(pt - defaultPt) > 0.01)
            {
                person.FontPt = pt;
            }

            var color = colorCombo.SelectedItem as ColorOption;
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

            for (int i = 0; i < PresetFillColors.Count; i++)
            {
                if (string.Equals(PresetFillColors[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
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

            for (int i = 0; i < PresetTextColors.Count; i++)
            {
                if (string.Equals(PresetTextColors[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
