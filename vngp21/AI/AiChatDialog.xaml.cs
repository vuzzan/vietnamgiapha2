using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MahApps.Metro.Controls;

namespace vietnamgiapha.AI
{
    public partial class AiChatDialog : MetroWindow
    {
        private readonly AiApiService _service;
        private readonly GiaPhaIntentOrchestrator _intentOrchestrator = new GiaPhaIntentOrchestrator();
        private readonly GiaPhaQueryEngine _ruleEngine;
        private FamilyViewModel _currentFamily;   // gia đình đang chọn (có thể null)
        private FamilyViewModel _fileRoot;         // gốc cây gia phả
        private CancellationTokenSource _cts;
        private bool _isBusy;

        // Lịch sử hội thoại (để AI nhớ ngữ cảnh qua nhiều lượt)
        private readonly List<(string role, string content)> _history =
            new List<(string, string)>();

        // Lịch sử câu hỏi người dùng — phím ↑ / nút ↑ để gọi lại
        private readonly List<string> _questionHistory = new List<string>();
        private int _historyIndex = -1; // -1 = chưa điều hướng
        private string _draftBeforeHistory; // nháp đang gõ trước khi duyệt lịch sử

        /// <summary>Sau khi sửa gia phả qua chat — MainWindow chọn lại node và rebuild index.</summary>
        public event Action<FamilyViewModel> AfterEditApplied;

        public AiChatDialog(AiApiService service, GiaPhaQueryEngine ruleEngine,
            FamilyViewModel fileRoot, FamilyViewModel currentFamily)
        {
            InitializeComponent();
            _service = service;
            _ruleEngine = ruleEngine;
            _fileRoot = fileRoot;
            _currentFamily = currentFamily;

            _intentOrchestrator.EditActionHandler = ExecuteEditOnUiThreadAsync;

            Loaded += (_, __) =>
            {
                FocusInputBox();
                GiaPhaRuleEngineLoader.ReloadFromDisk();
                GiaPhaIntentPromptLoader.ReloadFromDisk();
            };
            UpdateHistoryNavButtons();

            // Cập nhật tiêu đề theo chế độ
            UpdateModeIndicator();

            // Chào mừng
            AppendAiBubble(BuildWelcomeMessage());
        }

        /// <summary>Cập nhật nhãn chế độ hiển thị trên header.</summary>
        public void UpdateModeIndicator()
        {
            var settings = _service?.Settings;
            string mode = settings?.BackendMode ?? AiBackendModes.RuleEngine;

            if (AiBackendModes.IsLocalLlama(mode))
            {
                statusText.Text = "Qwen: hiểu câu hỏi → fact engine";
            }
            else if (AiBackendModes.IsCloudApi(mode))
            {
                statusText.Text = "Fact engine (rule) — cloud API tắt trả lời tự do";
            }
            else
            {
                statusText.Text = "Fact engine (rule)";
            }
        }

        // ── Giao diện ─────────────────────────────────────────────────────────

        private string BuildWelcomeMessage()
        {
            var settings = _service?.Settings;
            string mode = settings?.BackendMode ?? AiBackendModes.RuleEngine;

            string modeNote;
            if (AiBackendModes.IsLocalLlama(mode))
            {
                modeNote = "🦙 Qwen chỉ hiểu câu hỏi (intent); câu trả lời từ dữ liệu gia phả (fact).";
            }
            else if (AiBackendModes.IsCloudApi(mode))
            {
                modeNote = "📚 Fact từ engine — chưa dùng cloud để trả lời (chỉ rule parse).";
            }
            else
            {
                modeNote = "📚 Fact từ engine (rule) — không cần API key.";
            }

            string ctx = _currentFamily?.familyInfo != null
                ? "Bạn đang xem gia đình đời " + _currentFamily.familyInfo.FamilyLevel
                  + ": " + (_currentFamily.familyInfo.Name0 ?? "") + "."
                : "Hỏi tôi về bất kỳ ai trong gia phả!";

            string examples = "💡 Tra cứu:\n• \"Con của Nguyễn Văn Chính\"\n• \"Đời 4 có ai?\"\n\n✏️ Sửa (chọn gia đình trên cây trước):\n• \"Thêm người vào gia đình\"\n• \"Thêm gia đình con\"\n• \"Đổi tên A thành B\"";
            string rulesHint = "📝 Rule base: ai\\rules\\ | Qwen: ai\\intent\\ → sửa txt → ↻ Rules.";

            return $"👋 Xin chào! {ctx}\n\n{modeNote}\n\n{examples}\n\n{rulesHint}";
        }

