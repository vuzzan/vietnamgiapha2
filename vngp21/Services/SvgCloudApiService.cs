using log4net;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace vietnamgiapha
{
    /// <summary>Gọi API PHP kho SVG trên cloud.</summary>
    public static class SvgCloudApiService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SvgCloudApiService));
        public const int DuplicateApiCode = 2;

        private static string GetApiBaseUrl()
        {
            string url = ConfigurationManager.AppSettings["svgCloudApiUrl"] ?? "";
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "https://vietnamgiapha.com/export/svg_api.php";
            }

            return url.TrimEnd('?');
        }

        private static void ConfigureClient(HttpClient client)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls
                | SecurityProtocolType.Tls11
                | SecurityProtocolType.Tls12;
            ServicePointManager.ServerCertificateValidationCallback += (s, c, ch, e) => true;
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "VietNamGiaPha2-SvgManager/1.0");
        }

        private static async Task<T> GetDataAsync<T>(string query)
        {
            string url = GetApiBaseUrl() + (query.StartsWith("?") ? query : "?" + query);
            using (var client = new HttpClient())
            {
                ConfigureClient(client);
                Log.Info("SVG API GET: " + url);
                string body = await client.GetStringAsync(url).ConfigureAwait(false);
                return ParseSuccessData<T>(body);
            }
        }

        private static T ParseSuccessData<T>(string jsonBody)
        {
            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                throw new Exception("API trả về rỗng.");
            }

            using (var doc = JsonDocument.Parse(jsonBody))
            {
                int code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetInt32() : 1;
                string msg = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString() : "";
                ThrowIfApiError(doc.RootElement, code, msg);

                if (!doc.RootElement.TryGetProperty("data", out var data))
                {
                    return default;
                }

                if (data.ValueKind == JsonValueKind.Null)
                {
                    return default;
                }

                return JsonSerializer.Deserialize<T>(data.GetRawText(), JsonOptions);
            }
        }

        private static void ThrowIfApiError(JsonElement root, int code, string msg)
        {
            if (code == 0)
            {
                return;
            }

            string message = string.IsNullOrWhiteSpace(msg) ? "API lỗi code=" + code : msg;

            if (code == DuplicateApiCode)
            {
                SvgCloudItem existing = TryParseDuplicateExisting(root);
                throw new SvgCloudDuplicateException(message, existing);
            }

            throw new Exception(message);
        }

        private static SvgCloudItem TryParseDuplicateExisting(JsonElement root)
        {
            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<SvgCloudItem>(data.GetRawText(), JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        private static void ParsePostResponse(string jsonBody)
        {
            if (string.IsNullOrWhiteSpace(jsonBody))
            {
                throw new Exception("API trả về rỗng.");
            }

            using (var doc = JsonDocument.Parse(jsonBody))
            {
                int code = doc.RootElement.TryGetProperty("code", out var c) ? c.GetInt32() : 1;
                string msg = doc.RootElement.TryGetProperty("msg", out var m) ? m.GetString() : "";
                ThrowIfApiError(doc.RootElement, code, msg);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public static Task<List<SvgCloudItem>> ListAsync()
        {
            return GetDataAsync<List<SvgCloudItem>>("action=list");
        }

        public static Task<List<string>> ListCategoriesAsync()
        {
            return GetDataAsync<List<string>>("action=categories");
        }

        public static Task<SvgCloudItem> GetAsync(int id)
        {
            return GetDataAsync<SvgCloudItem>("action=get&id=" + id.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Tăng count_download khi user chuyển SVG từ cloud sang kho local.</summary>
        public static Task<SvgCloudItem> RecordDownloadAsync(int id)
        {
            return GetDataAsync<SvgCloudItem>(
                "action=record_download&id=" + id.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Kiểm tra cloud đã có SVG cùng nội dung — ném SvgCloudDuplicateException nếu trùng.</summary>
        public static async Task EnsureNotDuplicateAsync(string sanitizedSvgMarkup)
        {
            if (string.IsNullOrWhiteSpace(sanitizedSvgMarkup))
            {
                return;
            }

            string body = await PostFormAsync(
                "check_duplicate",
                form =>
                {
                    form.Add(new StringContent(sanitizedSvgMarkup, Encoding.UTF8), "svg_data");
                }).ConfigureAwait(false);

            ParsePostResponse(body);
        }

        public static async Task<SvgCloudItem> UploadAsync(string category, string name, string author, string svgData)
        {
            string body = await PostFormAsync(
                "upload",
                form =>
                {
                    form.Add(new StringContent(category ?? "Chung"), "svg_category");
                    form.Add(new StringContent(name ?? ""), "svg_name");
                    form.Add(new StringContent(author ?? ""), "svg_author");
                    form.Add(new StringContent(svgData ?? "", Encoding.UTF8), "svg_data");
                }).ConfigureAwait(false);

            var uploaded = ParseSuccessData<SvgCloudItem>(body);
            uploaded.SvgData = svgData;
            return uploaded;
        }

        private static async Task<string> PostFormAsync(string action, Action<MultipartFormDataContent> fillForm)
        {
            string url = GetApiBaseUrl();
            using (var client = new HttpClient())
            {
                ConfigureClient(client);
                var form = new MultipartFormDataContent();
                form.Add(new StringContent(action), "action");
                fillForm?.Invoke(form);

                Log.Info("SVG API POST " + action + ": " + url);
                var response = await client.PostAsync(url, form).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception(
                        "Không kết nối được máy chủ cloud (HTTP " + (int)response.StatusCode + ").\n"
                        + "Kiểm tra mạng hoặc địa chỉ svgCloudApiUrl trong App.config.");
                }

                return body;
            }
        }
    }
}
