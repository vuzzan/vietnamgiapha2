using System;
using System.IO;
using System.Text;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Ghi SVG ra file theo luồng — tránh StringBuilder.ToString() gây OutOfMemory.</summary>
    public sealed class SvgExportWriter : IDisposable
    {
        private readonly StreamWriter _writer;
        private bool _disposed;

        public SvgExportWriter(string filePath, int bufferSize = 65536)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _writer = new StreamWriter(filePath, false, Encoding.UTF8, bufferSize);
        }

        public void Write(string text)
        {
            _writer.Write(text);
        }

        public void WriteLine(string text = null)
        {
            if (text == null)
            {
                _writer.WriteLine();
            }
            else
            {
                _writer.WriteLine(text);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer.Flush();
            _writer.Dispose();
        }
    }
}