        private void AppendUserBubble(string text)
        {
            var border = new Border { Style = (Style)Resources["UserBubble"] };
            border.Child = MakeSelectableTextBox(text, Brushes.White, 13, isUserBubble: true);
            chatPanel.Children.Add(border);
            ScrollToBottom();
        }

        private void AppendAiBubble(string text)
        {
            var border = new Border { Style = (Style)Resources["AiBubble"] };
            border.Child = MakeSelectableTextBox(text,
                new SolidColorBrush(Color.FromRgb(30, 40, 60)), 13, isUserBubble: false);
            chatPanel.Children.Add(border);
            ScrollToBottom();
        }

        /// <summary>Bong bóng AI rỗng — cập nhật text khi stream.</summary>
        private TextBox BeginStreamingAiBubble()
        {
            var border = new Border { Style = (Style)Resources["AiBubble"] };
            var tb = MakeSelectableTextBox("",
                new SolidColorBrush(Color.FromRgb(30, 40, 60)), 13, isUserBubble: false);
            border.Child = tb;
            chatPanel.Children.Add(border);
            ScrollToBottom();
            return tb;
        }

        private void AppendStreamChunk(TextBox streamBox, string chunk, StringBuilder accumulator)
        {
            if (streamBox == null || string.IsNullOrEmpty(chunk))
            {
                return;
            }

            accumulator?.Append(chunk);
            streamBox.Text = accumulator != null ? accumulator.ToString() : streamBox.Text + chunk;
            ScrollToBottom();
        }

        /// <summary>
        /// Tạo TextBox read-only trong suốt — trông như TextBlock nhưng cho phép chọn và copy text.
        /// </summary>
        private TextBox MakeSelectableTextBox(string text, Brush foreground, double fontSize,
            bool isUserBubble)
        {
            // Dùng BubbleTextBoxStyle định nghĩa trong XAML — bỏ toàn bộ chrome mặc định
            var tb = new TextBox
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                Foreground = foreground,
                FontSize = fontSize,
                // Highlight màu xanh mờ để thấy vùng chọn rõ
                SelectionBrush = isUserBubble
                    ? new SolidColorBrush(Color.FromArgb(80, 255, 255, 255))
                    : new SolidColorBrush(Color.FromArgb(100, 46, 95, 163)),
                Style = (Style)Resources["BubbleTextBoxStyle"],
            };
            return tb;
        }

        private Border AppendTypingIndicator()
        {
            var border = new Border { Style = (Style)Resources["AiBubble"] };
            border.Child = new TextBlock
            {
                Text = "⏳ Đang trả lời...",
                Foreground = Brushes.Gray,
                FontSize = 12,
                FontStyle = FontStyles.Italic
            };
            chatPanel.Children.Add(border);
            ScrollToBottom();
            return border;
        }

