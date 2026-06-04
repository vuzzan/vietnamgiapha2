using System;
using System.Collections.Generic;
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
        private readonly GiaPhaQueryEngine _ruleEngine;
        private FamilyViewModel _currentFamily;   // gia đình đang chọn (có thể null)
        private FamilyViewModel _fileRoot;         // gốc cây gia phả
        private CancellationTokenSource _cts;
        private bool _isBusy;

        // Lịch sử hội thoại (để AI nhớ ngữ cảnh qua nhiều lượt)
        private readonly List<(string role, string content)> _history =
            new List<(string, string)>();

        // Lịch sử câu hỏi người dùng — phím ↑ để gọi lại
        private readonly List<string> _questionHistory = new List<string>();
        private int _historyIndex = -1; // -1 = chưa điều hướng

        public AiChatDialog(AiApiService service, GiaPhaQueryEngine ruleEngine,
            FamilyViewModel fileRoot, FamilyViewModel currentFamily)
        {
            InitializeComponent();
            _service = service;
            _ruleEngine = ruleEngine;
            _fileRoot = fileRoot;
            _currentFamily = currentFamily;

            // Cập nhật tiêu đề theo chế độ
            UpdateModeIndicator();

            // Chào mừng
            AppendAiBubble(BuildWelcomeMessage());
        }

        /// <summary>Cập nhật nhãn chế độ hiển thị trên header.</summary>
        public void UpdateModeIndicator()
        {
            bool isLocal = _service?.Settings?.UseLocalRuleEngine == true
                          || !(_service?.IsConfigured == true);
            statusText.Text = isLocal ? "Chế độ tra cứu nội bộ" : "Chế độ AI API";
        }

        // ── Giao diện ─────────────────────────────────────────────────────────

        private string BuildWelcomeMessage()
        {
            bool isLocal = _service?.Settings?.UseLocalRuleEngine == true
                          || !(_service?.IsConfigured == true);

            string modeNote = isLocal
                ? "📚 Chế độ tra cứu nội bộ — không cần API key."
                : "🤖 Chế độ AI API — " + (_service?.Settings?.Provider ?? "AI") + " đang hoạt động.";

            string ctx = _currentFamily?.familyInfo != null
                ? "Bạn đang xem gia đình đời " + _currentFamily.familyInfo.FamilyLevel
                  + ": " + (_currentFamily.familyInfo.Name0 ?? "") + "."
                : "Hỏi tôi về bất kỳ ai trong gia phả!";

            string examples = isLocal
                ? "💡 Ví dụ:\n• \"Con của Nguyễn Văn Chính\"\n• \"Mộ của Trần Thị Hà ở đâu?\"\n• \"Đời 4 có ai?\"\n• \"Thủy tổ là ai?\"\n• \"Lương Văn A và Trần Thị B có quan hệ gì?\""
                : "💡 Ví dụ: \"Trần Công Đào ở đời mấy?\" hoặc \"Ai là con của người này?\"";

            return $"👋 Xin chào! {ctx}\n\n{modeNote}\n\n{examples}";
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

        // ── Gửi câu hỏi ──────────────────────────────────────────────────────

        private async void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            await SendMessageAsync();
        }

        private async void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                await SendMessageAsync();
                return;
            }

            // Phím ↑ — gọi lại câu hỏi trước trong lịch sử
            if (e.Key == Key.Up && _questionHistory.Count > 0)
            {
                // Chỉ điều hướng khi cursor đang ở dòng đầu (hoặc text một dòng)
                bool isOnFirstLine = inputBox.GetLineIndexFromCharacterIndex(inputBox.CaretIndex) == 0;
                if (isOnFirstLine)
                {
                    e.Handled = true;
                    if (_historyIndex < 0)
                    {
                        // Lần đầu bấm ↑ — lưu text hiện tại (nếu có) rồi nhảy về câu cuối
                        _historyIndex = _questionHistory.Count - 1;
                    }
                    else if (_historyIndex > 0)
                    {
                        _historyIndex--;
                    }

                    inputBox.Text = _questionHistory[_historyIndex];
                    inputBox.CaretIndex = inputBox.Text.Length;
                }

                return;
            }

            // Phím ↓ — đi tới câu hỏi sau (hoặc xóa nếu hết)
            if (e.Key == Key.Down && _historyIndex >= 0)
            {
                bool isOnLastLine = inputBox.GetLineIndexFromCharacterIndex(inputBox.CaretIndex)
                                    == inputBox.LineCount - 1;
                if (isOnLastLine)
                {
                    e.Handled = true;
                    if (_historyIndex < _questionHistory.Count - 1)
                    {
                        _historyIndex++;
                        inputBox.Text = _questionHistory[_historyIndex];
                        inputBox.CaretIndex = inputBox.Text.Length;
                    }
                    else
                    {
                        // Đã qua câu mới nhất → reset
                        _historyIndex = -1;
                        inputBox.Text = "";
                    }
                }
            }
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
            _historyIndex = -1; // Reset điều hướng lịch sử khi gửi câu hỏi mới

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

            // Chọn chế độ xử lý: rule-based hoặc API
            bool useLocal = _service?.Settings?.UseLocalRuleEngine == true
                           || !(_service?.IsConfigured == true);

            if (useLocal)
            {
                // Chế độ rule-based — đồng bộ, không cần async
                string answer = _ruleEngine != null
                    ? _ruleEngine.Query(question, _currentFamily)
                    : "⚠️ Engine tra cứu chưa được khởi tạo. Hãy mở file gia phả trước.";

                _history.Add(("user", question));
                _history.Add(("assistant", answer));
                AppendAiBubble(answer);
                return;
            }

            // Chế độ API AI
            var indicator = AppendTypingIndicator();
            SetBusy(true);
            _cts = new CancellationTokenSource();

            try
            {
                // Xây dựng context từ dữ liệu gia phả
                string familyContext = BuildFamilyContext(question);

                // System prompt chứa context
                string systemPrompt = BuildSystemPrompt(familyContext);

                // Thêm vào lịch sử
                _history.Add(("user", question));

                // Gọi AI
                string fullUserMsg = BuildUserMessageWithHistory(question);
                string answer = await _service.AskAsync(systemPrompt, fullUserMsg, _cts.Token);

                _history.Add(("assistant", answer));

                // Giới hạn lịch sử: giữ 10 lượt gần nhất
                if (_history.Count > 20)
                {
                    _history.RemoveRange(0, _history.Count - 20);
                }

                chatPanel.Children.Remove(indicator);
                AppendAiBubble(answer);
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
            }
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
