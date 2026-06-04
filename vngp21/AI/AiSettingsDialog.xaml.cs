using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using MahApps.Metro.Controls;

namespace vietnamgiapha.AI
{
    public partial class AiSettingsDialog : MetroWindow
    {
        private readonly AiApiService _service;
        private AiSettings _current;
        private bool _keyVisible;
        private CancellationTokenSource _testCts;
        // Token riêng cho việc fetch model list — hủy request cũ khi đổi provider
        private CancellationTokenSource _modelFetchCts;

        public AiSettings ResultSettings { get; private set; }

        public AiSettingsDialog(AiApiService service)
        {
            InitializeComponent();
            _service = service;
            _current = service.Settings != null
                ? CloneSettings(service.Settings)
                : new AiSettings();

            LoadProviders();
            LoadFromSettings(_current);

            // Sau khi window loaded (key đã điền xong) — fetch model list lần đầu
            Loaded += OnDialogLoaded;
        }

        private async void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= OnDialogLoaded;
            int idx = providerCombo.SelectedIndex;
            if (idx < 0 || idx >= AiProviderConfig.Providers.Count) return;

            var provider = AiProviderConfig.Providers[idx];
            string key = GetCurrentApiKey();
            bool canFetch = !string.IsNullOrEmpty(provider.ModelsEndpoint)
                && (provider.Key == "openrouter" || !string.IsNullOrEmpty(key));

            if (canFetch) await FetchAndUpdateModelsAsync(provider, key);
        }

        // ── Khởi tạo ─────────────────────────────────────────────────────────

        private void LoadProviders()
        {
            providerCombo.Items.Clear();
            foreach (var p in AiProviderConfig.Providers)
            {
                providerCombo.Items.Add(p.DisplayName);
            }
        }

        private void LoadFromSettings(AiSettings s)
        {
            enabledCheck.IsChecked = s.IsEnabled;

            // Chọn chế độ
            if (s.UseLocalRuleEngine)
            {
                localModeRadio.IsChecked = true;
            }
            else
            {
                apiModeRadio.IsChecked = true;
            }

            UpdateApiSectionVisibility();

            // Chọn provider
            int provIdx = 0;
            for (int i = 0; i < AiProviderConfig.Providers.Count; i++)
            {
                if (AiProviderConfig.Providers[i].Key == s.Provider)
                {
                    provIdx = i;
                    break;
                }
            }

            providerCombo.SelectedIndex = provIdx;

            // API key
            if (!string.IsNullOrEmpty(s.ApiKey))
            {
                apiKeyBox.Password = s.ApiKey;
            }
        }

        private void UpdateApiSectionVisibility()
        {
            bool isApiMode = apiModeRadio.IsChecked == true;
            if (apiSettingsPanel != null)
            {
                apiSettingsPanel.Visibility = isApiMode
                    ? System.Windows.Visibility.Visible
                    : System.Windows.Visibility.Collapsed;
            }
        }

        private void UpdateGuideAndModels(AiProviderInfo provider)
        {
            if (provider == null) return;
            UpdateGuideOnly(provider);
            // Hiện ngay danh sách dự phòng — FetchAndUpdateModels sẽ cập nhật sau
            PopulateModelCombo(provider.Models);
        }

        /// <summary>Cập nhật phần hướng dẫn và URL lấy key (sync).</summary>
        private void UpdateGuideOnly(AiProviderInfo provider)
        {
            if (provider == null) return;
            guideText.Text = provider.GetKeyGuide;
            openUrlBtn.Tag = provider.GetKeyUrl;
        }

