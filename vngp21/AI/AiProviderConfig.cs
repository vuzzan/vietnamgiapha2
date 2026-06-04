using System.Collections.Generic;

namespace vietnamgiapha.AI
{
    /// <summary>Thông tin một nhà cung cấp AI.</summary>
    public sealed class AiProviderInfo
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }
        public string GetKeyUrl { get; set; }
        public string GetKeyGuide { get; set; }
        public string[] Models { get; set; }
        /// <summary>Endpoint gốc — {model} sẽ được replace khi gọi.</summary>
        public string EndpointTemplate { get; set; }
        /// <summary>True = key truyền qua header Authorization Bearer; False = key truyền qua query string.</summary>
        public bool KeyInHeader { get; set; }
        /// <summary>
        /// Endpoint lấy danh sách model động từ API.
        /// Dùng {key} placeholder nếu provider cần key trong URL (Gemini).
        /// Null = không hỗ trợ fetch model động.
        /// </summary>
        public string ModelsEndpoint { get; set; }
    }

    /// <summary>Danh sách provider AI hỗ trợ.</summary>
    public static class AiProviderConfig
    {
        public static readonly IReadOnlyList<AiProviderInfo> Providers = new List<AiProviderInfo>
        {
            new AiProviderInfo
            {
                Key = "gemini",
                DisplayName = "Google Gemini (Miễn phí)",
                GetKeyUrl = "https://aistudio.google.com/apikey",
                GetKeyGuide = "1. Truy cập aistudio.google.com\n2. Đăng nhập tài khoản Google\n3. Nhấn \"Get API key\" → \"Create API key\"\n4. Copy và dán vào ô bên dưới",
                Models = new[] { "gemini-2.0-flash", "gemini-1.5-flash", "gemini-1.5-pro" },
                EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}",
                // Key trong URL query string — ModelsEndpoint dùng {key} placeholder tương tự
                KeyInHeader = false,
                ModelsEndpoint = "https://generativelanguage.googleapis.com/v1beta/models?key={key}"
            },
            new AiProviderInfo
            {
                Key = "groq",
                DisplayName = "Groq (Miễn phí)",
                GetKeyUrl = "https://console.groq.com/keys",
                GetKeyGuide = "1. Truy cập console.groq.com\n2. Đăng ký / đăng nhập\n3. Vào mục \"API Keys\" → \"Create API Key\"\n4. Copy và dán vào ô bên dưới",
                Models = new[] { "llama-3.3-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768" },
                EndpointTemplate = "https://api.groq.com/openai/v1/chat/completions",
                KeyInHeader = true,
                ModelsEndpoint = "https://api.groq.com/openai/v1/models"
            },
            new AiProviderInfo
            {
                Key = "openrouter",
                DisplayName = "OpenRouter (Miễn phí — nhiều model)",
                GetKeyUrl = "https://openrouter.ai/keys",
                GetKeyGuide = "1. Truy cập openrouter.ai và đăng ký\n2. Vào mục \"Keys\" → \"Create Key\"\n3. Copy và dán vào ô bên dưới\n\nOpenRouter cho phép dùng hàng chục model miễn phí\n(các model có hậu tố ':free' không tốn credit)",
                // Danh sách dự phòng — thực tế sẽ được fetch động từ API (lọc model miễn phí)
                Models = new[]
                {
                    "meta-llama/llama-3.3-70b-instruct:free",
                    "deepseek/deepseek-v3:free",
                    "deepseek/deepseek-r1:free",
                    "google/gemma-4-31b-it:free",
                    "qwen/qwen3-coder:free",
                    "meta-llama/llama-3.2-3b-instruct:free",
                    "nousresearch/hermes-3-llama-3.1-405b:free"
                },
                EndpointTemplate = "https://openrouter.ai/api/v1/chat/completions",
                // Endpoint models là public — không cần auth, nhưng truyền key nếu có
                KeyInHeader = true,
                ModelsEndpoint = "https://openrouter.ai/api/v1/models"
            },
            new AiProviderInfo
            {
                Key = "deepseek",
                DisplayName = "DeepSeek (Gần như miễn phí)",
                GetKeyUrl = "https://platform.deepseek.com/api_keys",
                GetKeyGuide = "1. Truy cập platform.deepseek.com và đăng ký\n2. Vào \"API Keys\" → \"Create new secret key\"\n3. Copy và dán vào ô bên dưới\n\nDeepSeek tặng $5 credit khi đăng ký.\nGiá rất rẻ: ~$0.14/triệu token (thực tế gần miễn phí).",
                Models = new[] { "deepseek-chat", "deepseek-reasoner" },
                EndpointTemplate = "https://api.deepseek.com/v1/chat/completions",
                KeyInHeader = true,
                ModelsEndpoint = "https://api.deepseek.com/v1/models"
            },
            new AiProviderInfo
            {
                Key = "cerebras",
                DisplayName = "Cerebras (Miễn phí — siêu nhanh)",
                GetKeyUrl = "https://cloud.cerebras.ai/",
                GetKeyGuide = "1. Truy cập cloud.cerebras.ai và đăng ký\n2. Vào \"API Keys\" → tạo key mới\n3. Copy và dán vào ô bên dưới\n\nCerebras chạy AI trên chip chuyên dụng — tốc độ ~2000 tokens/giây.\nModel gpt-oss-120b là model OpenAI open-source, chất lượng cao.",
                Models = new[] { "gpt-oss-120b", "zai-glm-4.7" },
                EndpointTemplate = "https://api.cerebras.ai/v1/chat/completions",
                KeyInHeader = true,
                ModelsEndpoint = "https://api.cerebras.ai/v1/models"
            },
            new AiProviderInfo
            {
                Key = "mistral",
                DisplayName = "Mistral AI (Free tier)",
                GetKeyUrl = "https://console.mistral.ai/api-keys/",
                GetKeyGuide = "1. Truy cập console.mistral.ai và đăng ký\n2. Vào \"API Keys\" → \"Create new key\"\n3. Copy và dán vào ô bên dưới\n\nMistral cung cấp free tier cho dùng cá nhân.",
                Models = new[] { "mistral-small-latest", "open-mistral-7b", "open-mixtral-8x7b" },
                EndpointTemplate = "https://api.mistral.ai/v1/chat/completions",
                KeyInHeader = true,
                ModelsEndpoint = "https://api.mistral.ai/v1/models"
            }
        };

        public static AiProviderInfo Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            foreach (var p in Providers)
            {
                if (p.Key == key)
                {
                    return p;
                }
            }

            return null;
        }
    }
}
