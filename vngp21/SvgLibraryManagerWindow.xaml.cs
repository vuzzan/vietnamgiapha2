using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    /// <summary>Quản lý SVG zone (title_* / family_*) + kho cloud.</summary>
    public partial class SvgLibraryManagerWindow : MetroWindow
    {
        private const string ZoneCatTitle = "title";
        private const string ZoneCatFamily = "family";

        private enum PanelSelectionSource
        {
            None = 0,
            Cloud = 1,
            Local = 2
        }

        private readonly GiaphaInfo _giaPha;
        private readonly Func<bool> _saveGiaPhaFile;
        private readonly Action _onZoneFilesChanged;
        private readonly ObservableCollection<SvgTreeNode> _treeRoots = new ObservableCollection<SvgTreeNode>();
        private readonly ObservableCollection<SvgLocalListItem> _localItems = new ObservableCollection<SvgLocalListItem>();
        private readonly DispatcherTimer _previewTimer;

        private bool _suppressCodePreview;
        private bool _suppressPanelSelectionSync;
        private bool _isEditMode = true;
        private PanelSelectionSource _panelSource = PanelSelectionSource.None;
        private int? _selectedCloudId;
        private SvgCloudItem _loadedCloudDetail;
        private string _zoneSvgFolderPath;
        private string _selectedZoneFilePath;

        public SvgLibraryManagerWindow(GiaphaInfo giaPha, Func<bool> saveGiaPhaFile, Action onZoneFilesChanged = null)
        {
            _giaPha = giaPha;
            _saveGiaPhaFile = saveGiaPhaFile;
            _onZoneFilesChanged = onZoneFilesChanged;

            InitializeComponent();
            DataContext = this;
            TreeRoots = _treeRoots;
            LocalItems = _localItems;

            uploadCategoryCombo.ItemsSource = new[] { ZoneCatTitle, ZoneCatFamily };
            uploadCategoryCombo.SelectedIndex = 0;

            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _previewTimer.Tick += PreviewTimer_Tick;

            Loaded += async (s, e) =>
            {
                RefreshLocalList();
                await LoadFromCloudAsync().ConfigureAwait(true);
            };
        }

        public ObservableCollection<SvgTreeNode> TreeRoots { get; }
        public ObservableCollection<SvgLocalListItem> LocalItems { get; }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadFromCloudAsync().ConfigureAwait(true);

        private void RefreshLocal_Click(object sender, RoutedEventArgs e) => RefreshLocalList();

        private void New_Click(object sender, RoutedEventArgs e)
        {
            _selectedCloudId = null;
            _loadedCloudDetail = null;
            _selectedZoneFilePath = null;
            _panelSource = PanelSelectionSource.None;
            _isEditMode = true;
            svgCodeBox.IsReadOnly = false;
            ClearTreeViewSelection(svgTree);
            localSvgList.SelectedIndex = -1;

            _suppressCodePreview = true;
            try
            {
                svgCodeBox.Text = "";
                uploadNameBox.Text = "";
                uploadAuthorBox.Text = "";
            }
            finally
            {
                _suppressCodePreview = false;
            }

            uploadCategoryCombo.SelectedIndex = 0;
            detailStatusText.Text = "Tạo mới — dán/tải file → Lưu file SVG (thư mục zone) hoặc Upload cloud.";
            SetStatus("Chế độ tạo mới. Thư mục: " + (_zoneSvgFolderPath ?? "?"));
            UpdatePreview();
        }

        private void LoadFile_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn file SVG",
                Filter = "SVG (*.svg)|*.svg|Tất cả (*.*)|*.*"
            };

            if (dlg.ShowDialog(this) != true)
            {
                return;
            }

            try
            {
                _panelSource = PanelSelectionSource.None;
                _isEditMode = true;
                _suppressCodePreview = true;
                svgCodeBox.Text = File.ReadAllText(dlg.FileName);
                string stem = Path.GetFileNameWithoutExtension(dlg.FileName);
                if (string.IsNullOrWhiteSpace(uploadNameBox.Text))
                {
                    uploadNameBox.Text = StripZonePrefixFromFileName(stem);
                }

                ApplyZoneCategoryFromFileName(stem);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không đọc file: " + ex.Message, "Lỗi");
            }
            finally
            {
                _suppressCodePreview = false;
            }

            svgCodeBox.IsReadOnly = false;
            SchedulePreview();
            SetStatus("Đã tải file: " + dlg.FileName);
        }

        private async void Upload_Click(object sender, RoutedEventArgs e)
        {
            string raw = svgCodeBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("Chưa có nội dung SVG.", "Upload");
                return;
            }

            var sanitized = PhaDoBoxSvgSanitizer.Sanitize(raw);
            if (!sanitized.Success)
            {
                MessageBox.Show(sanitized.Message ?? "SVG không hợp lệ.", "Upload");
                return;
            }

            string category = GetSelectedZoneCategory();
            string name = uploadNameBox?.Text?.Trim() ?? "";
            string author = uploadAuthorBox?.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Nhập tên khung (Tên).", "Upload");
                uploadNameBox.Focus();
                return;
            }

            var consent = MessageBox.Show(
                "Khi upload lên cloud, là bạn mặc nhiên đồng ý cho phép mọi người dùng chung phần mềm Việt Nam Gia Phả được sử dụng miễn phí file SVG của bạn. Bấm OK là đồng ý và upload, bấm Cancel là không đồng ý.",
                "Upload lên cloud",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Information);
            if (consent != MessageBoxResult.OK)
            {
                return;
            }

            btnUpload.IsEnabled = false;
            SetStatus("Đang kiểm tra trùng lặp trên cloud...");
            try
            {
                await SvgCloudApiService.EnsureNotDuplicateAsync(sanitized.SanitizedSvgMarkup)
                    .ConfigureAwait(true);
            }
            catch (SvgCloudDuplicateException dupEx)
            {
                HandleDuplicateUploadBlocked(dupEx);
                btnUpload.IsEnabled = true;
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không kiểm tra được trùng lặp trên cloud:\n\n" + ex.Message,
                    "Lỗi kiểm tra",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                SetStatus("Upload bị hủy: lỗi kiểm tra trùng.");
                btnUpload.IsEnabled = true;
                return;
            }

            SetStatus("Đang upload lên cloud...");
            try
            {
                var result = await SvgCloudApiService.UploadAsync(
                    category,
                    name,
                    author,
                    sanitized.SanitizedSvgMarkup).ConfigureAwait(true);

                MessageBox.Show(
                    "Đã upload khung \"" + result.Name + "\" (id=" + result.Id + ").",
                    "Upload",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadFromCloudAsync().ConfigureAwait(true);
                Dispatcher.BeginInvoke(new Action(() => SelectCloudItemById(result.Id)), DispatcherPriority.Loaded);
            }
            catch (SvgCloudDuplicateException dupEx)
            {
                HandleDuplicateUploadBlocked(dupEx);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Upload thất bại:\n\n" + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Upload lỗi.");
            }
            finally
            {
                btnUpload.IsEnabled = true;
            }
        }

        private void SaveToZoneFolder_Click(object sender, RoutedEventArgs e)
        {
            string raw = svgCodeBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(raw))
            {
                MessageBox.Show("Chưa có nội dung SVG.", "Lưu file SVG");
                return;
            }

            var sanitized = PhaDoBoxSvgSanitizer.Sanitize(raw);
            if (!sanitized.Success)
            {
                MessageBox.Show(sanitized.Message ?? "SVG không hợp lệ.", "Lưu file SVG");
                return;
            }

            string namePart = uploadNameBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(namePart))
            {
                MessageBox.Show("Nhập tên file (phần sau title_ hoặc family_).", "Lưu file SVG");
                uploadNameBox.Focus();
                return;
            }

            string svgId;
            try
            {
                svgId = BuildZoneSvgFileId(GetSelectedZoneCategory(), namePart);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Lưu file SVG");
                return;
            }

            string folder = PhaDoZoneSvgFolderLoader.ResolveFolderPath();
            string targetPath = Path.Combine(folder, svgId + ".svg");
            if (File.Exists(targetPath))
            {
                var ow = MessageBox.Show(
                    "File đã tồn tại:\n" + targetPath + "\n\nGhi đè?",
                    "Lưu file SVG",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (ow != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            btnSaveZone.IsEnabled = false;
            try
            {
                string savedPath = PhaDoZoneSvgFolderLoader.SaveSvgToFolder(
                    folder,
                    svgId,
                    sanitized.SanitizedSvgMarkup);

                _zoneSvgFolderPath = folder;
                _selectedZoneFilePath = savedPath;
                _panelSource = PanelSelectionSource.Local;
                _isEditMode = true;
                svgCodeBox.IsReadOnly = false;

                RefreshLocalList();
                SelectLocalItemById(svgId);
                _onZoneFilesChanged?.Invoke();

                SetStatus("Đã lưu: " + savedPath);
                MessageBox.Show(
                    "Đã lưu file SVG:\n" + savedPath,
                    "Lưu file SVG",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không lưu được file:\n\n" + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Lỗi lưu file zone.");
            }
            finally
            {
                btnSaveZone.IsEnabled = true;
            }
        }

        private async void ImportToLocal_Click(object sender, RoutedEventArgs e)
        {
            if (_giaPha == null)
            {
                MessageBox.Show("Chưa mở file gia phả.", "Kho local");
                return;
            }

            if (_selectedCloudId == null || _selectedCloudId <= 0)
            {
                MessageBox.Show("Chọn một khung từ kho cloud (cây phía trên).", "Thêm vào local");
                return;
            }

            btnImportToLocal.IsEnabled = false;
            SetStatus("Đang chuyển sang kho local...");
            try
            {
                SvgCloudItem detail = _loadedCloudDetail;
                if (detail == null || detail.Id != _selectedCloudId || string.IsNullOrWhiteSpace(detail.SvgData))
                {
                    detail = await SvgCloudApiService.GetAsync(_selectedCloudId.Value).ConfigureAwait(true);
                    _loadedCloudDetail = detail;
                }

                var sanitized = PhaDoBoxSvgSanitizer.Sanitize(detail.SvgData ?? "");
                if (!sanitized.Success)
                {
                    MessageBox.Show(sanitized.Message ?? "SVG cloud không hợp lệ.", "Kho local");
                    return;
                }

                string localId = PhaDoSvgCatalog.NormalizeUserSvgId(detail.Name);
                if (string.IsNullOrWhiteSpace(localId))
                {
                    localId = "cloud_" + detail.Id;
                }

                if (_giaPha.SvgShapesById != null && _giaPha.SvgShapesById.ContainsKey(localId))
                {
                    var ow = MessageBox.Show(
                        "Khung local \"" + localId + "\" đã có. Ghi đè?",
                        "Kho local",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    if (ow != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }

                PhaDoSvgCatalog.UpsertShapeWithId(
                    _giaPha,
                    localId,
                    sanitized.SanitizedSvgMarkup,
                    sanitized.ViewBoxWidth,
                    sanitized.ViewBoxHeight);

                var recorded = await SvgCloudApiService.RecordDownloadAsync(detail.Id).ConfigureAwait(true);

                if (_saveGiaPhaFile != null && !_saveGiaPhaFile())
                {
                    MessageBox.Show(
                        "Đã thêm vào kho local nhưng lưu file gia phả thất bại. Hãy Save thủ công.",
                        "Lưu file",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                RefreshLocalList();
                SelectLocalItemById(localId);
                await LoadFromCloudAsync().ConfigureAwait(true);
                SelectCloudItemById(detail.Id);

                SetStatus("Đã thêm \"" + localId + "\" vào local. Cloud lượt tải: " + recorded.CountDownload + ".");
                MessageBox.Show(
                    "Đã thêm khung \"" + localId + "\" vào file gia phả.\nLượt tải cloud: " + recorded.CountDownload,
                    "Kho local",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không thêm được vào local: " + ex.Message, "Lỗi");
                SetStatus("Lỗi import local.");
            }
            finally
            {
                btnImportToLocal.IsEnabled = true;
            }
        }

        private async void SvgTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suppressPanelSelectionSync)
            {
                return;
            }

            if (!(svgTree.SelectedItem is SvgTreeNode node) || node.IsCategory || node.Item == null)
            {
                return;
            }

            _panelSource = PanelSelectionSource.Cloud;
            _suppressPanelSelectionSync = true;
            try
            {
                localSvgList.SelectedIndex = -1;
            }
            finally
            {
                _suppressPanelSelectionSync = false;
            }

            int id = node.Item.Id;
            _selectedCloudId = id;
            _isEditMode = false;
            SetStatus("Đang tải chi tiết cloud id=" + id + "...");
            try
            {
                var detail = await SvgCloudApiService.GetAsync(id).ConfigureAwait(true);
                _loadedCloudDetail = detail;

                _suppressCodePreview = true;
                try
                {
                    svgCodeBox.Text = detail.SvgData ?? "";
                    SelectZoneCategoryFromCloud(detail.Category);
                    uploadNameBox.Text = detail.Name ?? "";
                    uploadAuthorBox.Text = detail.Author ?? "";
                }
                finally
                {
                    _suppressCodePreview = false;
                }

                svgCodeBox.IsReadOnly = true;
                detailStatusText.Text = detail.TreeLabel
                    + (string.IsNullOrWhiteSpace(detail.UpdateDate) ? "" : " | " + detail.UpdateDate)
                    + " | Lượt tải: " + detail.CountDownload
                    + " (tăng khi Thêm vào kho local)";
                SetStatus("Xem cloud — chưa tăng lượt tải. Bấm → Thêm vào kho local để lưu file.");
                UpdatePreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Không tải được SVG: " + ex.Message, "Lỗi");
            }
        }

        private void LocalSvgList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressPanelSelectionSync)
            {
                return;
            }

            if (!(localSvgList.SelectedItem is SvgLocalListItem local))
            {
                return;
            }

            _panelSource = PanelSelectionSource.Local;
            _selectedCloudId = null;
            _loadedCloudDetail = null;
            _selectedZoneFilePath = local.FilePath;
            _isEditMode = true;

            _suppressPanelSelectionSync = true;
            try
            {
                ClearTreeViewSelection(svgTree);
            }
            finally
            {
                _suppressPanelSelectionSync = false;
            }

            string markup = local.Shape?.GetSvgMarkup() ?? "";
            if (string.IsNullOrWhiteSpace(markup)
                && !string.IsNullOrWhiteSpace(local.FilePath)
                && File.Exists(local.FilePath))
            {
                try
                {
                    markup = File.ReadAllText(local.FilePath);
                }
                catch
                {
                    // Giữ markup rỗng nếu đọc file lỗi
                }
            }

            _suppressCodePreview = true;
            try
            {
                svgCodeBox.Text = markup;
                ApplyZoneCategoryFromFileName(local.SvgId);
                uploadNameBox.Text = StripZonePrefixFromFileName(local.SvgId);
            }
            finally
            {
                _suppressCodePreview = false;
            }

            svgCodeBox.IsReadOnly = false;
            detailStatusText.Text = "File zone: " + (local.FilePath ?? local.DisplayText);
            SetStatus("Đang sửa file zone \"" + local.SvgId + "\" — Lưu file SVG để ghi đè.");
            UpdatePreview();
        }

        private void SvgCodeBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressCodePreview || !_isEditMode)
            {
                return;
            }

            SchedulePreview();
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            _previewTimer.Stop();
            UpdatePreview();
        }

        private void SchedulePreview()
        {
            _previewTimer.Stop();
            _previewTimer.Start();
        }

        private void UpdatePreview()
        {
            previewHost.Children.Clear();
            string raw = svgCodeBox?.Text ?? "";
            var result = PhaDoBoxSvgSanitizer.Sanitize(raw);

            if (result == null || !result.Success || string.IsNullOrWhiteSpace(result.SanitizedSvgMarkup))
            {
                previewHost.Children.Add(new TextBlock
                {
                    Text = result?.Message ?? "Chưa có SVG hợp lệ",
                    Foreground = Brushes.Gray,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(8)
                });
                return;
            }

            previewHost.Children.Add(PhaDoBoxSvgWpfRenderer.CreateDialogPreview(
                result.SanitizedSvgMarkup,
                result.ViewBoxWidth,
                result.ViewBoxHeight,
                null,
                320,
                180));
        }

        private void RefreshLocalList()
        {
            _localItems.Clear();
            _zoneSvgFolderPath = PhaDoZoneSvgFolderLoader.ResolveFolderPath();
            var loaded = PhaDoZoneSvgFolderLoader.Load(_zoneSvgFolderPath);

            var entries = new List<PhaDoZoneSvgFileEntry>();
            entries.AddRange(loaded.TitleEntries);
            entries.AddRange(loaded.FamilyEntries);

            foreach (var entry in entries.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
            {
                PhaDoSvgShape shape = null;
                if (loaded.ShapesById != null)
                {
                    loaded.ShapesById.TryGetValue(entry.Id, out shape);
                }

                _localItems.Add(new SvgLocalListItem
                {
                    SvgId = entry.Id,
                    FilePath = entry.FilePath,
                    Shape = shape
                });
            }
        }

        private async Task LoadFromCloudAsync()
        {
            btnRefresh.IsEnabled = false;
            SetStatus("Đang tải danh sách cloud...");
            try
            {
                var list = await SvgCloudApiService.ListAsync().ConfigureAwait(true)
                    ?? new List<SvgCloudItem>();

                BuildTree(list);
                SetStatus(
                    "Cloud: " + list.Count + " khung | Zone: " + _localItems.Count + " file | "
                    + (_zoneSvgFolderPath ?? PhaDoZoneSvgFolderLoader.ResolveFolderPath()));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Không kết nối được API cloud.\n\n" + ex.Message,
                    "Cloud SVG",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                SetStatus("Lỗi cloud: " + ex.Message + " | Local: " + _localItems.Count + ".");
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private void BuildTree(List<SvgCloudItem> list)
        {
            _treeRoots.Clear();

            var titleGroup = new SvgCategoryGroup { Category = ZoneCatTitle };
            var familyGroup = new SvgCategoryGroup { Category = ZoneCatFamily };
            var titleNode = new SvgTreeNode { IsCategory = true, Group = titleGroup };
            var familyNode = new SvgTreeNode { IsCategory = true, Group = familyGroup };

            foreach (var item in list.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (IsFamilyCategory(item.Category))
                {
                    familyGroup.Items.Add(item);
                    familyNode.Children.Add(new SvgTreeNode { IsCategory = false, Item = item });
                }
                else
                {
                    titleGroup.Items.Add(item);
                    titleNode.Children.Add(new SvgTreeNode { IsCategory = false, Item = item });
                }
            }

            if (titleNode.Children.Count > 0)
            {
                _treeRoots.Add(titleNode);
            }

            if (familyNode.Children.Count > 0)
            {
                _treeRoots.Add(familyNode);
            }
        }

        private static string GetSelectedZoneCategoryFromCombo(System.Windows.Controls.ComboBox combo)
        {
            if (combo?.SelectedItem is string s && !string.IsNullOrWhiteSpace(s))
            {
                return s.Trim();
            }

            return ZoneCatTitle;
        }

        private string GetSelectedZoneCategory()
        {
            return GetSelectedZoneCategoryFromCombo(uploadCategoryCombo);
        }

        private void SelectZoneCategory(string category)
        {
            if (uploadCategoryCombo == null)
            {
                return;
            }

            string cat = IsFamilyCategory(category) ? ZoneCatFamily : ZoneCatTitle;
            uploadCategoryCombo.SelectedItem = cat;
        }

        private void SelectZoneCategoryFromCloud(string category)
        {
            SelectZoneCategory(category);
        }

        private static bool IsFamilyCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                return false;
            }

            string c = category.Trim();
            if (c.Equals(ZoneCatFamily, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return c.StartsWith(PhaDoZoneSvgFolderLoader.FamilyPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyZoneCategoryFromFileName(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return;
            }

            if (fileNameWithoutExtension.StartsWith(
                    PhaDoZoneSvgFolderLoader.FamilyPrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                SelectZoneCategory(ZoneCatFamily);
            }
            else if (fileNameWithoutExtension.StartsWith(
                         PhaDoZoneSvgFolderLoader.TitlePrefix,
                         StringComparison.OrdinalIgnoreCase))
            {
                SelectZoneCategory(ZoneCatTitle);
            }
        }

        private static string StripZonePrefixFromFileName(string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            {
                return "";
            }

            string stem = fileNameWithoutExtension.Trim();
            if (stem.StartsWith(PhaDoZoneSvgFolderLoader.FamilyPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return stem.Substring(PhaDoZoneSvgFolderLoader.FamilyPrefix.Length);
            }

            if (stem.StartsWith(PhaDoZoneSvgFolderLoader.TitlePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return stem.Substring(PhaDoZoneSvgFolderLoader.TitlePrefix.Length);
            }

            return stem;
        }

        private static string BuildZoneSvgFileId(string category, string namePart)
        {
            string stem = (namePart ?? "").Trim();
            stem = StripZonePrefixFromFileName(stem);

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                stem = stem.Replace(c, '_');
            }

            if (string.IsNullOrWhiteSpace(stem))
            {
                throw new ArgumentException("Tên file không hợp lệ.");
            }

            string prefix = IsFamilyCategory(category)
                ? PhaDoZoneSvgFolderLoader.FamilyPrefix
                : PhaDoZoneSvgFolderLoader.TitlePrefix;

            return prefix + stem;
        }

        private void SelectCloudItemById(int id)
        {
            foreach (var cat in _treeRoots)
            {
                foreach (var child in cat.Children)
                {
                    if (child.Item?.Id != id)
                    {
                        continue;
                    }

                    if (!TrySelectTreeNode(cat, child))
                    {
                        Dispatcher.BeginInvoke(
                            new Action(() => TrySelectTreeNode(cat, child)),
                            DispatcherPriority.Loaded);
                    }

                    return;
                }
            }
        }

        private void SelectLocalItemById(string svgId)
        {
            for (int i = 0; i < _localItems.Count; i++)
            {
                if (string.Equals(_localItems[i].SvgId, svgId, StringComparison.Ordinal))
                {
                    localSvgList.SelectedIndex = i;
                    localSvgList.ScrollIntoView(_localItems[i]);
                    return;
                }
            }
        }

        private static void ClearTreeViewSelection(TreeView tree)
        {
            if (tree == null)
            {
                return;
            }

            ClearTreeViewItemSelectionRecursive(tree);
        }

        private static void ClearTreeViewItemSelectionRecursive(ItemsControl parent)
        {
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < parent.Items.Count; i++)
            {
                if (parent.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem item)
                {
                    item.IsSelected = false;
                    ClearTreeViewItemSelectionRecursive(item);
                }
            }
        }

        private bool TrySelectTreeNode(SvgTreeNode categoryNode, SvgTreeNode itemNode)
        {
            if (categoryNode == null || itemNode == null)
            {
                return false;
            }

            if (!(svgTree.ItemContainerGenerator.ContainerFromItem(categoryNode) is TreeViewItem catItem))
            {
                return false;
            }

            catItem.IsExpanded = true;
            catItem.UpdateLayout();

            if (!(catItem.ItemContainerGenerator.ContainerFromItem(itemNode) is TreeViewItem leafItem))
            {
                return false;
            }

            leafItem.IsSelected = true;
            leafItem.BringIntoView();
            return true;
        }

        private void HandleDuplicateUploadBlocked(SvgCloudDuplicateException dupEx)
        {
            string message = string.IsNullOrWhiteSpace(dupEx?.UserMessage)
                ? dupEx?.Message
                : dupEx.UserMessage;

            MessageBox.Show(
                message ?? "Khung SVG đã tồn tại trên cloud.",
                "Không thể upload — SVG đã tồn tại",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            if (dupEx?.Existing != null && dupEx.Existing.Id > 0)
            {
                string shortStatus = "Upload bị hủy: trùng «" + (dupEx.Existing.Name ?? "?") + "» (id "
                    + dupEx.Existing.Id + ").";
                SetStatus(shortStatus);
                Dispatcher.BeginInvoke(
                    new Action(() => SelectCloudItemById(dupEx.Existing.Id)),
                    DispatcherPriority.Loaded);
            }
            else
            {
                SetStatus("Upload bị hủy: trùng khung đã có trên cloud.");
            }
        }

        private void SetStatus(string text)
        {
            statusBarText.Text = text ?? "";
        }
    }
}
