using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace vietnamgiapha.AI
{
    /// <summary>Quản lý process llama-server (sidecar) — user không cần chạy tay.</summary>
    public sealed class LocalLlamaHost
    {
        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        private static readonly Lazy<LocalLlamaHost> _instance =
            new Lazy<LocalLlamaHost>(() => new LocalLlamaHost());

        public static LocalLlamaHost Instance => _instance.Value;

        private readonly object _gate = new object();
        private Process _process;
        private int _activePort;
        private string _loadedModelPath;

        public int ActivePort => _activePort;
        public bool IsRunning
        {
            get
            {
                lock (_gate)
                {
                    return _process != null && !_process.HasExited;
                }
            }
        }

        /// <summary>Khởi động sidecar nếu chưa chạy hoặc đổi model/port.</summary>
        public async Task EnsureRunningAsync(
            AiSettings settings,
            IProgress<string> progress,
            CancellationToken ct)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            LocalLlamaPaths.EnsureAiFoldersExist();

            string serverExe = LocalLlamaPaths.GetServerExePath();
            if (!File.Exists(serverExe))
            {
                throw new FileNotFoundException(
                    "Không tìm thấy llama-server.exe trong thư mục ai\\ cạnh chương trình.\n"
                    + "Xem hướng dẫn trong ai\\README.txt (tải bản Windows từ llama.cpp).",
                    serverExe);
            }

            string modelPath = LocalLlamaPaths.ResolveModelPath(settings.LocalLlamaModelPath);
            if (string.IsNullOrEmpty(modelPath))
            {
                throw new FileNotFoundException(
                    "Chưa có file model GGUF.\n"
                    + "Đặt file .gguf vào thư mục ai\\models\\ hoặc chọn file trong Cài đặt AI → Qwen trên máy.");
            }

            var manifest = LocalLlamaPaths.LoadManifest();
            int port = settings.LocalLlamaPort > 0
                ? settings.LocalLlamaPort
                : (manifest.Port > 0 ? manifest.Port : LocalLlamaPaths.DefaultPort);

            lock (_gate)
            {
                if (_process != null && !_process.HasExited
                    && _activePort == port
                    && string.Equals(_loadedModelPath, modelPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            StopInternal();

            progress?.Report("Đang khởi động AI Qwen trên máy…");

            string args = BuildServerArguments(modelPath, port);
            var psi = new ProcessStartInfo
            {
                FileName = serverExe,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(serverExe) ?? LocalLlamaPaths.AiRootDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = false
            };

            Process proc;
            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Không chạy được llama-server: " + ex.Message, ex);
            }

            if (proc == null)
            {
                throw new InvalidOperationException("Không khởi động được llama-server.");
            }

            lock (_gate)
            {
                _process = proc;
                _activePort = port;
                _loadedModelPath = modelPath;
            }

            progress?.Report("Đang nạp model (lần đầu có thể mất 1–2 phút)…");
            await WaitUntilReadyAsync(this, port, progress, ct).ConfigureAwait(false);
        }

        public void Stop()
        {
            StopInternal();
        }

        private void StopInternal()
        {
            lock (_gate)
            {
                if (_process == null)
                {
                    return;
                }

                try
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill();
                        _process.WaitForExit(5000);
                    }
                }
                catch
                {
                    // Bỏ qua khi process đã thoát
                }
                finally
                {
                    _process.Dispose();
                    _process = null;
                    _loadedModelPath = null;
                    _activePort = 0;
                }
            }
        }

        private static string BuildServerArguments(string modelPath, int port)
        {
            // Host chỉ localhost — không mở ra LAN
            return "--model \"" + modelPath + "\""
                + " --host 127.0.0.1"
                + " --port " + port
                + " -c 8192"
                + " -ngl 0";
        }

        private static async Task WaitUntilReadyAsync(
            LocalLlamaHost host,
            int port,
            IProgress<string> progress,
            CancellationToken ct)
        {
            string baseUrl = "http://127.0.0.1:" + port;
            var deadline = DateTime.UtcNow.AddMinutes(3);

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                if (await ProbeEndpointAsync(baseUrl + "/health").ConfigureAwait(false)
                    || await ProbeEndpointAsync(baseUrl + "/v1/models").ConfigureAwait(false))
                {
                    progress?.Report("AI local sẵn sàng.");
                    return;
                }

                lock (host._gate)
                {
                    if (host._process != null && host._process.HasExited)
                    {
                        throw new InvalidOperationException(
                            "llama-server thoát sớm. Kiểm tra model GGUF và bản llama-server (xem ai\\README.txt).");
                    }
                }

                await Task.Delay(500, ct).ConfigureAwait(false);
            }

            throw new TimeoutException(
                "AI local không phản hồi sau 3 phút. Model quá lớn hoặc thiếu RAM — thử model nhỏ hơn (Qwen3-1.7B/4B Q4).");
        }

        private static async Task<bool> ProbeEndpointAsync(string url)
        {
            try
            {
                using (var resp = await _http.GetAsync(url).ConfigureAwait(false))
                {
                    return resp.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
