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
        private readonly LocalLlamaChatService _localLlama = new LocalLlamaChatService();
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
            if (AiBackendModes.IsLocalLlama(s.BackendMode))
            {
                localLlamaModeRadio.IsChecked = true;
            }
            else if (AiBackendModes.IsCloudApi(s.BackendMode))
            {
                apiModeRadio.IsChecked = true;
            }
            else
            {
                localModeRadio.IsChecked = true;
            }

            localModelPathBox.Text = s.LocalLlamaModelPath ?? "";
            UpdateModePanelsVisibility();
            RefreshLocalLlamaGuideText();

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

        private void UpdateModePanelsVisibility()
        {
            bool isApiMode = apiModeRadio.IsChecked == true;
            bool isLocalLlama = localLlamaModeRadio.IsChecked == true;

            if (apiSettingsPanel != null)
            {
                apiSettingsPanel.Visibility = isApiMode
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            if (localLlamaSettingsPanel != null)
            {
                localLlamaSettingsPanel.Visibility = isLocalLlama
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void RefreshLocalLlamaGuideText()
        {
            if (localLlamaGuideText == null)
            {
                return;
            }

            string server = LocalLlamaPaths.GetServerExePath();
            string model = LocalLlamaPaths.ResolveModelPath(localModelPathBox?.Text);
            bool hasServer = System.IO.File.Exists(server);
            bool hasModel = !string.IsNullOrEmpty(model);

            localLlamaGuideText.Text =
                "Thư mục: " + LocalLlamaPaths.AiRootDirectory + "\n"
                + "• llama-server: " + (hasServer ? "✅ có" : "❌ thiếu — bấm nút Tải llama-server bên dưới") + "\n"
                + "• model GGUF: " + (hasModel ? "✅ " + System.IO.Path.GetFileName(model) : "❌ thiếu — bấm Tải model hoặc Chọn file");
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
            UpdateModePanelsVisibility();
            RefreshLocalLlamaGuideText();
            UpdateTestButtonState();
        }

        private void BrowseLocalModelBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Chọn file model GGUF",
                Filter = "GGUF model (*.gguf)|*.gguf|Tất cả (*.*)|*.*",
                InitialDirectory = LocalLlamaPaths.ModelsDirectory
            };

            if (System.IO.Directory.Exists(LocalLlamaPaths.ModelsDirectory))
            {
                dlg.InitialDirectory = LocalLlamaPaths.ModelsDirectory;
            }

            if (dlg.ShowDialog() == true)
            {
                localModelPathBox.Text = dlg.FileName;
                RefreshLocalLlamaGuideText();
            }
        }

        private void OpenAiFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            LocalLlamaPaths.EnsureAiFoldersExist();
            OpenUrlInBrowser(LocalLlamaPaths.AiRootDirectory, isFolder: true);
        }

        private void DownloadLlamaServerBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrlInBrowser(LocalLlamaDownloadLinks.LlamaServerReleasesUrl);
        }

        private void DownloadQwenModelBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrlInBrowser(LocalLlamaDownloadLinks.Qwen3_4bGgufUrl);
        }

        private void DownloadQwenSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenUrlInBrowser(LocalLlamaDownloadLinks.QwenGgufSearchUrl);
        }

        /// <summary>Mở URL hoặc thư mục bằng app mặc định của Windows.</summary>
        private static void OpenUrlInBrowser(string target, bool isFolder = false)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                string label = isFolder ? "thư mục" : "trang web";
                MessageBox.Show(
                    "Không mở được " + label + ":\n" + target + "\n\n" + ex.Message,
                    "Lỗi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void TestLocalBtn_Click(object sender, RoutedEventArgs e)
        {
            _testCts?.Cancel();
            _testCts = new CancellationTokenSource();

            testLocalBtn.IsEnabled = false;
            testLocalResultText.Text = "⏳ Đang khởi động…";
            testLocalResultText.Foreground = Brushes.Gray;

            var temp = BuildCurrentSettings();
            var progress = new Progress<string>(msg =>
            {
                testLocalResultText.Text = msg;
            });

            try
            {
                string answer = await _localLlama.AskAsync(
                    temp,
                    "Bạn là trợ lý gia phả Việt Nam.",
                    "Trả lời ngắn gọn bằng tiếng Việt: Xin chào!",
                    null,
                    progress,
                    _testCts.Token);

                testLocalResultText.Text = "✅ OK — " + Truncate(answer, 80);
                testLocalResultText.Foreground = new SolidColorBrush(Color.FromRgb(0, 128, 0));
            }
            catch (OperationCanceledException)
            {
                testLocalResultText.Text = "Đã hủy.";
                testLocalResultText.Foreground = Brushes.Gray;
            }
            catch (Exception ex)
            {
                testLocalResultText.Text = "❌ " + ex.Message.Split('\n')[0];
                testLocalResultText.Foreground = Brushes.Red;
            }
            finally
            {
                testLocalBtn.IsEnabled = true;
                RefreshLocalLlamaGuideText();
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }

            return text.Substring(0, max) + "…";
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
            string backendMode;
            if (localLlamaModeRadio.IsChecked == true)
            {
                backendMode = AiBackendModes.LocalLlama;
            }
            else if (apiModeRadio.IsChecked == true)
            {
                backendMode = AiBackendModes.CloudApi;
            }
            else
            {
                backendMode = AiBackendModes.RuleEngine;
            }

            int provIdx = providerCombo.SelectedIndex;
            string provKey = provIdx >= 0 && provIdx < AiProviderConfig.Providers.Count
                ? AiProviderConfig.Providers[provIdx].Key
                : "gemini";

            string apiKey = _keyVisible ? apiKeyVisible.Text : apiKeyBox.Password;
            // IsEditable=True nên dùng Text — cho phép model ID tùy ý không có trong list
            string model = !string.IsNullOrWhiteSpace(modelCombo.Text)
                ? modelCombo.Text
                : (modelCombo.SelectedItem as string ?? "gemini-2.0-flash");

            bool isCloud = backendMode == AiBackendModes.CloudApi;

            return new AiSettings
            {
                BackendMode = backendMode,
                Provider = provKey,
                Model = model,
                IsEnabled = enabledCheck.IsChecked == true,
                UseLocalRuleEngine = backendMode == AiBackendModes.RuleEngine,
                LocalLlamaModelPath = localModelPathBox?.Text?.Trim(),
                LocalLlamaPort = _current != null ? _current.LocalLlamaPort : 0,
                UseLlmForIntentParse = _current != null ? _current.UseLlmForIntentParse : true,
                ApiKey = isCloud ? apiKey : null,
                EncryptedApiKey = (isCloud && !string.IsNullOrEmpty(apiKey)) ? "__dpapi__" : null
            };
        }

        private void UpdateTestButtonState()
        {
            bool isApiMode = apiModeRadio.IsChecked == true;
            string key = _keyVisible ? apiKeyVisible.Text : apiKeyBox.Password;
            testBtn.IsEnabled = isApiMode && !string.IsNullOrWhiteSpace(key);
            if (testLocalBtn != null)
            {
                testLocalBtn.IsEnabled = localLlamaModeRadio.IsChecked == true;
            }
        }

        private static AiSettings CloneSettings(AiSettings s)
        {
            return new AiSettings
            {
                BackendMode = s.BackendMode,
                Provider = s.Provider,
                Model = s.Model,
                IsEnabled = s.IsEnabled,
                UseLocalRuleEngine = s.UseLocalRuleEngine,
                LocalLlamaModelPath = s.LocalLlamaModelPath,
                LocalLlamaPort = s.LocalLlamaPort,
                UseLlmForIntentParse = s.UseLlmForIntentParse,
                ApiKey = s.ApiKey,
                EncryptedApiKey = s.EncryptedApiKey
            };
        }
    }
}
