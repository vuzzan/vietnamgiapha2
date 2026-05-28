using System;
using System.Configuration;
using System.IO;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    public static class GiaPhaLayoutSettingsStore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(GiaPhaLayoutSettingsStore));
        private const string SettingsFileName = "giapha_layout_settings.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        public static GiaPhaLayoutSettings Current { get; private set; }

        static GiaPhaLayoutSettingsStore()
        {
            Current = Load() ?? GiaPhaLayoutSettings.CreateDefault();
        }

        public static string GetSettingsFilePath()
        {
            string folder = ConfigurationManager.AppSettings["defaultSaveFolder"] ?? "";
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VietNamGiaPha2");
            }

            return Path.Combine(folder, SettingsFileName);
        }

        public static GiaPhaLayoutSettings Load()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (!File.Exists(path))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<GiaPhaLayoutSettings>(
                    File.ReadAllText(path),
                    JsonSettings);
            }
            catch (Exception ex)
            {
                Log.Warn("Không đọc được cài đặt layout.", ex);
                return null;
            }
        }

        public static void Save(GiaPhaLayoutSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                string path = GetSettingsFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(settings, JsonSettings));
                Current = settings;
            }
            catch (Exception ex)
            {
                Log.Warn("Không lưu được cài đặt layout.", ex);
                throw;
            }
        }
    }
}
