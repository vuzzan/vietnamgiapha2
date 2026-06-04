using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace vietnamgiapha.AI
{
    /// <summary>Serialize dữ liệu cây gia phả thành text ngắn gọn để đưa vào context AI.</summary>
    public static class FamilyContextSerializer
    {
        // ── API công khai ────────────────────────────────────────────────────

        /// <summary>
        /// Tạo context tổng quan file: tóm tắt số đời, tổng GD, tên gốc.
        /// Dùng cho system prompt khi chưa chọn gia đình cụ thể.
        /// </summary>
        public static string SerializeTreeSummary(FamilyViewModel root)
        {
            if (root?.familyInfo == null)
            {
                return "(Chưa có dữ liệu gia phả.)";
            }

            int total = CountFamilies(root);
            int maxLevel = GetMaxLevel(root);

            var sb = new StringBuilder();
            sb.AppendLine("=== TÓM TẮT GIA PHẢ ===");
            sb.AppendLine("Tổ tiên gốc: " + GetPersonsLine(root.familyInfo));
            sb.AppendLine("Tổng số gia đình: " + total);
            sb.AppendLine("Số đời ghi nhận: " + maxLevel);
            sb.AppendLine();

            // Liệt kê con trực tiếp của gốc
            if (root.Children != null && root.Children.Count > 0)
            {
                sb.AppendLine("Con cái đời 1 (từ gốc):");
                foreach (var child in root.Children.Take(10))
                {
                    if (child?.familyInfo == null)
                    {
                        continue;
                    }

                    sb.AppendLine("  - " + GetPersonsLine(child.familyInfo)
                        + " | " + CountFamilies(child) + " GD");
                }

                if (root.Children.Count > 10)
                {
                    sb.AppendLine("  ... (còn " + (root.Children.Count - 10) + " người con nữa)");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Serialize chi tiết 1 gia đình + cha mẹ + con cái — dùng khi người dùng đang chọn 1 gia đình.
        /// </summary>
        public static string SerializeFamilyDetail(FamilyViewModel family, FamilyViewModel fileRoot)
        {
            if (family?.familyInfo == null)
            {
                return "(Không có thông tin gia đình.)";
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== GIA ĐÌNH ĐANG XEM ===");
            AppendFamilyBlock(sb, family.familyInfo, "");

            // Cha (gia đình cha)
            int upId = family.familyInfo.FamilyUp;
            if (upId > 0 && fileRoot != null)
            {
                var parentVm = FindById(fileRoot, upId);
                if (parentVm?.familyInfo != null)
                {
                    sb.AppendLine();
                    sb.AppendLine("=== GIA ĐÌNH CHA ===");
                    AppendFamilyBlock(sb, parentVm.familyInfo, "");
                }
            }

            // Con cái
            if (family.Children != null && family.Children.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("=== CÁC GIA ĐÌNH CON (" + family.Children.Count + ") ===");
                foreach (var child in family.Children)
                {
                    if (child?.familyInfo == null)
                    {
                        continue;
                    }

                    AppendFamilyBlock(sb, child.familyInfo, "  ");
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Tìm kiếm và serialize các gia đình khớp tên — dùng khi AI cần tìm người.
        /// </summary>
        public static string SerializeSearchResults(FamilyViewModel fileRoot, string keyword, int maxResults = 8)
        {
            if (fileRoot == null || string.IsNullOrWhiteSpace(keyword))
            {
                return "(Không có từ khóa tìm kiếm.)";
            }

            var results = SearchByName(fileRoot, keyword, maxResults);
            if (results.Count == 0)
            {
                return "Không tìm thấy gia đình nào khớp với \"" + keyword + "\".";
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== KẾT QUẢ TÌM KIẾM: \"" + keyword + "\" (" + results.Count + " kết quả) ===");
            foreach (var vm in results)
            {
                if (vm?.familyInfo == null)
                {
                    continue;
                }

                AppendFamilyBlock(sb, vm.familyInfo, "");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private static void AppendFamilyBlock(StringBuilder sb, FamilyInfo fi, string indent)
        {
            sb.AppendLine(indent + "GĐ #" + fi.FamilyId
                + " | Đời " + fi.FamilyLevel
                + (fi.FamilyUp > 0 ? " | Cha: GĐ #" + fi.FamilyUp : " | (Gốc)"));

            foreach (var p in fi.ListPerson)
            {
                if (p == null)
                {
                    continue;
                }

                string role = p.IsMainPerson == 1 ? "Chồng/Cha" : "Vợ/Mẹ";
                string name = p.MANS_NAME_HUY ?? "";
                if (!string.IsNullOrWhiteSpace(p.MANS_NAME_TU))
                {
                    name += " (tự: " + p.MANS_NAME_TU + ")";
                }

                string dob = !string.IsNullOrWhiteSpace(p.MANS_DOB) ? " sinh " + p.MANS_DOB : "";
                string dod = !string.IsNullOrWhiteSpace(p.MANS_DOD) ? " mất " + p.MANS_DOD : "";
                string gender = p.MANS_GENDER ?? "";
                string detail = !string.IsNullOrWhiteSpace(p.MANS_DETAIL)
                    ? " | Ghi chú: " + TruncateDetail(p.MANS_DETAIL, 120)
                    : "";

                sb.AppendLine(indent + "  [" + role + "] " + name + " | " + gender + dob + dod + detail);
            }

            // Tóm tắt số con
            if (fi.FamilyChildren != null && fi.FamilyChildren.Count > 0)
            {
                var childNames = fi.FamilyChildren
                    .Take(6)
                    .Select(c => GetPersonsLine(c))
                    .ToList();
                sb.AppendLine(indent + "  Con (" + fi.FamilyChildren.Count + "): "
                    + string.Join("; ", childNames)
                    + (fi.FamilyChildren.Count > 6 ? " ..." : ""));
            }
        }

        private static string GetPersonsLine(FamilyInfo fi)
        {
            if (fi == null || fi.ListPerson == null || fi.ListPerson.Count == 0)
            {
                return "(không có tên)";
            }

            var parts = fi.ListPerson
                .OrderByDescending(p => p.IsMainPerson)
                .Take(2)
                .Select(p => p.MANS_NAME_HUY ?? "")
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            return parts.Count > 0 ? string.Join(" + ", parts) : "(không có tên)";
        }

        private static string TruncateDetail(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            // Bỏ tag HTML nếu có
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ").Trim();
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }

        private static int CountFamilies(FamilyViewModel vm)
        {
            if (vm == null)
            {
                return 0;
            }

            int count = 1;
            if (vm.Children != null)
            {
                foreach (var c in vm.Children)
                {
                    count += CountFamilies(c);
                }
            }

            return count;
        }

        private static int GetMaxLevel(FamilyViewModel vm)
        {
            if (vm == null)
            {
                return 0;
            }

            int max = vm.familyInfo?.FamilyLevel ?? 0;
            if (vm.Children != null)
            {
                foreach (var c in vm.Children)
                {
                    max = Math.Max(max, GetMaxLevel(c));
                }
            }

            return max;
        }

        private static FamilyViewModel FindById(FamilyViewModel root, int id)
        {
            if (root?.familyInfo?.FamilyId == id)
            {
                return root;
            }

            if (root?.Children == null)
            {
                return null;
            }

            foreach (var child in root.Children)
            {
                var found = FindById(child, id);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static List<FamilyViewModel> SearchByName(FamilyViewModel root, string keyword, int maxResults)
        {
            var results = new List<FamilyViewModel>();
            string kw = keyword.ToLowerInvariant();
            SearchByNameRecursive(root, kw, results, maxResults);
            return results;
        }

        private static void SearchByNameRecursive(FamilyViewModel vm, string kw,
            List<FamilyViewModel> results, int maxResults)
        {
            if (vm?.familyInfo == null || results.Count >= maxResults)
            {
                return;
            }

            bool match = vm.familyInfo.ListPerson != null
                && vm.familyInfo.ListPerson.Any(p =>
                    (!string.IsNullOrEmpty(p.MANS_NAME_HUY) && p.MANS_NAME_HUY.ToLowerInvariant().Contains(kw))
                    || (!string.IsNullOrEmpty(p.MANS_NAME_TU) && p.MANS_NAME_TU.ToLowerInvariant().Contains(kw))
                    || (!string.IsNullOrEmpty(p.MANS_NAME_THUONG) && p.MANS_NAME_THUONG.ToLowerInvariant().Contains(kw)));

            if (match)
            {
                results.Add(vm);
            }

            if (vm.Children != null)
            {
                foreach (var child in vm.Children)
                {
                    if (results.Count >= maxResults)
                    {
                        break;
                    }

                    SearchByNameRecursive(child, kw, results, maxResults);
                }
            }
        }
    }
}
