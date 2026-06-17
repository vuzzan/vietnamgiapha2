using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Gọi llama-server OpenAI-compatible (/v1/chat/completions).</summary>
    public sealed class LocalLlamaChatService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        /// <summary>Hỏi đáp có stream — gọi <paramref name="onChunk"/> từng đoạn text.</summary>
        public async Task<string> AskStreamAsync(
            AiSettings settings,
            string systemPrompt,
            string userMessage,
            IReadOnlyList<(string role, string content)> history,
            IProgress<string> progress,
            Action<string> onChunk,
            CancellationToken ct = default)
        {
            await LocalLlamaHost.Instance.EnsureRunningAsync(settings, progress, ct)
                .ConfigureAwait(false);

            int port = LocalLlamaHost.Instance.ActivePort;
            string url = "http://127.0.0.1:" + port + "/v1/chat/completions";

            var messages = BuildMessages(systemPrompt, userMessage, history);
            var body = new
            {
                model = "local",
                messages,
                temperature = 0.6,
                max_tokens = 1024,
                stream = true
            };

            string json = JsonSerializer.Serialize(body);
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "local");
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resp = await _http.SendAsync(
                    req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
                {
                    return await OpenAiStreamHelper.ReadChatCompletionStreamAsync(
                        resp, onChunk, ct).ConfigureAwait(false);
                }
            }
        }

        /// <summary>Một lần gọi không stream — dùng parse intent JSON.</summary>
        public async Task<string> AskPlainAsync(
            AiSettings settings,
            string systemPrompt,
            string userMessage,
            IProgress<string> progress,
            CancellationToken ct = default)
        {
            await LocalLlamaHost.Instance.EnsureRunningAsync(settings, progress, ct)
                .ConfigureAwait(false);

            int port = LocalLlamaHost.Instance.ActivePort;
            string url = "http://127.0.0.1:" + port + "/v1/chat/completions";

            var messages = new List<object>();
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            messages.Add(new { role = "user", content = userMessage ?? "" });

            var body = new
            {
                model = "local",
                messages,
                temperature = 0.1,
                max_tokens = 320,
                stream = false
            };

            string json = JsonSerializer.Serialize(body);
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "local");
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                {
                    string raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            "AI local lỗi " + (int)resp.StatusCode + ": " + Truncate(raw, 500));
                    }

                    return ParseOpenAiResponse(raw);
                }
            }
        }

        /// <summary>Hỏi đáp một lượt — tự khởi động sidecar nếu cần.</summary>
        public async Task<string> AskAsync(
            AiSettings settings,
            string systemPrompt,
            string userMessage,
            IReadOnlyList<(string role, string content)> history,
            IProgress<string> progress,
            CancellationToken ct = default)
        {
            await LocalLlamaHost.Instance.EnsureRunningAsync(settings, progress, ct)
                .ConfigureAwait(false);

            int port = LocalLlamaHost.Instance.ActivePort;
            string url = "http://127.0.0.1:" + port + "/v1/chat/completions";

            var messages = BuildMessages(systemPrompt, userMessage, history);
            var body = new
            {
                model = "local",
                messages,
                temperature = 0.6,
                max_tokens = 1024
            };

            string json = JsonSerializer.Serialize(body);
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "local");
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resp = await _http.SendAsync(req, ct).ConfigureAwait(false))
                {
                    string raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            "AI local lỗi " + (int)resp.StatusCode + ": " + Truncate(raw, 500));
                    }

                    return ParseOpenAiResponse(raw);
                }
            }
        }

        private static List<object> BuildMessages(
            string systemPrompt,
            string userMessage,
            IReadOnlyList<(string role, string content)> history)
        {
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new { role = "system", content = systemPrompt });
            }

            if (history != null)
            {
                int start = Math.Max(0, history.Count - 8);
                for (int i = start; i < history.Count; i++)
                {
                    var (role, content) = history[i];
                    string apiRole = role == "assistant" ? "assistant" : "user";
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        messages.Add(new { role = apiRole, content });
                    }
                }
            }

            messages.Add(new { role = "user", content = userMessage ?? "" });
            return messages;
        }

        private static string ParseOpenAiResponse(string raw)
        {
            using (var doc = JsonDocument.Parse(raw))
            {
                var root = doc.RootElement;
                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var first = choices[0];
                    if (first.TryGetProperty("message", out var msg)
                        && msg.TryGetProperty("content", out var content))
                    {
                        return content.GetString() ?? "";
                    }
                }
            }

            throw new InvalidOperationException("Không đọc được phản hồi AI local: " + Truncate(raw, 300));
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
            {
                return text ?? "";
            }

            return text.Substring(0, max) + "…";
        }
    }
}
