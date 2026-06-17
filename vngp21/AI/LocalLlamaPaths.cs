using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace vietnamgiapha.AI
{
    /// <summary>Đường dẫn thư mục ai/ cạnh exe và manifest sidecar.</summary>
    public static class LocalLlamaPaths
    {
        public const int DefaultPort = 8765;

        public static string AiRootDirectory =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai");

        public static string ModelsDirectory =>
            Path.Combine(AiRootDirectory, "models");

        public static string ManifestPath =>
            Path.Combine(AiRootDirectory, "manifest.json");

        public static string ReadmePath =>
            Path.Combine(AiRootDirectory, "README.txt");

        /// <summary>Đọc manifest.json — thiếu file thì dùng mặc định.</summary>
        public static LocalLlamaManifest LoadManifest()
        {
            try
            {
                if (!File.Exists(ManifestPath))
                {
                    return LocalLlamaManifest.CreateDefault();
                }

                string json = File.ReadAllText(ManifestPath);
                var m = JsonSerializer.Deserialize<LocalLlamaManifest>(json);
                return m ?? LocalLlamaManifest.CreateDefault();
            }
            catch
            {
                return LocalLlamaManifest.CreateDefault();
            }
        }

        public static string GetServerExePath()
        {
            var manifest = LoadManifest();
            string name = string.IsNullOrWhiteSpace(manifest.ServerExe)
                ? "llama-server.exe"
                : manifest.ServerExe.Trim();
            return Path.Combine(AiRootDirectory, name);
        }

        /// <summary>Ưu tiên đường dẫn user chọn, rồi manifest, rồi *.gguf đầu tiên trong ai/models.</summary>
        public static string ResolveModelPath(string userModelPath)
        {
            if (!string.IsNullOrWhiteSpace(userModelPath) && File.Exists(userModelPath))
            {
                return Path.GetFullPath(userModelPath);
            }

            var manifest = LoadManifest();
            if (!string.IsNullOrWhiteSpace(manifest.DefaultModelFile))
            {
                string fromManifest = Path.Combine(AiRootDirectory, manifest.DefaultModelFile.Replace('/', '\\'));
                if (File.Exists(fromManifest))
                {
                    return Path.GetFullPath(fromManifest);
                }
            }

            if (!Directory.Exists(ModelsDirectory))
            {
                return null;
            }

            string first = Directory.GetFiles(ModelsDirectory, "*.gguf", SearchOption.TopDirectoryOnly)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return first != null ? Path.GetFullPath(first) : null;
        }

        public static void EnsureAiFoldersExist()
        {
            if (!Directory.Exists(AiRootDirectory))
            {
                Directory.CreateDirectory(AiRootDirectory);
            }

            if (!Directory.Exists(ModelsDirectory))
            {
                Directory.CreateDirectory(ModelsDirectory);
            }
        }
    }

    public sealed class LocalLlamaManifest
    {
        public string ServerExe { get; set; } = "llama-server.exe";
        public string DefaultModelFile { get; set; } = "models/qwen3-4b-instruct-q4_k_m.gguf";
        public int Port { get; set; } = LocalLlamaPaths.DefaultPort;

        public static LocalLlamaManifest CreateDefault()
        {
            return new LocalLlamaManifest();
        }
    }
}
