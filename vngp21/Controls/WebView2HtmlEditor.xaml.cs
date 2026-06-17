using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Newtonsoft.Json;

namespace vietnamgiapha.Controls
{
    /// <summary>
    /// Soạn HTML tư liệu gia phả qua WebView2 (contenteditable) — thay Smith HtmlEditor.
    /// </summary>
    public partial class WebView2HtmlEditor : UserControl
    {
        private bool _isReady;
        private string _pendingHtml = string.Empty;
        private string _lastLoadedHtml = string.Empty;

        public WebView2HtmlEditor()
        {
            InitializeComponent();
            Loaded += WebView2HtmlEditor_Loaded;
        }

        /// <summary>Sự kiện tương thích HtmlEditor cũ — WebView2 đã sẵn sàng.</summary>
        public event EventHandler DocumentReady;

        /// <summary>Gán HTML hiển thị/sửa (fragment hoặc full document).</summary>
        public string ContentHtml
        {
            get
            {
                return GetContentHtmlAsync().GetAwaiter().GetResult();
            }
            set
            {
                SetContentHtml(value);
            }
        }

        public void SetContentHtml(string html)
        {
            _pendingHtml = html ?? string.Empty;
            if (_isReady)
            {
                _ = LoadHtmlInternalAsync(_pendingHtml);
            }
        }

        public async Task<string> GetContentHtmlAsync()
        {
            if (!_isReady || browser.CoreWebView2 == null)
            {
                return _pendingHtml ?? string.Empty;
            }

            try
            {
                string json = await browser.CoreWebView2.ExecuteScriptAsync(
                    "(function(){ if(!document.body){return '';} return document.body.innerHTML; })();");
                return JsonConvert.DeserializeObject<string>(json) ?? string.Empty;
            }
            catch
            {
                return _lastLoadedHtml ?? string.Empty;
            }
        }

        private async void WebView2HtmlEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isReady)
            {
                return;
            }

            try
            {
                await browser.EnsureCoreWebView2Async(null);
                _isReady = true;
                await LoadHtmlInternalAsync(_pendingHtml);
                DocumentReady?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                browser.NavigateToString(BuildErrorHtml(ex.Message));
            }
        }

        private async Task LoadHtmlInternalAsync(string html)
        {
            if (browser.CoreWebView2 == null)
            {
                return;
            }

            string documentHtml = WrapEditableHtml(html);
            _lastLoadedHtml = html ?? string.Empty;
            browser.NavigateToString(documentHtml);
            await Task.Delay(50);
        }

        /// <summary>Bọc fragment HTML trong trang contenteditable — giữ tài liệu cũ nếu đã là HTML đầy đủ.</summary>
        private static string WrapEditableHtml(string fragment)
        {
            string text = fragment ?? string.Empty;
            string trimmed = text.TrimStart();
            if (trimmed.StartsWith("<!", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmed.IndexOf("contenteditable", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return text;
                }

                // Thêm contenteditable vào body nếu thiếu
                int bodyIdx = trimmed.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
                if (bodyIdx >= 0)
                {
                    int closeTag = trimmed.IndexOf('>', bodyIdx);
                    if (closeTag > bodyIdx
                        && trimmed.IndexOf("contenteditable", bodyIdx, closeTag - bodyIdx, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return trimmed.Insert(closeTag, " contenteditable=\"true\"");
                    }
                }

                return text;
            }

            var sb = new StringBuilder(256 + text.Length);
            sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"/>");
            sb.Append("<style>body{font-family:'Segoe UI',sans-serif;font-size:14pt;margin:8px;line-height:1.45;}</style>");
            sb.Append("</head><body contenteditable=\"true\">");
            sb.Append(text);
            sb.Append("</body></html>");
            return sb.ToString();
        }

        private static string BuildErrorHtml(string message)
        {
            string safe = WebUtilityHtmlEncode(message ?? "Không khởi tạo được WebView2.");
            return "<!DOCTYPE html><html><body style=\"font-family:Segoe UI;padding:12px;color:#842029;\">"
                + "<p><b>WebView2 chưa sẵn sàng</b></p><p>"
                + safe
                + "</p><p>Cài <i>Microsoft Edge WebView2 Runtime</i> từ Microsoft rồi mở lại tab Tư liệu.</p></body></html>";
        }

        private static string WebUtilityHtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;");
        }
    }
}
