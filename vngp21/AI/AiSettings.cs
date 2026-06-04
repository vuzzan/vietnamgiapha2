using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace vietnamgiapha.AI
{
    /// <summary>Cấu hình AI của người dùng — API key được mã hóa DPAPI khi lưu.</summary>
    public sealed class AiSettings
    {
        public string Provider { get; set; } = "gemini";
        public string Model { get; set; } = "gemini-2.0-flash";
        public bool IsEnabled { get; set; } = false;

        /// <summary>
        /// True = dùng rule-based engine nội bộ (không cần API key).
        /// False = dùng AI API (cần API key).
        /// </summary>
        public bool UseLocalRuleEngine { get; set; } = false;

        // Dùng nội bộ — không serialize trực tiếp
        [System.Text.Json.Serialization.JsonIgnore]
        public string ApiKey { get; set; }

        // Trường này lưu key đã mã hóa Base64
        public string EncryptedApiKey { get; set; }

        private static string SettingsPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VietNamGiaPha",
                "ai-settings.json");

        private static string KeyFilePath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VietNamGiaPha",
                "ai.key");

        /// <summary>Lưu cài đặt xuống file — mã hóa API key bằng DPAPI.</summary>
        public void Save()
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Mã hóa key riêng vào file .key (DPAPI — chỉ giải mã được trên máy + user này)
            if (!string.IsNullOrEmpty(ApiKey))
            {
                byte[] encrypted = ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(ApiKey),
                    null,
                    DataProtectionScope.CurrentUser);
                File.WriteAllBytes(KeyFilePath, encrypted);
                EncryptedApiKey = "__dpapi__"; // đánh dấu đã có key riêng
            }
            else if (string.IsNullOrEmpty(ApiKey) && EncryptedApiKey != "__dpapi__")
            {
                // Xóa key nếu người dùng đã xóa
                EncryptedApiKey = null;
                if (File.Exists(KeyFilePath))
                {
                    File.Delete(KeyFilePath);
                }
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json, Encoding.UTF8);
        }

        /// <summary>Đọc cài đặt từ file — giải mã API key.</summary>
        public static AiSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return new AiSettings();
                }

                var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                var s = JsonSerializer.Deserialize<AiSettings>(json) ?? new AiSettings();

                // Giải mã key từ file .key
                if (s.EncryptedApiKey == "__dpapi__" && File.Exists(KeyFilePath))
                {
                    byte[] raw = File.ReadAllBytes(KeyFilePath);
                    s.ApiKey = Encoding.UTF8.GetString(
                        ProtectedData.Unprotect(raw, null, DataProtectionScope.CurrentUser));
                }

                return s;
            }
            catch
            {
                return new AiSettings();
            }
        }

        public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
    }
}
