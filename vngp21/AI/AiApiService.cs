using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Service gọi AI API — hỗ trợ Gemini và Groq (OpenAI-compatible).</summary>
    public sealed class AiApiService
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private AiSettings _settings;

        public AiSettings Settings => _settings;

        public bool IsConfigured => _settings != null && _settings.HasKey && _settings.IsEnabled;

        public AiApiService()
        {
            _settings = AiSettings.Load();
        }

        public void ApplySettings(AiSettings s)
        {
            _settings = s;
        }

        /// <summary>Gửi câu hỏi tới AI, trả về nội dung text.</summary>
        public async Task<string> AskAsync(
            string systemPrompt,
            string userMessage,
            CancellationToken ct = default)
        {
            if (_settings == null || !_settings.HasKey)
            {
                throw new InvalidOperationException("Chưa cài đặt API key. Vào menu AI → Cài đặt để thêm key.");
            }

            var provider = AiProviderConfig.Find(_settings.Provider);
            if (provider == null)
            {
                throw new InvalidOperationException("Provider không hợp lệ: " + _settings.Provider);
            }

            if (provider.Key == "gemini")
            {
                return await CallGeminiAsync(provider, systemPrompt, userMessage, ct);
            }

            // Groq và các provider OpenAI-compatible
            return await CallOpenAiCompatibleAsync(provider, systemPrompt, userMessage, ct);
        }

        /// <summary>Kiểm tra kết nối bằng câu hỏi nhỏ.</summary>
        public async Task<string> TestConnectionAsync(AiSettings s, CancellationToken ct = default)
        {
            var saved = _settings;
            _settings = s;
            try
            {
                var result = await AskAsync(
                    "Bạn là trợ lý gia phả.",
                    "Trả lời ngắn gọn: Xin chào!",
                    ct);
                return result;
            }
            finally
            {
                _settings = saved;
            }
        }

        // ── Gemini REST API ──────────────────────────────────────────────────

        private async Task<string> CallGeminiAsync(
            AiProviderInfo provider,
            string systemPrompt,
            string userMessage,
            CancellationToken ct)
        {
            string model = _settings.Model ?? "gemini-2.0-flash";
            string url = provider.EndpointTemplate
                .Replace("{model}", model)
                .Replace("{key}", _settings.ApiKey);

            // Gemini dùng "system_instruction" + "contents"
            var body = new
            {
                system_instruction = string.IsNullOrWhiteSpace(systemPrompt)
                    ? (object)null
                    : new { parts = new[] { new { text = systemPrompt } } },
                contents = new[]
                {
                    new { role = "user", parts = new[] { new { text = userMessage } } }
                },
                generationConfig = new { temperature = 0.7 }
            };

            var json = JsonSerializer.Serialize(body);
            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (var resp = await _http.SendAsync(req, ct))
                {
                    var raw = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException("Gemini lỗi " + (int)resp.StatusCode + ": " + raw);
                    }

                    return ParseGeminiResponse(raw);
                }
            }
        }

        private static string ParseGeminiResponse(string raw)
        {
            using (var doc = JsonDocument.Parse(raw))
            {
                var root = doc.RootElement;
                // candidates[0].content.parts[0].text
                if (root.TryGetProperty("candidates", out var cands) && cands.GetArrayLength() > 0)
                {
                    var first = cands[0];
                    if (first.TryGetProperty("content", out var content)
                        && content.TryGetProperty("parts", out var parts)
                        && parts.GetArrayLength() > 0
                        && parts[0].TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? "";
                    }
                }

                // Có thể bị block bởi safety filter
                if (root.TryGetProperty("promptFeedback", out var fb)
                    && fb.TryGetProperty("blockReason", out var reason))
                {
                    throw new InvalidOperationException("Bị chặn bởi bộ lọc an toàn: " + reason.GetString());
                }

                throw new InvalidOperationException("Không đọc được phản hồi Gemini: " + raw);
            }
        }

        // ── OpenAI-compatible (Groq, OpenAI…) ───────────────────────────────

        private async Task<string> CallOpenAiCompatibleAsync(
            AiProviderInfo provider,
            string systemPrompt,
            string userMessage,
            CancellationToken ct)
        {
            string url = provider.EndpointTemplate;
            string model = _settings.Model ?? provider.Models[0];

            var messages = string.IsNullOrWhiteSpace(systemPrompt)
                ? new object[] { new { role = "user", content = userMessage } }
                : new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                };

            var body = new { model, messages, temperature = 0.7 };
            var json = JsonSerializer.Serialize(body);

            using (var req = new HttpRequestMessage(HttpMethod.Post, url))
            {
                req.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (var resp = await _http.SendAsync(req, ct))
                {
                    var raw = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException("API lỗi " + (int)resp.StatusCode + ": " + raw);
                    }

                    return ParseOpenAiResponse(raw);
                }
            }
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

                throw new InvalidOperationException("Không đọc được phản hồi AI: " + raw);
            }
        }

        // ── Lấy danh sách model động từ API ─────────────────────────────────

        /// <summary>
        /// Gọi API của provider để lấy danh sách model hiện tại.
        /// OpenRouter không cần key (endpoint công khai).
        /// Trả về danh sách dự phòng hardcode nếu fetch thất bại.
        /// </summary>
        public static async Task<string[]> FetchModelsAsync(
            string apiKey, AiProviderInfo provider, CancellationToken ct = default)
        {
            if (provider == null || string.IsNullOrEmpty(provider.ModelsEndpoint))
                return provider != null ? provider.Models : new string[0];

            // Gemini nhúng key vào URL query string
            string url = provider.ModelsEndpoint.Replace("{key}", apiKey ?? "");

            using (var req = new HttpRequestMessage(HttpMethod.Get, url))
            {
                // Các provider dùng Bearer — trừ Gemini (key đã trong URL)
                if (provider.KeyInHeader && !string.IsNullOrEmpty(apiKey))
                    req.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                using (var resp = await _http.SendAsync(req, ct))
                {
                    var raw = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                        throw new HttpRequestException(
                            "Lỗi " + (int)resp.StatusCode + ": " + raw);

                    return ParseModels(provider, raw);
                }
            }
        }

        private static string[] ParseModels(AiProviderInfo provider, string raw)
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                using (var doc = JsonDocument.Parse(raw))
                {
                    var root = doc.RootElement;

                    if (provider.Key == "gemini")
                    {
                        // {"models": [{"name": "models/gemini-2.0-flash", "supportedGenerationMethods": [...]}]}
                        if (!root.TryGetProperty("models", out var models)) return provider.Models;

                        foreach (var m in models.EnumerateArray())
                        {
                            // Chỉ lấy model hỗ trợ generateContent (bỏ embedding, vision-only…)
                            bool canGenerate = false;
                            if (m.TryGetProperty("supportedGenerationMethods", out var methods))
                            {
                                foreach (var method in methods.EnumerateArray())
                                {
                                    if (method.GetString() == "generateContent")
                                    {
                                        canGenerate = true;
                                        break;
                                    }
                                }
                            }

                            if (!canGenerate) continue;

                            if (!m.TryGetProperty("name", out var nameProp)) continue;
                            string id = nameProp.GetString() ?? "";

                            // Bỏ prefix "models/" — chỉ giữ model gemini-*
                            if (id.StartsWith("models/")) id = id.Substring(7);
                            if (id.StartsWith("gemini")) list.Add(id);
                        }
                    }
                    else if (provider.Key == "openrouter")
                    {
                        // {"data": [{"id": "...", "pricing": {"prompt": "0"}}]}
                        // Chỉ hiện model miễn phí (pricing.prompt == 0)
                        if (!root.TryGetProperty("data", out var data)) return provider.Models;

                        foreach (var m in data.EnumerateArray())
                        {
                            if (!m.TryGetProperty("id", out var idProp)) continue;
                            string modelId = idProp.GetString() ?? "";
                            if (string.IsNullOrEmpty(modelId)) continue;

                            bool isFree = false;
                            if (m.TryGetProperty("pricing", out var pricing)
                                && pricing.TryGetProperty("prompt", out var prompt))
                            {
                                if (prompt.ValueKind == JsonValueKind.String)
                                    isFree = prompt.GetString() == "0";
                                else if (prompt.ValueKind == JsonValueKind.Number)
                                    isFree = prompt.GetDouble() == 0.0;
                            }

                            if (isFree) list.Add(modelId);
                        }
                    }
                    else if (provider.Key == "mistral")
                    {
                        // Lọc bỏ embedding model (id chứa "embed")
                        if (!root.TryGetProperty("data", out var data)) return provider.Models;

                        foreach (var m in data.EnumerateArray())
                        {
                            if (!m.TryGetProperty("id", out var idProp)) continue;
                            string id = idProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(id) && !id.Contains("embed"))
                                list.Add(id);
                        }
                    }
                    else
                    {
                        // Format OpenAI-compatible chuẩn: {"data": [{"id": "..."}]}
                        if (!root.TryGetProperty("data", out var data)) return provider.Models;

                        foreach (var m in data.EnumerateArray())
                        {
                            if (m.TryGetProperty("id", out var idProp))
                            {
                                string id = idProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(id)) list.Add(id);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Parse thất bại — dùng danh sách dự phòng
                return provider.Models;
            }

            return list.Count > 0 ? list.ToArray() : provider.Models;
        }
    }
}