        /// <summary>Điền items vào modelCombo, giữ lại model đang chọn nếu có trong list.</summary>
        private void PopulateModelCombo(string[] models)
        {
            // Lưu lại model đang hiển thị để restore sau khi fill list
            string currentModel = modelCombo.IsEditable
                ? modelCombo.Text
                : (modelCombo.SelectedItem as string ?? "");

            if (string.IsNullOrEmpty(currentModel))
                currentModel = _current.Model ?? "";

            modelCombo.Items.Clear();
            int selIdx = 0;
            for (int i = 0; i < models.Length; i++)
            {
                modelCombo.Items.Add(models[i]);
                if (models[i] == currentModel) selIdx = i;
            }

            if (models.Length > 0)
                modelCombo.SelectedIndex = selIdx;

            // Nếu model đang dùng không có trong list — vẫn giữ làm text (IsEditable)
            if (modelCombo.SelectedIndex == 0 && !string.IsNullOrEmpty(currentModel)
                && models.Length > 0 && models[0] != currentModel)
            {
                modelCombo.Text = currentModel;
            }
        }

        /// <summary>
        /// Fetch danh sách model từ API, cập nhật ComboBox và status text.
        /// Hủy request fetch cũ nếu đang chạy.
        /// </summary>
        private async Task FetchAndUpdateModelsAsync(AiProviderInfo provider, string apiKey)
        {
            _modelFetchCts?.Cancel();
            _modelFetchCts = new CancellationTokenSource();
            var ct = _modelFetchCts.Token;

            modelsStatusText.Text = "⏳ Đang tải...";
            refreshModelsBtn.IsEnabled = false;

            try
            {
                var models = await AiApiService.FetchModelsAsync(apiKey, provider, ct);

                if (ct.IsCancellationRequested) return;

                PopulateModelCombo(models);
                modelsStatusText.Text = "✅ " + models.Length + " model (từ API)";
            }
            catch (OperationCanceledException)
            {
                // Request bị hủy bởi fetch mới hơn — không cần báo
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested) return;
                // Giữ nguyên danh sách dự phòng đang hiển thị
                modelsStatusText.Text = "❌ " + ex.Message.Split('\n')[0];
            }
            finally
            {
                if (!ct.IsCancellationRequested)
                    refreshModelsBtn.IsEnabled = true;
            }
        }

        private string GetCurrentApiKey()
        {
            return _keyVisible ? apiKeyVisible.Text : apiKeyBox.Password;
        }

        // ── Events ────────────────────────────────────────────────────────────

        private async void ProviderCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            int idx = providerCombo.SelectedIndex;
            if (idx < 0 || idx >= AiProviderConfig.Providers.Count) return;

            var provider = AiProviderConfig.Providers[idx];
            UpdateGuideOnly(provider);

            // Hiện ngay danh sách dự phòng, không chờ fetch
            PopulateModelCombo(provider.Models);
            modelsStatusText.Text = "📋 Danh sách mặc định";

            // Fetch model động nếu có thể (OpenRouter không cần key)
            string key = GetCurrentApiKey();
            bool canFetch = !string.IsNullOrEmpty(provider.ModelsEndpoint)
                && (provider.Key == "openrouter" || !string.IsNullOrEmpty(key));

            if (canFetch) await FetchAndUpdateModelsAsync(provider, key);

            UpdateTestButtonState();
        }

        private async void RefreshModelsBtn_Click(object sender, RoutedEventArgs e)
        {
            int idx = providerCombo.SelectedIndex;
            if (idx < 0 || idx >= AiProviderConfig.Providers.Count) return;

            var provider = AiProviderConfig.Providers[idx];
            string key = GetCurrentApiKey();

            if (string.IsNullOrEmpty(provider.ModelsEndpoint))
            {
                modelsStatusText.Text = "ℹ️ Provider này không hỗ trợ fetch model động";
                return;
            }

            if (provider.Key != "openrouter" && string.IsNullOrEmpty(key))
            {
                modelsStatusText.Text = "⚠️ Cần nhập API key trước";
                return;
            }

            await FetchAndUpdateModelsAsync(provider, key);
        }

        private void EnabledCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateTestButtonState();
        }

        private void ModeRadio_Changed(object sender, RoutedEventArgs e)
        {
            UpdateApiSectionVisibility();
            UpdateTestButtonState();
        }

        private void ApiKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_keyVisible)
            {
                return;
            }

            UpdateTestButtonState();
        }

        private void ApiKeyVisible_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (!_keyVisible)
            {
                return;
            }

            UpdateTestButtonState();
        }

        private void ShowKeyBtn_Click(object sender, RoutedEventArgs e)
        {
            _keyVisible = !_keyVisible;
            if (_keyVisible)
            {
                apiKeyVisible.Text = apiKeyBox.Password;
                apiKeyBox.Visibility = Visibility.Collapsed;
                apiKeyVisible.Visibility = Visibility.Visible;
                showKeyBtn.Content = "🙈";
            }
            else
            {
                apiKeyBox.Password = apiKeyVisible.Text;
                apiKeyVisible.Visibility = Visibility.Collapsed;
                apiKeyBox.Visibility = Visibility.Visible;
                showKeyBtn.Content = "👁";
            }
        }

        private void OpenUrlBtn_Click(object sender, RoutedEventArgs e)
        {
            var url = (openUrlBtn.Tag as string) ?? "https://aistudio.google.com/apikey";
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private async void TestBtn_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            _testCts = new CancellationTokenSource();

            testBtn.IsEnabled = false;
            testResultText.Text = "⏳ Đang kiểm tra...";
            testResultText.Foreground = Brushes.Gray;
            testDetailBorder.Visibility = Visibility.Collapsed;

            var temp = BuildCurrentSettings();
            try
            {
                var result = await _service.TestConnectionAsync(temp, _testCts.Token);
                testResultText.Text = "✅ Kết nối thành công!";
                testResultText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 0));
                testDetailText.Text = result?.Length > 200 ? result.Substring(0, 200) + "…" : result;
                testDetailBorder.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException)
            {
                testResultText.Text = "Đã hủy.";
                testResultText.Foreground = Brushes.Gray;
            }
            catch (Exception ex)
            {
                testResultText.Text = "❌ Lỗi: " + ex.Message;
                testResultText.Foreground = Brushes.Red;
                testDetailText.Text = ex.Message;
                testDetailBorder.Visibility = Visibility.Visible;
            }
            finally
            {
                testBtn.IsEnabled = true;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var s = BuildCurrentSettings();
            try
            {
                s.Save();
                ResultSettings = s;
                _service.ApplySettings(s);
                DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu cài đặt:\n" + ex.Message, "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            DialogResult = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private AiSettings BuildCurrentSettings()
        {
            bool useLocal = localModeRadio.IsChecked == true;

            int provIdx = providerCombo.SelectedIndex;
            string provKey = provIdx >= 0 && provIdx < AiProviderConfig.Providers.Count
                ? AiProviderConfig.Providers[provIdx].Key
                : "gemini";

            string apiKey = _keyVisible ? apiKeyVisible.Text : apiKeyBox.Password;
            // IsEditable=True nên dùng Text — cho phép model ID tùy ý không có trong list
            string model = !string.IsNullOrWhiteSpace(modelCombo.Text)
                ? modelCombo.Text
                : (modelCombo.SelectedItem as string ?? "gemini-2.0-flash");

            return new AiSettings
            {
                Provider = provKey,
                Model = model,
                IsEnabled = enabledCheck.IsChecked == true,
                UseLocalRuleEngine = useLocal,
                ApiKey = useLocal ? null : apiKey,
                // Giữ nguyên EncryptedApiKey để Save() biết cần ghi file .key
                EncryptedApiKey = (!useLocal && !string.IsNullOrEmpty(apiKey)) ? "__dpapi__" : null
            };
        }

        private void UpdateTestButtonState()
        {
            bool isApiMode = apiModeRadio.IsChecked == true;
            string key = _keyVisible ? apiKeyVisible.Text : apiKeyBox.Password;
            // Nút test chỉ khả dụng khi ở chế độ API và có key
            testBtn.IsEnabled = isApiMode && !string.IsNullOrWhiteSpace(key);
        }

        private static AiSettings CloneSettings(AiSettings s)
        {
            return new AiSettings
            {
                Provider = s.Provider,
                Model = s.Model,
                IsEnabled = s.IsEnabled,
                UseLocalRuleEngine = s.UseLocalRuleEngine,
                ApiKey = s.ApiKey,
                EncryptedApiKey = s.EncryptedApiKey
            };
        }
    }
}
