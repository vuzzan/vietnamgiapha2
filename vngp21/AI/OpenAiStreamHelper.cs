using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Đọc phản hồi SSE OpenAI-compatible (stream: true) — dùng chung API cloud và llama-server.</summary>
    internal static class OpenAiStreamHelper
    {
        public static async Task<string> ReadChatCompletionStreamAsync(
            HttpResponseMessage response,
            Action<string> onChunk,
            CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException(
                    "API lỗi " + (int)response.StatusCode + ": " + err);
            }

            var full = new StringBuilder(512);
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    ct.ThrowIfCancellationRequested();
                    string line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    if (!line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string data = line.Substring(5).Trim();
                    if (data == "[DONE]")
                    {
                        break;
                    }

                    string piece = TryExtractDeltaContent(data);
                    if (string.IsNullOrEmpty(piece))
                    {
                        continue;
                    }

                    full.Append(piece);
                    onChunk?.Invoke(piece);
                }
            }

            return full.ToString();
        }

        private static string TryExtractDeltaContent(string jsonLine)
        {
            try
            {
                using (var doc = JsonDocument.Parse(jsonLine))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                    {
                        return null;
                    }

                    var first = choices[0];
                    if (first.TryGetProperty("delta", out var delta)
                        && delta.TryGetProperty("content", out var content)
                        && content.ValueKind == JsonValueKind.String)
                    {
                        return content.GetString();
                    }

                    // Một số API gửi chunk hoàn chỉnh trong message
                    if (first.TryGetProperty("message", out var msg)
                        && msg.TryGetProperty("content", out var msgContent)
                        && msgContent.ValueKind == JsonValueKind.String)
                    {
                        return msgContent.GetString();
                    }
                }
            }
            catch
            {
                // Bỏ qua dòng JSON lỗi trong luồng SSE
            }

            return null;
        }
    }
}
