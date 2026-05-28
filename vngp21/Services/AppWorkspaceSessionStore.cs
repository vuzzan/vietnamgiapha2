using System;
using System.Configuration;
using System.IO;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace vietnamgiapha
{
    public static class AppWorkspaceSessionStore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AppWorkspaceSessionStore));
        private const string SessionFileName = "workspace_session.json";

        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            NullValueHandling = NullValueHandling.Ignore
        };

        public static string GetSessionFilePath()
        {
            string folder = ConfigurationManager.AppSettings["defaultSaveFolder"] ?? "";
            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "VietNamGiaPha2");
            }

            return Path.Combine(folder, SessionFileName);
        }

        public static void Save(AppWorkspaceSession session)
        {
            if (session == null)
            {
                return;
            }

            try
            {
                string path = GetSessionFilePath();
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(session, JsonSettings));
            }
            catch (Exception ex)
            {
                Log.Warn("Không lưu được workspace session.", ex);
            }
        }

        public static AppWorkspaceSession Load()
        {
            try
            {
                string path = GetSessionFilePath();
                if (!File.Exists(path))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<AppWorkspaceSession>(File.ReadAllText(path), JsonSettings);
            }
            catch (Exception ex)
            {
                Log.Warn("Không đọc được workspace session.", ex);
                return null;
            }
        }
    }
}
