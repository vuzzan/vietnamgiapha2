using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;

namespace vietnamgiapha.GiaPhaRender
{
    /// <summary>Một file SVG trong thư mục ZoneSvg (title_* / family_*).</summary>
    public sealed class PhaDoZoneSvgFileEntry
    {
        public string Id { get; set; }
        public string Display { get; set; }
        public string FilePath { get; set; }
    }

    /// <summary>Kết quả quét thư mục SVG trang trí ô.</summary>
    public sealed class PhaDoZoneSvgFolderLoadResult
    {
        public string FolderPath { get; set; }
        public List<PhaDoZoneSvgFileEntry> TitleEntries { get; } = new List<PhaDoZoneSvgFileEntry>();
        public List<PhaDoZoneSvgFileEntry> FamilyEntries { get; } = new List<PhaDoZoneSvgFileEntry>();
        public Dictionary<string, PhaDoSvgShape> ShapesById { get; }
            = new Dictionary<string, PhaDoSvgShape>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Đọc SVG từ thư mục — title_*.svg (combo trái), family_*.svg (combo phải).</summary>
    public static class PhaDoZoneSvgFolderLoader
    {
        public const string DefaultFolderName = "svgZone";
        public const string TitlePrefix = "title_";
        public const string FamilyPrefix = "family_";

        private static readonly string[] FolderNameCandidates =
        {
            "svgZone",
            "ZoneSvg",
            "SvgZone",
            "zone_svg"
        };

        /// <summary>Tìm thư mục SVG: app.config → cạnh .exe → lên cấp (project khi F5).</summary>
        public static string ResolveFolderPath()
        {
            try
            {
                string cfg = ConfigurationManager.AppSettings["phaDoZoneSvgFolder"];
                if (!string.IsNullOrWhiteSpace(cfg))
                {
                    string full = Path.GetFullPath(cfg.Trim());
                    if (Directory.Exists(full))
                    {
                        return full;
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu không đọc được config
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            string found = FindFolderUnder(baseDir);
            if (!string.IsNullOrEmpty(found))
            {
                return found;
            }

            string probe = baseDir;
            for (int depth = 0; depth < 6; depth++)
            {
                string parent = Path.GetDirectoryName(probe);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, probe, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                probe = parent;
                found = FindFolderUnder(probe);
                if (!string.IsNullOrEmpty(found))
                {
                    return found;
                }
            }

            return Path.Combine(baseDir, DefaultFolderName);
        }

        private static string FindFolderUnder(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return null;
            }

            foreach (string name in FolderNameCandidates)
            {
                string path = Path.Combine(root, name);
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }

        public static string GetDefaultFolderPath()
        {
            return ResolveFolderPath();
        }

        public static PhaDoZoneSvgFolderLoadResult Load(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                folderPath = ResolveFolderPath();
            }

            var result = new PhaDoZoneSvgFolderLoadResult
            {
                FolderPath = folderPath ?? string.Empty
            };

            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return result;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(folderPath, "*.svg", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return result;
            }

            Array.Sort(files, StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                string id = Path.GetFileNameWithoutExtension(name);
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                bool isTitle = id.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase);
                bool isFamily = id.StartsWith(FamilyPrefix, StringComparison.OrdinalIgnoreCase);
                if (!isTitle && !isFamily)
                {
                    continue;
                }

                if (!TryLoadShapeFromFile(file, id, out PhaDoSvgShape shape))
                {
                    continue;
                }

                result.ShapesById[id] = shape;
                var entry = new PhaDoZoneSvgFileEntry
                {
                    Id = id,
                    Display = id,
                    FilePath = file
                };

                if (isTitle)
                {
                    result.TitleEntries.Add(entry);
                }
                else
                {
                    result.FamilyEntries.Add(entry);
                }
            }

            return result;
        }

        /// <summary>Ghi markup SVG ra file {svgId}.svg trong thư mục zone (tạo thư mục nếu chưa có).</summary>
        public static string SaveSvgToFolder(string folderPath, string svgId, string sanitizedMarkup)
        {
            if (string.IsNullOrWhiteSpace(svgId))
            {
                throw new ArgumentException("Thiếu tên file (id SVG).", nameof(svgId));
            }

            if (string.IsNullOrWhiteSpace(sanitizedMarkup))
            {
                throw new ArgumentException("Nội dung SVG trống.", nameof(sanitizedMarkup));
            }

            string id = svgId.Trim();
            bool isTitle = id.StartsWith(TitlePrefix, StringComparison.OrdinalIgnoreCase);
            bool isFamily = id.StartsWith(FamilyPrefix, StringComparison.OrdinalIgnoreCase);
            if (!isTitle && !isFamily)
            {
                throw new ArgumentException(
                    "Tên file phải bắt đầu bằng " + TitlePrefix + " hoặc " + FamilyPrefix + ".",
                    nameof(svgId));
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                folderPath = ResolveFolderPath();
            }

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string path = Path.Combine(folderPath, id + ".svg");
            File.WriteAllText(path, sanitizedMarkup);
            return path;
        }

        private static bool TryLoadShapeFromFile(string filePath, string svgId, out PhaDoSvgShape shape)
        {
            shape = null;
            try
            {
                string raw = File.ReadAllText(filePath);
                var sanitized = PhaDoBoxSvgSanitizer.Sanitize(raw);
                if (!sanitized.Success || string.IsNullOrWhiteSpace(sanitized.SanitizedSvgMarkup))
                {
                    return false;
                }

                shape = PhaDoSvgShape.FromMarkup(
                    svgId,
                    sanitized.SanitizedSvgMarkup,
                    sanitized.ViewBoxWidth,
                    sanitized.ViewBoxHeight);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
