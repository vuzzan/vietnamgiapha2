using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Đọc stream SSE của Gemini (streamGenerateContent).</summary>
    internal static class GeminiStreamHelper
    {
        public static async Task<string> ReadStreamAsync(
            HttpResponseMessage response,
            Action<string> onChunk,
            CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                string err = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new HttpRequestException(
                    "Gemini lỗi " + (int)response.StatusCode + ": " + err);
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
                    string piece = TryExtractGeminiText(data);
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

        private static string TryExtractGeminiText(string jsonLine)
        {
            try
            {
                using (var doc = JsonDocument.Parse(jsonLine))
                {
                    var root = doc.RootElement;
                    if (!root.TryGetProperty("candidates", out var candidates)
                        || candidates.GetArrayLength() == 0)
                    {
                        return null;
                    }

                    var first = candidates[0];
                    if (!first.TryGetProperty("content", out var content)
                        || !content.TryGetProperty("parts", out var parts)
                        || parts.GetArrayLength() == 0)
                    {
                        return null;
                    }

                    if (parts[0].TryGetProperty("text", out var text)
                        && text.ValueKind == JsonValueKind.String)
                    {
                        return text.GetString();
                    }
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