        private void ScrollToBottom()
        {
            chatScroll.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => chatScroll.ScrollToEnd()));
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;
            sendBtn.IsEnabled = !busy;
            inputBox.IsEnabled = !busy;
            if (reloadRulesBtn != null)
            {
                reloadRulesBtn.IsEnabled = !busy;
            }

            if (openRulesBtn != null)
            {
                openRulesBtn.IsEnabled = !busy;
            }

            UpdateHistoryNavButtons();
            if (busy)
            {
                statusText.Text = "Đang xử lý...";
            }
            else
            {
                // Khôi phục nhãn chế độ sau khi xử lý xong
                UpdateModeIndicator();
            }
        }

        /// <summary>Đưa focus về ô nhập câu hỏi — sau gửi hoặc mở dialog.</summary>
        private void FocusInputBox()
        {
            if (inputBox == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Input,
                new Action(() =>
                {
                    if (!inputBox.IsEnabled)
                    {
                        return;
                    }

                    inputBox.Focus();
                    Keyboard.Focus(inputBox);
                    inputBox.CaretIndex = inputBox.Text?.Length ?? 0;
                }));
        }

        // ── Gửi câu hỏi ──────────────────────────────────────────────────────

        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // ↑↓ duyệt lịch sử câu đã gửi (bắt trước TextBox để không bị nuốt phím)
            if (e.Key == Key.Up && _questionHistory.Count > 0 && !_isBusy)
            {
                e.Handled = true;
                ShowPreviousQuestion();
                return;
            }

            if (e.Key == Key.Down && _historyIndex >= 0 && !_isBusy)
            {
                e.Handled = true;
                ShowNextQuestion();
                return;
            }

            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                await SendMessageAsync();
            }
        }

        private void HistoryUpBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowPreviousQuestion();
        }

        private void HistoryDownBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowNextQuestion();
        }

        /// <summary>Lùi về câu hỏi đã gửi trước đó (mới → cũ).</summary>
        private void ShowPreviousQuestion()
        {
            if (_questionHistory.Count == 0 || _isBusy)
            {
                return;
            }

            if (_historyIndex < 0)
            {
                _draftBeforeHistory = inputBox.Text ?? "";
                _historyIndex = _questionHistory.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
            }

            inputBox.Text = _questionHistory[_historyIndex];
            inputBox.CaretIndex = inputBox.Text.Length;
            inputBox.Focus();
            UpdateHistoryNavButtons();
        }

        /// <summary>Tiến tới câu hỏi mới hơn; hết lịch sử thì khôi phục nháp.</summary>
        private void ShowNextQuestion()
        {
            if (_historyIndex < 0 || _isBusy)
            {
                return;
            }

            if (_historyIndex < _questionHistory.Count - 1)
            {
                _historyIndex++;
                inputBox.Text = _questionHistory[_historyIndex];
            }
            else
            {
                _historyIndex = -1;
                inputBox.Text = _draftBeforeHistory ?? "";
            }

            inputBox.CaretIndex = inputBox.Text?.Length ?? 0;
            inputBox.Focus();
            UpdateHistoryNavButtons();
        }

        private void UpdateHistoryNavButtons()
        {
            if (historyUpBtn == null || historyDownBtn == null)
            {
                return;
            }

            historyUpBtn.IsEnabled = !_isBusy && _questionHistory.Count > 0;
            historyDownBtn.IsEnabled = !_isBusy && _historyIndex >= 0;
        }

        private async Task SendMessageAsync()
        {
            if (_isBusy)
            {
                return;
            }

            string question = inputBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(question))
            {
                return;
            }

            inputBox.Text = "";
            _historyIndex = -1;
            _draftBeforeHistory = null;

            // Lưu câu hỏi vào lịch sử phím ↑ (tránh trùng liên tiếp)
            if (_questionHistory.Count == 0
                || _questionHistory[_questionHistory.Count - 1] != question)
            {
                _questionHistory.Add(question);
                // Giới hạn 50 câu hỏi gần nhất
                if (_questionHistory.Count > 50)
                {
                    _questionHistory.RemoveAt(0);
                }
            }

            AppendUserBubble(question);

            var settings = _service?.Settings;
            _history.Add(("user", question));

            var indicator = AppendTypingIndicator();
            SetBusy(true);
            _cts = new CancellationTokenSource();

            try
            {
                var progress = new Progress<string>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg))
                    {
                        statusText.Text = msg;
                    }
                });

                GiaPhaQueryResult result = await _intentOrchestrator.AskAsync(
                    question,
                    _ruleEngine,
                    settings,
                    _currentFamily,
                    _fileRoot,
                    progress,
                    _cts.Token);

                chatPanel.Children.Remove(indicator);

                string answer = result != null && result.Success
                    ? result.AnswerText
                    : (result != null ? result.AnswerText : "❌ Không có kết quả.");

                if (!string.IsNullOrEmpty(result?.StatusHint))
                {
                    statusText.Text = result.StatusHint;
                }

                _history.Add(("assistant", answer ?? ""));

                if (_history.Count > 20)
                {
                    _history.RemoveRange(0, _history.Count - 20);
                }

                AppendAiBubble(answer);

                if (result?.AffectedFamily != null)
                {
                    _currentFamily = result.AffectedFamily;
                    AfterEditApplied?.Invoke(result.AffectedFamily);
                }
            }
            catch (OperationCanceledException)
            {
                chatPanel.Children.Remove(indicator);
                AppendAiBubble("(Đã hủy)");
            }
            catch (Exception ex)
            {
                chatPanel.Children.Remove(indicator);
                AppendAiBubble("❌ Lỗi: " + ex.Message);
            }
            finally
            {
                SetBusy(false);
                UpdateModeIndicator();
                FocusInputBox();
            }
        }

        /// <summary>Biên tập gia phả phải chạy trên UI thread (ObservableCollection).</summary>
        private Task<GiaPhaQueryResult> ExecuteEditOnUiThreadAsync(
            GiaPhaIntent intent,
            GiaPhaParseSource source)
        {
            var tcs = new TaskCompletionSource<GiaPhaQueryResult>();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    GiaPhaQueryResult editResult = GiaPhaEditActionExecutor.Execute(
                        intent,
                        _currentFamily,
                        _fileRoot,
                        _ruleEngine,
                        source);
                    tcs.TrySetResult(editResult);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }));
            return tcs.Task;
        }

        // ── Xây context AI ────────────────────────────────────────────────────

        private string BuildFamilyContext(string question)
        {
            var sb = new System.Text.StringBuilder();

            // 1. Tóm tắt file (luôn có)
            if (_fileRoot != null)
            {
                sb.AppendLine(FamilyContextSerializer.SerializeTreeSummary(_fileRoot));
            }

            // 2. Chi tiết gia đình đang chọn (nếu có)
            if (_currentFamily?.familyInfo != null)
            {
                sb.AppendLine(FamilyContextSerializer.SerializeFamilyDetail(_currentFamily, _fileRoot));
            }

            // 3. Tìm kiếm tên trong câu hỏi và đưa kết quả vào context
            if (_fileRoot != null)
            {
                string kw = ExtractKeyword(question);
                if (!string.IsNullOrEmpty(kw))
                {
                    string searchResult = FamilyContextSerializer.SerializeSearchResults(_fileRoot, kw);
                    if (!searchResult.StartsWith("Không tìm thấy"))
                    {
                        sb.AppendLine(searchResult);
                    }
                }
            }

            return sb.ToString();
        }

        private string BuildSystemPrompt(string familyContext)
        {
            return "Bạn là trợ lý gia phả Việt Nam, thông thạo tiếng Việt.\n"
                + "Trả lời ngắn gọn, chính xác, dựa trên DỮ LIỆU được cung cấp.\n"
                + "Nếu không có thông tin trong dữ liệu, hãy nói thẳng là không có.\n"
                + "Không bịa đặt thông tin.\n\n"
                + "=== DỮ LIỆU GIA PHẢ ===\n"
                + familyContext;
        }

        private string BuildUserMessageWithHistory(string currentQuestion)
        {
            // Đưa tối đa 4 lượt hội thoại trước vào để AI nhớ ngữ cảnh
            if (_history.Count <= 1)
            {
                return currentQuestion;
            }

            var sb = new System.Text.StringBuilder();
            int start = Math.Max(0, _history.Count - 9); // 4 lượt = 8 entries + 1 hiện tại
            for (int i = start; i < _history.Count - 1; i++)
            {
                var (role, content) = _history[i];
                sb.AppendLine(role == "user" ? "[Người dùng]: " + content : "[Trợ lý]: " + content);
            }

            sb.AppendLine("[Người dùng]: " + currentQuestion);
            return sb.ToString();
        }

        // ── Trích xuất từ khóa từ câu hỏi ────────────────────────────────────

        private static string ExtractKeyword(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return null;
            }

            // Tìm chuỗi họ tên Việt Nam trong câu (chuỗi ký tự liên tiếp có dấu, bắt đầu Hoa)
            var match = System.Text.RegularExpressions.Regex.Match(
                question,
                @"[A-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĐƠƯẠẶẬẮẦẺẼẸẾỀỆỈỊỌỘỞỜỚỢỤƯỪỨỰỶỸỴ][a-zA-ZÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝĂĐƠƯàáâãèéêìíòóôõùúýăđơưạặậắầẻẽẹếềệỉịọộởờớợụưừứựỷỹỵ\s]{3,}");

            return match.Success ? match.Value.Trim() : null;
        }

        // ── Buttons ───────────────────────────────────────────────────────────

        private void ReloadRulesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isBusy)
            {
                return;
            }

            GiaPhaRuleEngineSnapshot ruleSnap = GiaPhaRuleEngineLoader.ReloadFromDisk();
            GiaPhaIntentRulesSnapshot qwenSnap = GiaPhaIntentPromptLoader.ReloadFromDisk();
            string summary = GiaPhaRuleEngineLoader.FormatReloadSummary(ruleSnap)
                + Environment.NewLine + Environment.NewLine
                + GiaPhaIntentPromptLoader.FormatReloadSummary(qwenSnap);
            GiaPhaIntentTraceLog.WriteBlock("=== RELOAD RULES ===", summary);
            AppendAiBubble(summary);
            statusText.Text = "Rules đã tải lại " + ruleSnap.LoadedAt.ToString("HH:mm:ss");
            FocusInputBox();
        }

        private void OpenRulesBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!TryOpenAiConfigFolder())
            {
                AppendAiBubble("❌ Không mở được thư mục ai\\");
            }
        }

        private static bool TryOpenAiConfigFolder()
        {
            try
            {
                LocalLlamaPaths.EnsureAiFoldersExist();
                GiaPhaRuleEngineLoader.EnsureRulesFolderExists();
                GiaPhaIntentPromptLoader.EnsureIntentFolderExists();
                System.Diagnostics.Process.Start("explorer.exe", LocalLlamaPaths.AiRootDirectory);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ClearBtn_Click(object sender, RoutedEventArgs e)
        {
            chatPanel.Children.Clear();
            _history.Clear();
            AppendAiBubble("🗑️ Đã xóa lịch sử. Bắt đầu cuộc trò chuyện mới!");
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new AiSettingsDialog(_service) { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                // Cập nhật chỉ báo chế độ sau khi lưu cài đặt
                UpdateModeIndicator();
            }
        }

        /// <summary>Cập nhật gia đình đang chọn khi người dùng click sang gia đình khác.</summary>
        public void UpdateCurrentFamily(FamilyViewModel family)
        {
            _currentFamily = family;
            if (family?.familyInfo != null)
            {
                string name = family.familyInfo.Name0 ?? "";
                AppendAiBubble("📍 Đang xem: **" + name + "** (Đời " + family.familyInfo.FamilyLevel + ")");
            }
        }

        /// <summary>
        /// Gọi khi người dùng mở file gia phả khác — cập nhật root mới và thông báo trong chat.
        /// </summary>
        public void UpdateFileRoot(FamilyViewModel newRoot, FamilyViewModel newSelected)
        {
            _fileRoot = newRoot;
            _currentFamily = newSelected;

            string rootName = newRoot?.familyInfo?.Name0 ?? newRoot?.familyInfo?.Name ?? "(chưa có tên)";
            AppendAiBubble($"🔄 Đã tải gia phả mới: {rootName}\nDatabase tra cứu đã được cập nhật.");
        }
    }
}
