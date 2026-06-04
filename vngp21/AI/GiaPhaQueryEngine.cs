using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace vietnamgiapha.AI
{
    /// <summary>Rule-based query engine — tra cứu gia phả không cần API key.</summary>
    public sealed class GiaPhaQueryEngine
    {
        private List<SearchEntry> _index = new List<SearchEntry>();

        public int IndexSize => _index.Count;

        /// <summary>True khi đã có dữ liệu để tra cứu.</summary>
        public bool IsReady => _index.Count > 0;

        // ── Build index ───────────────────────────────────────────────────────

        /// <summary>Xây dựng index phẳng từ cây gia phả — gọi lại mỗi khi mở file mới.</summary>
        public void BuildIndex(FamilyViewModel root)
        {
            _index.Clear();
            if (root != null)
                IndexRecursive(root, null);
        }

        private void IndexRecursive(FamilyViewModel vm, FamilyViewModel parent)
        {
            if (vm?.familyInfo == null)
            {
                return;
            }

            _index.Add(new SearchEntry { Family = vm, Parent = parent });

            if (vm.Children != null)
            {
                foreach (var child in vm.Children)
                {
                    IndexRecursive(child, vm);
                }
            }
        }

        // ── API chính ─────────────────────────────────────────────────────────

        /// <summary>
        /// Xử lý câu hỏi tự nhiên và trả về câu trả lời dạng text.
        /// currentFamily là gia đình đang chọn trên cây (có thể null).
        /// </summary>
        public string Query(string question, FamilyViewModel currentFamily = null)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return "Bạn muốn hỏi gì về gia phả?";
            }

            if (!IsReady)
            {
                return "⚠️ Chưa có dữ liệu gia phả. Hãy mở file gia phả trước.";
            }

            string q = RemoveDiacritics(question.Trim().ToLowerInvariant());

            // Chuẩn hóa "Ai là [vai trò] của [tên]?" → "[vai trò] cua [tên]?"
            // (ví dụ: "Ai là cha của X?" → gọi lại Query("cha cua X?") để tái dùng routing)
            string normalizedForm = TryNormalizeAiLaQuestion(question, q);
            if (normalizedForm != null)
                return Query(normalizedForm, currentFamily);

            // 1. Pattern cố định không cần tên người
            if (ContainsAny(q, "thuy to", "thuythu", "to tien", "thuy-to"))
                return QueryThuyTo();

            if (ContainsAny(q, "bao nhieu", "tong so", "so nguoi", "tong cong", "dem nguoi"))
                return QuerySoNguoi();

            // Lọc người chưa có con cháu
            if (ContainsAny(q, "chua co con", "khong co con", "vo hau", "tuyet tu", "khong con"))
                return QueryChuaCoCon();

            // Tìm theo năm sinh — "sinh năm 1920", "sinh 1920"
            var mNamSinh = Regex.Match(q, @"sinh\s*(?:nam)?\s*(\d{4})");
            if (mNamSinh.Success && int.TryParse(mNamSinh.Groups[1].Value, out int namSinh))
                return QueryTheoNam(namSinh, true);

            // Tìm theo năm mất — "mất năm 1980", "kỵ năm 1980", "chết 1980"
            var mNamMat = Regex.Match(q, @"(?:mat|chet|qua doi|tu tran|ky)\s*(?:nam)?\s*(\d{4})");
            if (mNamMat.Success && int.TryParse(mNamMat.Groups[1].Value, out int namMat))
                return QueryTheoNam(namMat, false);

            // 2. Pattern "đời N"
            var matchDoi = Regex.Match(q, @"doi\s*(\d+)");
            if (matchDoi.Success && int.TryParse(matchDoi.Groups[1].Value, out int level))
            {
                // Xử lý khi không có tên người sau "đời N" hoặc chỉ hỏi số lượng
                string afterDoi = q.Substring(matchDoi.Index + matchDoi.Length).Trim();
                if (string.IsNullOrWhiteSpace(afterDoi)
                    || afterDoi.StartsWith("co ai") || afterDoi.StartsWith("co gi")
                    || afterDoi.StartsWith("co bao nhieu") || afterDoi.StartsWith("bao nhieu")
                    || afterDoi.StartsWith("co may") || afterDoi.StartsWith("may nguoi"))
                {
                    return QueryDoiSo(level);
                }
            }

            // 3. Câu hỏi về "người này" / "gia đình đang chọn"
            if (ContainsAny(q, "nguoi nay", "gia dinh nay", "day", "nguoi dang chon", "hien tai"))
            {
                return QueryCurrentFamily(currentFamily, q);
            }

            // 4. Tìm quan hệ giữa 2 người: "X và Y", "X với Y", "X là gì của Y"
            string nameA, nameB;
            if (TryExtractTwoNames(question, out nameA, out nameB))
            {
                return QueryRelationship(nameA, nameB);
            }

            // 5. Các pattern cần tên người — trích tên, rồi tìm
            string name = ExtractName(question); // dùng bản gốc có dấu để tìm
            string nameNorm = RemoveDiacritics(name.ToLowerInvariant());

            if (string.IsNullOrWhiteSpace(nameNorm))
            {
                return "Xin hỏi cụ thể hơn, ví dụ:\n• \"Con của Nguyễn Văn Chính\"\n• \"Mộ của Trần Thị Thê ở đâu?\"\n• \"Đời 4 có ai?\"";
            }

            var entries = FindByName(nameNorm);

            if (entries.Count == 0)
            {
                return $"Không tìm thấy ai tên \"{name}\" trong gia phả.\n"
                    + "Hãy kiểm tra lại tên hoặc thử gõ một phần tên họ.";
            }

            // Lấy hàm xử lý theo loại câu hỏi
            Func<SearchEntry, string> detailFunc = GetDetailQueryFunc(q);

            if (entries.Count == 1)
            {
                // Duy nhất 1 kết quả → trả lời trực tiếp
                return detailFunc != null
                    ? detailFunc(entries[0])
                    : QueryThongTinDay(entries[0]);
            }

            // Nhiều người cùng tên
            if (detailFunc == null)
            {
                // Câu hỏi chung (tìm / thông tin) → liệt kê danh sách để người dùng chọn
                return FormatMultipleResults(name, entries);
            }

            // Câu hỏi cụ thể (con, cha, mộ…) → trả lời cho TẤT CẢ người cùng tên
            return QueryDetailForAll(name, entries, detailFunc);
        }

        // ── Routing helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Trả về hàm xử lý tương ứng với loại câu hỏi.
        /// Trả về null nếu là câu hỏi chung (tìm / thông tin tổng hợp).
        /// </summary>
        private Func<SearchEntry, string> GetDetailQueryFunc(string q)
        {
            if (ContainsAny(q, "con cua", "con cai", "con trai", "con gai", "co may con"))
                return QueryConCua;

            if (ContainsAny(q, "cha cua", "ba cua", "cha la ai", "ba la ai"))
                return QueryChaCua;

            if (ContainsAny(q, "me cua", "ma cua", "me la ai"))
                return QueryMeCua;

            if (ContainsAny(q, "vo cua", "vo la ai", "nguoi vo"))
                return e => QueryVoChong(e, "Vợ");

            if (ContainsAny(q, "chong cua", "chong la ai", "nguoi chong"))
                return e => QueryVoChong(e, "Chồng");

            if (ContainsAny(q, "anh em cua", "anh chi em cua", "anh cua", "em cua", "chi cua"))
                return QueryAnhEm;

            if (ContainsAny(q, "chau cua", "chau noi", "chau ngoai"))
                return QueryChauCua;

            // Ngày kỵ — thêm synonym "ngày mất", "ngày chết", "ngày qua đời"
            if (ContainsAny(q, "ngay ky", "ngay gio", "ky nhat", "gio",
                               "ngay mat", "ngay chet", "ngay qua doi"))
                return QueryNgayKy;

            if (ContainsAny(q, "mo ", "phan mo", "lang mo", "an tang", "chon cat"))
                return QueryMo;

            if (ContainsAny(q, "doi may", "the he thu", "thu may"))
                return QueryDoiMay;

            if (ContainsAny(q, "ghi chu", "chi tiet", "mo ta", "ghi nhan"))
                return QueryGhiChu;

            // Tổ tiên nhiều cấp — ông bà nội (2 cấp)
            if (ContainsAny(q, "ong noi", "ba noi"))
                return e => QueryToTienN(e, 2);

            // Tổ tiên nhiều cấp — ông bà cố/cụ (3 cấp)
            if (ContainsAny(q, "ong co", "ba co", "cu ong", "cu ba",
                               "ong cu", "ba cu", "ong to", "ba to"))
                return e => QueryToTienN(e, 3);

            // Tổ tiên 4 cấp — kỵ
            if (ContainsAny(q, "ky cua", "ky la ai"))
                return e => QueryToTienN(e, 4);

            // Hậu duệ đầy đủ theo cây đệ quy
            if (ContainsAny(q, "hau due", "tat ca con chau", "toan bo con chau", "con chau la ai"))
                return QueryHauDue;

            // Tên tự / thụy / thường gọi
            if (ContainsAny(q, "ten tu", "ten thuy", "ten thuong", "ten huy", "biet danh"))
                return QueryTenTu;

            return null; // câu hỏi chung
        }

        /// <summary>
        /// Khi có nhiều người cùng tên và câu hỏi cụ thể,
        /// trả lời cho từng người một, phân cách rõ ràng.
        /// </summary>
        private string QueryDetailForAll(string name, List<SearchEntry> entries,
            Func<SearchEntry, string> detailFunc)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 Có {entries.Count} người tên \"{name}\" — kết quả cho từng người:");

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                string doiLine = $"Đời {e.Family.familyInfo.FamilyLevel}";
                string chaLine = e.Parent != null
                    ? $"con của {GetMainName(e.Parent.familyInfo)}"
                    : "thủy tổ";

                sb.AppendLine();
                sb.AppendLine($"── [{i + 1}] {GetPersonsLine(e.Family.familyInfo)} ({doiLine}, {chaLine}) ──");
                sb.AppendLine(detailFunc(e));
            }

            return sb.ToString().Trim();
        }

        // ── Query handlers ────────────────────────────────────────────────────

        private string QueryThuyTo()
        {
            // Thủy tổ = gia đình không có cha (Parent == null) hoặc level thấp nhất
            var roots = _index.Where(e => e.Parent == null).ToList();
            if (roots.Count == 0)
            {
                roots = _index.OrderBy(e => e.Family.familyInfo.FamilyLevel).Take(1).ToList();
            }

            if (roots.Count == 0)
            {
                return "Chưa có dữ liệu gia phả.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("📜 Thủy tổ của dòng họ:");
            foreach (var r in roots)
            {
                sb.AppendLine(FormatFamilyShort(r.Family));
                // Thêm ghi chú nếu có
                foreach (var p in r.Family.familyInfo.ListPerson)
                {
                    if (!string.IsNullOrWhiteSpace(p.MANS_DETAIL))
                    {
                        string detail = StripHtml(p.MANS_DETAIL);
                        if (!string.IsNullOrWhiteSpace(detail))
                        {
                            sb.AppendLine($"   📝 {detail.Substring(0, Math.Min(detail.Length, 200))}");
                        }
                    }
                }
            }

            return sb.ToString().Trim();
        }

        private string QuerySoNguoi()
        {
            int soGd = _index.Count;
            int soNguoi = _index.Sum(e => e.Family.familyInfo.ListPerson.Count);
            int maxDoi = _index.Count > 0 ? _index.Max(e => e.Family.familyInfo.FamilyLevel) : 0;
            int minDoi = _index.Count > 0 ? _index.Min(e => e.Family.familyInfo.FamilyLevel) : 0;

            return "📊 Thống kê gia phả:\n"
                + $"• Tổng số gia đình: {soGd}\n"
                + $"• Tổng số người: {soNguoi}\n"
                + $"• Số đời ghi nhận: {maxDoi - minDoi + 1} (đời {minDoi} → đời {maxDoi})";
        }

        private string QueryDoiSo(int level)
        {
            var families = _index.Where(e => e.Family.familyInfo.FamilyLevel == level).ToList();
            if (families.Count == 0)
            {
                return $"Không có gia đình nào ở đời {level}.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"📋 Đời {level} ({families.Count} gia đình):");
            foreach (var e in families.Take(25))
            {
                sb.AppendLine("  • " + GetPersonsLine(e.Family.familyInfo));
            }

            if (families.Count > 25)
            {
                sb.AppendLine($"  ... (còn {families.Count - 25} gia đình nữa)");
            }

            return sb.ToString().Trim();
        }

        private string QueryCurrentFamily(FamilyViewModel fvm, string q)
        {
            if (fvm == null)
            {
                return "Chưa chọn gia đình nào trên cây.\nHãy click vào một gia đình trên cây gia phả.";
            }

            // Tìm entry có parent reference
            var entry = _index.FirstOrDefault(e => ReferenceEquals(e.Family, fvm))
                       ?? new SearchEntry { Family = fvm, Parent = null };

            if (ContainsAny(q, "con cai", "con cua", "co may con"))
            {
                return QueryConCua(entry);
            }

            if (ContainsAny(q, "cha cua", "ba cua"))
            {
                return QueryChaCua(entry);
            }

            if (ContainsAny(q, "me cua", "ma cua"))
            {
                return QueryMeCua(entry);
            }

            if (ContainsAny(q, "anh em", "anh chi em"))
            {
                return QueryAnhEm(entry);
            }

            if (ContainsAny(q, "ngay ky", "ngay gio", "ngay mat", "ngay chet"))
                return QueryNgayKy(entry);

            if (ContainsAny(q, "mo ", "phan mo"))
                return QueryMo(entry);

            // Tổ tiên nhiều cấp
            if (ContainsAny(q, "ong noi", "ba noi"))
                return QueryToTienN(entry, 2);

            if (ContainsAny(q, "ong co", "ba co", "cu ong", "cu ba", "ong cu", "ba cu"))
                return QueryToTienN(entry, 3);

            // Hậu duệ đầy đủ
            if (ContainsAny(q, "hau due", "tat ca con chau", "toan bo con"))
                return QueryHauDue(entry);

            // Tên tự/thụy/thường
            if (ContainsAny(q, "ten tu", "ten thuy", "ten thuong"))
                return QueryTenTu(entry);

            return QueryThongTinDay(entry);
        }

        private string QueryConCua(SearchEntry entry)
        {
            var children = entry.Family.Children;
            if (children == null || children.Count == 0)
            {
                return $"👶 {GetMainName(entry.Family.familyInfo)} không có con cháu được ghi nhận trong gia phả.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"👶 Con của {GetPersonsLine(entry.Family.familyInfo)} ({children.Count} người):");
            foreach (var child in children)
            {
                string conLine = "  • " + GetPersonsLine(child.familyInfo)
                    + $" — Đời {child.familyInfo.FamilyLevel}";
                // Thêm số cháu nếu có
                if (child.Children != null && child.Children.Count > 0)
                {
                    conLine += $" | {child.Children.Count} cháu";
                }

                sb.AppendLine(conLine);
            }

            return sb.ToString().Trim();
        }

        private string QueryChaCua(SearchEntry entry)
        {
            if (entry.Parent == null)
            {
                return $"🌳 {GetMainName(entry.Family.familyInfo)} là thủy tổ — không có cha được ghi nhận.";
            }

            string tenCha = GetMainName(entry.Parent.familyInfo);
            return $"👨 Cha của {GetMainName(entry.Family.familyInfo)}: {tenCha} (Đời {entry.Parent.familyInfo.FamilyLevel})";
        }

        private string QueryMeCua(SearchEntry entry)
        {
            if (entry.Parent == null)
            {
                return $"{GetMainName(entry.Family.familyInfo)} là thủy tổ — không có mẹ được ghi nhận.";
            }

            var me = entry.Parent.familyInfo.ListPerson.FirstOrDefault(p => p.IsMainPerson == 0);
            string tenMe = me?.MANS_NAME_HUY ?? "(không ghi)";
            return $"👩 Mẹ của {GetMainName(entry.Family.familyInfo)}: {tenMe} (Đời {entry.Parent.familyInfo.FamilyLevel})";
        }

        private string QueryVoChong(SearchEntry entry, string loai)
        {
            // Vợ = người IsMainPerson == 0 trong cùng gia đình
            var spouse = entry.Family.familyInfo.ListPerson.FirstOrDefault(p => p.IsMainPerson == 0);
            if (spouse == null)
            {
                return $"{GetMainName(entry.Family.familyInfo)} không có {loai.ToLower()} được ghi nhận.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"💑 {loai} của {GetMainName(entry.Family.familyInfo)}: {spouse.MANS_NAME_HUY}");
            if (!string.IsNullOrWhiteSpace(spouse.MANS_DOD))
            {
                sb.AppendLine($"   Ngày kỵ: {spouse.MANS_DOD}");
            }

            if (!string.IsNullOrWhiteSpace(spouse.MANS_WOD))
            {
                sb.AppendLine($"   Mộ: {spouse.MANS_WOD}");
            }

            return sb.ToString().Trim();
        }

        private string QueryAnhEm(SearchEntry entry)
        {
            if (entry.Parent == null)
            {
                return $"{GetMainName(entry.Family.familyInfo)} là thủy tổ — không có anh em cùng cha.";
            }

            var siblings = entry.Parent.Children
                .Where(c => !ReferenceEquals(c, entry.Family))
                .ToList();

            if (siblings.Count == 0)
            {
                return $"{GetMainName(entry.Family.familyInfo)} là con một trong gia đình này.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"👥 Anh/chị/em của {GetMainName(entry.Family.familyInfo)} ({siblings.Count} người):");
            foreach (var s in siblings)
            {
                sb.AppendLine("  • " + GetPersonsLine(s.familyInfo));
            }

            return sb.ToString().Trim();
        }

        private string QueryChauCua(SearchEntry entry)
        {
            var grandChildren = entry.Family.Children != null
                ? entry.Family.Children
                    .SelectMany(c => c.Children ?? Enumerable.Empty<FamilyViewModel>())
                    .ToList()
                : new List<FamilyViewModel>();

            if (grandChildren.Count == 0)
            {
                return $"{GetMainName(entry.Family.familyInfo)} chưa có cháu nội được ghi nhận trong gia phả.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"👶 Cháu của {GetMainName(entry.Family.familyInfo)} ({grandChildren.Count} gia đình):");
            foreach (var gc in grandChildren.Take(25))
            {
                sb.AppendLine($"  • {GetPersonsLine(gc.familyInfo)} — Đời {gc.familyInfo.FamilyLevel}");
            }

            if (grandChildren.Count > 25)
            {
                sb.AppendLine($"  ... (còn {grandChildren.Count - 25} gia đình nữa)");
            }

            return sb.ToString().Trim();
        }

        private string QueryNgayKy(SearchEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🕯️ Ngày kỵ — {GetPersonsLine(entry.Family.familyInfo)} (Đời {entry.Family.familyInfo.FamilyLevel}):");
            bool found = false;
            foreach (var p in entry.Family.familyInfo.ListPerson)
            {
                if (!string.IsNullOrWhiteSpace(p.MANS_DOD))
                {
                    sb.AppendLine($"  • {p.MANS_NAME_HUY}: {p.MANS_DOD}");
                    found = true;
                }
            }

            if (!found)
            {
                sb.AppendLine("  (Không có thông tin ngày kỵ)");
            }

            return sb.ToString().Trim();
        }

        private string QueryMo(SearchEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🪦 Nơi mộ — {GetPersonsLine(entry.Family.familyInfo)} (Đời {entry.Family.familyInfo.FamilyLevel}):");
            bool found = false;
            foreach (var p in entry.Family.familyInfo.ListPerson)
            {
                if (!string.IsNullOrWhiteSpace(p.MANS_WOD))
                {
                    sb.AppendLine($"  • {p.MANS_NAME_HUY}: {p.MANS_WOD}");
                    found = true;
                }
            }

            if (!found)
            {
                sb.AppendLine("  (Không có thông tin nơi mộ)");
            }

            return sb.ToString().Trim();
        }

        private string QueryDoiMay(SearchEntry entry)
        {
            return $"📅 {GetMainName(entry.Family.familyInfo)} thuộc đời {entry.Family.familyInfo.FamilyLevel}.";
        }

        private string QueryGhiChu(SearchEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📝 Ghi chú — {GetPersonsLine(entry.Family.familyInfo)}:");
            bool found = false;
            foreach (var p in entry.Family.familyInfo.ListPerson)
            {
                if (!string.IsNullOrWhiteSpace(p.MANS_DETAIL))
                {
                    string detail = StripHtml(p.MANS_DETAIL);
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        sb.AppendLine($"  • {p.MANS_NAME_HUY}: {detail}");
                        found = true;
                    }
                }
            }

            if (!found)
            {
                sb.AppendLine("  (Không có ghi chú)");
            }

            return sb.ToString().Trim();
        }

        private string QueryThongTinDay(SearchEntry entry)
        {
            var fi = entry.Family.familyInfo;
            var sb = new StringBuilder();
            sb.AppendLine($"📋 Thông tin — Đời {fi.FamilyLevel}:");

            foreach (var p in fi.ListPerson)
            {
                string role = p.IsMainPerson == 1 ? "Con trong tộc" : "Vợ/Chồng ngoài";
                sb.AppendLine($"  [{role}] {p.MANS_NAME_HUY} ({p.MANS_GENDER})");
                if (!string.IsNullOrWhiteSpace(p.MANS_NAME_TU))
                {
                    sb.AppendLine($"    Tự/Huý: {p.MANS_NAME_TU}");
                }

                if (!string.IsNullOrWhiteSpace(p.MANS_DOB))
                {
                    sb.AppendLine($"    Sinh: {p.MANS_DOB}");
                }

                if (!string.IsNullOrWhiteSpace(p.MANS_DOD))
                {
                    sb.AppendLine($"    Ngày kỵ: {p.MANS_DOD}");
                }

                if (!string.IsNullOrWhiteSpace(p.MANS_WOD))
                {
                    sb.AppendLine($"    Mộ: {p.MANS_WOD}");
                }

                if (!string.IsNullOrWhiteSpace(p.MANS_DETAIL))
                {
                    string detail = StripHtml(p.MANS_DETAIL);
                    if (!string.IsNullOrWhiteSpace(detail))
                    {
                        sb.AppendLine($"    Ghi chú: {detail}");
                    }
                }
            }

            // Cha mẹ
            if (entry.Parent != null)
            {
                sb.AppendLine($"  Cha: {GetMainName(entry.Parent.familyInfo)} (Đời {entry.Parent.familyInfo.FamilyLevel})");
            }
            else
            {
                sb.AppendLine("  (Là thủy tổ)");
            }

            // Con
            if (entry.Family.Children != null && entry.Family.Children.Count > 0)
            {
                var childNames = entry.Family.Children.Take(8)
                    .Select(c => GetPersonsLine(c.familyInfo))
                    .ToList();
                sb.AppendLine($"  Con ({entry.Family.Children.Count}): "
                    + string.Join(", ", childNames)
                    + (entry.Family.Children.Count > 8 ? " ..." : ""));
            }

            return sb.ToString().Trim();
        }

        // ── Tổ tiên nhiều cấp, hậu duệ, năm sinh/mất, lọc, tên tự ──────────────

        /// <summary>
        /// Leo lên <paramref name="levels"/> cấp từ entry để tìm tổ tiên.
        /// Ví dụ: levels=2 → ông/bà nội; levels=3 → ông/bà cố; levels=4 → kỵ.
        /// </summary>
        private string QueryToTienN(SearchEntry entry, int levels)
        {
            string tenBanThan = GetMainName(entry.Family.familyInfo);
            SearchEntry current = entry;

            for (int i = 0; i < levels; i++)
            {
                if (current.Parent == null)
                {
                    return $"{tenBanThan} là thủy tổ nhánh — không thể truy lên {levels} cấp.\n"
                         + $"(Chỉ truy được {i} cấp tính từ người này)";
                }

                FamilyViewModel parentVm = current.Parent;
                SearchEntry parentEntry = null;
                foreach (var e in _index)
                {
                    if (ReferenceEquals(e.Family, parentVm))
                    {
                        parentEntry = e;
                        break;
                    }
                }

                if (parentEntry == null)
                {
                    // Có cha trong cây nhưng không có trong index — dùng FamilyViewModel trực tiếp
                    bool isMaleParent = parentVm.familyInfo?.ListPerson?
                        .FirstOrDefault(p => p.IsMainPerson == 1)?.IsGioiTinhNam == 1;
                    string role = GetAncestorTerm(i + 1, isMaleParent);
                    return $"🌳 {role} của {tenBanThan}: {GetMainName(parentVm.familyInfo)} "
                         + $"(Đời {parentVm.familyInfo.FamilyLevel})";
                }

                current = parentEntry;
            }

            string finalRole = GetAncestorTerm(levels, GetIsMale(current));
            var fi = current.Family.familyInfo;
            var sb = new StringBuilder();
            sb.AppendLine($"🌳 {finalRole} của {tenBanThan}:");
            sb.AppendLine($"  {GetPersonsLine(fi)} — Đời {fi.FamilyLevel}");

            foreach (var p in fi.ListPerson)
            {
                if (!string.IsNullOrWhiteSpace(p.MANS_DOD))
                    sb.AppendLine($"  Ngày kỵ {p.MANS_NAME_HUY}: {p.MANS_DOD}");
                if (!string.IsNullOrWhiteSpace(p.MANS_WOD))
                    sb.AppendLine($"  Mộ {p.MANS_NAME_HUY}: {p.MANS_WOD}");
            }

            return sb.ToString().Trim();
        }

        /// <summary>Liệt kê toàn bộ hậu duệ theo cây đệ quy (tối đa 10 thế hệ, 100 người).</summary>
        private string QueryHauDue(SearchEntry entry)
        {
            string tenBanThan = GetPersonsLine(entry.Family.familyInfo);
            var hauDue = new List<(FamilyViewModel vm, int depth)>();
            CollectDescendantsToList(entry.Family, 0, hauDue, 100);

            if (hauDue.Count == 0)
                return $"{tenBanThan} không có con cháu được ghi nhận trong gia phả.";

            var sb = new StringBuilder();
            sb.AppendLine($"🌲 Toàn bộ hậu duệ của {tenBanThan} (Đời {entry.Family.familyInfo.FamilyLevel}):");

            foreach (var item in hauDue)
            {
                string indent = new string(' ', item.depth * 2 + 2);
                sb.AppendLine($"{indent}• {GetPersonsLine(item.vm.familyInfo)} — Đời {item.vm.familyInfo.FamilyLevel}");
            }

            bool truncated = hauDue.Count >= 100;
            sb.AppendLine($"\n📊 Tổng: {hauDue.Count}{(truncated ? "+" : "")} hậu duệ");
            return sb.ToString().Trim();
        }

        private static void CollectDescendantsToList(
            FamilyViewModel vm, int depth,
            List<(FamilyViewModel, int)> result, int maxItems)
        {
            if (depth > 10 || vm.Children == null || result.Count >= maxItems) return;
            foreach (var child in vm.Children)
            {
                if (result.Count >= maxItems) break;
                result.Add((child, depth));
                CollectDescendantsToList(child, depth + 1, result, maxItems);
            }
        }

        /// <summary>Tìm người sinh (isBirth=true) hoặc mất (isBirth=false) trong năm chỉ định.</summary>
        private string QueryTheoNam(int year, bool isBirth)
        {
            string loai = isBirth ? "sinh" : "mất/kỵ";
            string yearStr = year.ToString();
            var results = new List<(string ten, string doi, string ngay)>();

            foreach (var e in _index)
            {
                foreach (var p in e.Family.familyInfo.ListPerson)
                {
                    string dateField = isBirth ? p.MANS_DOB : p.MANS_DOD;
                    if (!string.IsNullOrWhiteSpace(dateField) && dateField.Contains(yearStr))
                        results.Add((
                            p.MANS_NAME_HUY ?? "(không rõ)",
                            $"Đời {e.Family.familyInfo.FamilyLevel}",
                            dateField));
                }
            }

            if (results.Count == 0)
                return $"Không tìm thấy ai {loai} năm {year} trong gia phả.";

            var sb = new StringBuilder();
            sb.AppendLine($"📅 Người {loai} năm {year} ({results.Count} người):");
            foreach (var r in results.Take(30))
                sb.AppendLine($"  • {r.ten} — {r.doi} | {loai}: {r.ngay}");

            if (results.Count > 30)
                sb.AppendLine($"  ... (còn {results.Count - 30} người nữa)");

            return sb.ToString().Trim();
        }

        /// <summary>Liệt kê người chưa có con cháu ghi nhận trong gia phả.</summary>
        private string QueryChuaCoCon()
        {
            var list = _index
                .Where(e => e.Family.Children == null || e.Family.Children.Count == 0)
                .ToList();

            if (list.Count == 0)
                return "Mọi người trong gia phả đều có con cháu được ghi nhận!";

            var sb = new StringBuilder();
            sb.AppendLine($"📋 Người chưa có con cháu ghi nhận ({list.Count} người):");
            foreach (var e in list.Take(30))
                sb.AppendLine($"  • {GetPersonsLine(e.Family.familyInfo)} — Đời {e.Family.familyInfo.FamilyLevel}");

            if (list.Count > 30)
                sb.AppendLine($"  ... (còn {list.Count - 30} người nữa)");

            return sb.ToString().Trim();
        }

        /// <summary>Hiển thị tên tự/thụy/thường gọi của từng thành viên trong gia đình.</summary>
        private static string QueryTenTu(SearchEntry entry)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"📛 Tên tự/thụy/thường — {GetPersonsLine(entry.Family.familyInfo)} "
                        + $"(Đời {entry.Family.familyInfo.FamilyLevel}):");

            bool found = false;
            foreach (var p in entry.Family.familyInfo.ListPerson)
            {
                bool hasTu = !string.IsNullOrWhiteSpace(p.MANS_NAME_TU);
                bool hasThuong = !string.IsNullOrWhiteSpace(p.MANS_NAME_THUONG);
                if (!hasTu && !hasThuong) continue;

                sb.Append($"  • {p.MANS_NAME_HUY}");
                if (hasTu) sb.Append($" — Tự/Huý: {p.MANS_NAME_TU}");
                if (hasThuong) sb.Append($" — Thường gọi: {p.MANS_NAME_THUONG}");
                sb.AppendLine();
                found = true;
            }

            if (!found)
                sb.AppendLine("  (Không có tên tự/thụy/thường được ghi nhận)");

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Phát hiện câu "Ai là [vai trò] của [tên]?" và trả về câu đã chuẩn hóa.
        /// Ví dụ: "Ai là cha của Nguyễn Văn A?" → "cha cua Nguyễn Văn A?".
        /// Trả về null nếu không phải dạng này.
        /// Lưu ý: vị trí char trong qNormalized khớp 1:1 với original.Trim()
        /// vì RemoveDiacritics + ToLower là ánh xạ 1:1 (không thay đổi độ dài chuỗi).
        /// </summary>
        private static string TryNormalizeAiLaQuestion(string original, string qNormalized)
        {
            if (string.IsNullOrWhiteSpace(qNormalized)) return null;

            // Pattern: "ai là [role] của [tên]?"
            var m = Regex.Match(qNormalized,
                @"^ai\s+la\s+([\w ]+?)\s+cua\s+(.+?)(\?)?$");
            if (m.Success)
            {
                string role = m.Groups[1].Value.Trim();
                // Lấy tên gốc có dấu dựa trên vị trí trong original.Trim()
                string trimmed = original.Trim();
                int nameIdx = m.Groups[2].Index;
                int nameLen = m.Groups[2].Length;
                string nameOriginal = (nameIdx + nameLen <= trimmed.Length)
                    ? trimmed.Substring(nameIdx, nameLen)
                    : m.Groups[2].Value;
                // Dùng "cua" không dấu để ExtractName nhận dạng được
                return role + " cua " + nameOriginal.Trim() + "?";
            }

            // Pattern: "ai có con tên [tên]?" / "ai sinh ra [tên]?"
            m = Regex.Match(qNormalized,
                @"^ai\s+(?:co\s+con(?:\s+ten)?\s+|sinh\s+ra\s+)(.+?)(\?)?$");
            if (m.Success)
            {
                string trimmed = original.Trim();
                int nameIdx = m.Groups[1].Index;
                int nameLen = m.Groups[1].Length;
                string nameOriginal = (nameIdx + nameLen <= trimmed.Length)
                    ? trimmed.Substring(nameIdx, nameLen)
                    : m.Groups[1].Value;
                return "cha cua " + nameOriginal.Trim() + "?";
            }

            return null;
        }

        // ── Search helpers ────────────────────────────────────────────────────

        private List<SearchEntry> FindByName(string normalizedKeyword)
        {
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return new List<SearchEntry>();
            }

            // Tìm theo tên đã bỏ dấu để bắt cả trường hợp gõ không dấu
            return _index.Where(e =>
                e.Family.familyInfo.ListPerson.Any(p =>
                    (!string.IsNullOrEmpty(p.MANS_NAME_HUY)
                        && RemoveDiacritics(p.MANS_NAME_HUY.ToLowerInvariant()).Contains(normalizedKeyword))
                    || (!string.IsNullOrEmpty(p.MANS_NAME_TU)
                        && RemoveDiacritics(p.MANS_NAME_TU.ToLowerInvariant()).Contains(normalizedKeyword))
                    || (!string.IsNullOrEmpty(p.MANS_NAME_THUONG)
                        && RemoveDiacritics(p.MANS_NAME_THUONG.ToLowerInvariant()).Contains(normalizedKeyword))
                )
            ).ToList();
        }

        /// <summary>Trích tên người từ câu hỏi bằng cách loại prefix/suffix phổ biến.</summary>
        private static string ExtractName(string originalQuestion)
        {
            // Danh sách prefix cần loại bỏ (dùng bản gốc có dấu)
            string[] prefixes = {
                "con của ", "con cua ", "con cái của ", "con cai cua ",
                "cha của ", "cha cua ", "ba của ", "ba cua ",
                "mẹ của ", "me cua ", "mẹ ruột của ", "ma cua ",
                "vợ của ", "vo cua ", "vợ là ", "vo la ",
                "chồng của ", "chong cua ", "chồng là ", "chong la ",
                "anh em của ", "anh em cua ", "anh chị em của ", "anh chi em cua ",
                "cháu của ", "chau cua ", "cháu nội của ", "chau noi cua ",
                "ngày kỵ của ", "ngay ky cua ", "ngày kỵ ", "ngay ky ",
                "ngày giỗ của ", "ngay gio cua ", "ngày giỗ ", "ngay gio ",
                "ngày mất của ", "ngay mat cua ", "ngày mất ", "ngay mat ",
                "ngày chết của ", "ngay chet cua ",
                "mộ của ", "mo cua ", "phần mộ của ", "phan mo cua ",
                "đời mấy của ", "doi may cua ", "đời mấy ", "doi may ",
                // Tổ tiên nhiều cấp
                "ông nội của ", "ong noi cua ", "bà nội của ", "ba noi cua ",
                "ông cố của ", "ong co cua ", "bà cố của ", "ba co cua ",
                "ông cụ của ", "ong cu cua ", "bà cụ của ", "ba cu cua ",
                "kỵ của ", "ky cua ",
                // Hậu duệ / tên tự
                "hậu duệ của ", "hau due cua ",
                "tên tự của ", "ten tu cua ", "tên thụy của ", "ten thuy cua ",
                "tên thường của ", "ten thuong cua ",
                "thông tin về ", "thong tin ve ", "thông tin của ", "thong tin cua ",
                "ghi chú về ", "ghi chu ve ", "ghi chú của ", "ghi chu cua ",
                "tra cứu ", "tra cuu ", "tìm ", "tim ",
                "cho tôi biết về ", "cho toi biet ve ",
                "hỏi về ", "hoi ve ",
            };

            string[] suffixes = {
                " là ai?", " la ai?", " là ai", " la ai",
                " đời mấy?", " doi may?", " đời mấy", " doi may",
                " ở đâu?", " o dau?", " ở đâu", " o dau",
                " có ai?", " co ai?", "?", "!",
            };

            string s = originalQuestion.Trim();

            // Loại prefix — so sánh không phân biệt hoa/thường
            foreach (var pre in prefixes)
            {
                if (s.StartsWith(pre, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(pre.Length).Trim();
                    break; // chỉ loại 1 prefix
                }
            }

            // Loại suffix
            foreach (var suf in suffixes)
            {
                if (s.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - suf.Length).Trim();
                    break;
                }
            }

            return s.Trim();
        }

        // ── Format helpers ────────────────────────────────────────────────────

        private string FormatFamilyShort(FamilyViewModel vm)
        {
            var fi = vm?.familyInfo;
            if (fi == null)
            {
                return "(không có dữ liệu)";
            }

            return $"• {GetPersonsLine(fi)} — Đời {fi.FamilyLevel}";
        }

        private string FormatMultipleResults(string name, List<SearchEntry> entries)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"🔍 Tìm thấy {entries.Count} kết quả cho \"{name}\":");
            foreach (var e in entries.Take(10))
            {
                string parentName = e.Parent != null
                    ? $" | Cha: {GetMainName(e.Parent.familyInfo)}"
                    : " | Thủy tổ";
                sb.AppendLine($"  • {GetPersonsLine(e.Family.familyInfo)} — Đời {e.Family.familyInfo.FamilyLevel}{parentName}");
            }

            if (entries.Count > 10)
            {
                sb.AppendLine($"  ... (còn {entries.Count - 10} kết quả nữa)");
            }

            sb.AppendLine("\nHỏi cụ thể hơn, ví dụ: \"Con của [tên đầy đủ]\" hoặc \"[tên] đời mấy\"");
            return sb.ToString().Trim();
        }

        private static string GetPersonsLine(FamilyInfo fi)
        {
            if (fi?.ListPerson == null || fi.ListPerson.Count == 0)
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

        private static string GetMainName(FamilyInfo fi)
        {
            if (fi?.ListPerson == null)
            {
                return "(không rõ)";
            }

            var main = fi.ListPerson.FirstOrDefault(p => p.IsMainPerson == 1)
                      ?? fi.ListPerson.FirstOrDefault();
            return main?.MANS_NAME_HUY ?? "(không rõ)";
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return "";
            }

            return Regex.Replace(html, "<[^>]+>", " ")
                        .Replace("&nbsp;", " ")
                        .Replace("&amp;", "&")
                        .Trim();
        }

        // ── Tìm quan hệ 2 người ──────────────────────────────────────────────

        /// <summary>
        /// Thử tách câu hỏi thành 2 tên người.
        /// Hỗ trợ: "X và Y", "X với Y", "X là gì của Y", "quan hệ X và Y".
        /// </summary>
        private static bool TryExtractTwoNames(string original, out string n1, out string n2)
        {
            n1 = n2 = null;
            if (string.IsNullOrWhiteSpace(original))
            {
                return false;
            }

            string s = original.Trim();

            // Loại bỏ prefix câu hỏi
            string[] removePrefix = {
                "quan hệ giữa ", "quan he giua ",
                "quan hệ của ", "quan he cua ",
                "mối quan hệ giữa ", "moi quan he giua ",
                "tìm quan hệ ", "tim quan he ",
                "cho biết quan hệ giữa ", "cho biet quan he giua ",
            };
            foreach (var pre in removePrefix)
            {
                if (s.StartsWith(pre, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(pre.Length).Trim();
                    break;
                }
            }

            // Loại bỏ suffix
            string[] removeSuffix = {
                " có quan hệ gì?", " co quan he gi?",
                " có quan hệ gì", " co quan he gi",
                " là gì?", " la gi?", " là gì", " la gi",
                " quan hệ gì?", " quan he gi?",
                "?",
            };
            foreach (var suf in removeSuffix)
            {
                if (s.EndsWith(suf, StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring(0, s.Length - suf.Length).Trim();
                    break;
                }
            }

            // Pattern "X là gì của Y"
            var matchLaGi = Regex.Match(s, @"^(.+?)\s+la\s+gi\s+cua\s+(.+)$",
                RegexOptions.IgnoreCase);
            if (!matchLaGi.Success)
            {
                matchLaGi = Regex.Match(RemoveDiacritics(s.ToLowerInvariant()),
                    @"^(.+?)\s+la\s+gi\s+cua\s+(.+)$");
            }

            if (matchLaGi.Success)
            {
                n1 = s.Substring(matchLaGi.Groups[1].Index, matchLaGi.Groups[1].Length).Trim();
                n2 = s.Substring(matchLaGi.Groups[2].Index, matchLaGi.Groups[2].Length).Trim();
                return !string.IsNullOrWhiteSpace(n1) && !string.IsNullOrWhiteSpace(n2);
            }

            // Pattern "X và Y" / "X với Y" / "X + Y" — tìm separator
            string[] seps = { " và ", " va ", " với ", " voi ", " + ", "+" };
            foreach (var sep in seps)
            {
                int idx = s.IndexOf(sep, StringComparison.OrdinalIgnoreCase);
                if (idx > 2 && idx < s.Length - sep.Length - 2)
                {
                    n1 = s.Substring(0, idx).Trim();
                    n2 = s.Substring(idx + sep.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(n1) && !string.IsNullOrWhiteSpace(n2))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        // Kiểu nội bộ lưu 1 kết quả quan hệ trước khi sắp xếp
        private sealed class RelationshipResult
        {
            public string LabelA;
            public string LabelB;
            public string Description;
            public int TotalDist; // depthA + depthB — càng nhỏ càng gần
        }

        /// <summary>Tìm và trình bày mối quan hệ giữa 2 người — kết quả gần nhất hiện trước.</summary>
        private string QueryRelationship(string nameA, string nameB)
        {
            string normA = RemoveDiacritics(nameA.ToLowerInvariant());
            string normB = RemoveDiacritics(nameB.ToLowerInvariant());

            var listA = FindByName(normA);
            var listB = FindByName(normB);

            if (listA.Count == 0)
            {
                return $"Không tìm thấy \"{nameA}\" trong gia phả.";
            }

            if (listB.Count == 0)
            {
                return $"Không tìm thấy \"{nameB}\" trong gia phả.";
            }

            // Thu thập tất cả cặp có quan hệ
            var results = new List<RelationshipResult>();

            foreach (var eA in listA)
            {
                foreach (var eB in listB)
                {
                    if (ReferenceEquals(eA.Family, eB.Family))
                    {
                        // Cùng gia đình — vợ chồng (khoảng cách = 0, gần nhất)
                        results.Add(new RelationshipResult
                        {
                            LabelA = GetPersonsLine(eA.Family.familyInfo),
                            LabelB = GetPersonsLine(eB.Family.familyInfo),
                            Description = $"💑 {GetMainName(eA.Family.familyInfo)} và {GetMainName(eB.Family.familyInfo)} là vợ chồng trong cùng gia đình.",
                            TotalDist = 0
                        });
                        continue;
                    }

                    int dist;
                    string rel = ComputeRelationship(eA, eB, out dist);
                    if (rel != null)
                    {
                        results.Add(new RelationshipResult
                        {
                            LabelA = $"{GetPersonsLine(eA.Family.familyInfo)} (Đời {eA.Family.familyInfo.FamilyLevel})",
                            LabelB = $"{GetPersonsLine(eB.Family.familyInfo)} (Đời {eB.Family.familyInfo.FamilyLevel})",
                            Description = rel,
                            TotalDist = dist
                        });
                    }
                }
            }

            if (results.Count == 0)
            {
                return $"Không tìm thấy mối quan hệ giữa \"{nameA}\" và \"{nameB}\".\n"
                    + "Có thể họ thuộc các nhánh không cùng gốc hoặc chưa được ghi nhận.";
            }

            // Sắp xếp: quan hệ gần nhất (tổng số đời đến LCA nhỏ nhất) lên trước
            results.Sort((x, y) => x.TotalDist.CompareTo(y.TotalDist));

            var sb = new StringBuilder();
            bool isMultiple = results.Count > 1;

            for (int idx = 0; idx < results.Count; idx++)
            {
                var r = results[idx];
                if (idx > 0)
                {
                    sb.AppendLine();
                }

                if (isMultiple)
                {
                    // Nhãn phân biệt khi có nhiều cặp
                    sb.AppendLine($"👨‍👩‍👧 {r.LabelA}");
                    sb.AppendLine($"   với {r.LabelB}:");
                    sb.AppendLine($"   {r.Description}");
                }
                else
                {
                    // Chỉ 1 cặp — trình bày gọn hơn
                    sb.AppendLine(r.Description);
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>
        /// Tính quan hệ giữa 2 SearchEntry.
        /// Trả về chuỗi mô tả; totalDist = depthA + depthB (khoảng cách đến LCA).
        /// Trả về null và totalDist = int.MaxValue nếu không cùng gốc.
        /// </summary>
        private string ComputeRelationship(SearchEntry eA, SearchEntry eB, out int totalDist)
        {
            totalDist = int.MaxValue;
            var pathA = GetPathToRoot(eA);
            var pathB = GetPathToRoot(eB);

            // Tìm LCA bằng cách duyệt path B, kiểm tra xem có trong path A không
            var familiesInA = new Dictionary<FamilyViewModel, int>();
            for (int i = 0; i < pathA.Count; i++)
            {
                familiesInA[pathA[i].Family] = i;
            }

            int depthB = 0;
            SearchEntry lcaEntry = null;
            for (int j = 0; j < pathB.Count; j++)
            {
                int i;
                if (familiesInA.TryGetValue(pathB[j].Family, out i))
                {
                    lcaEntry = pathA[i];
                    depthB = j;
                    break;
                }
            }

            if (lcaEntry == null)
            {
                return null; // Không có tổ tiên chung
            }

            int depthA = familiesInA[lcaEntry.Family];
            totalDist = depthA + depthB;

            return DescribeRelationship(eA, eB, depthA, depthB);
        }

        /// <summary>Lấy danh sách SearchEntry từ entry đến root (gốc là cuối list).</summary>
        private List<SearchEntry> GetPathToRoot(SearchEntry start)
        {
            var path = new List<SearchEntry>();
            var visited = new HashSet<FamilyViewModel>();
            var current = start;

            while (current != null && !visited.Contains(current.Family))
            {
                path.Add(current);
                visited.Add(current.Family);

                if (current.Parent == null)
                {
                    break;
                }

                // Tìm SearchEntry của cha trong index
                FamilyViewModel parentVm = current.Parent;
                current = null;
                foreach (var e in _index)
                {
                    if (ReferenceEquals(e.Family, parentVm))
                    {
                        current = e;
                        break;
                    }
                }
            }

            return path;
        }

        /// <summary>Mô tả quan hệ dựa trên khoảng cách đến LCA.</summary>
        private static string DescribeRelationship(SearchEntry eA, SearchEntry eB,
            int depthA, int depthB)
        {
            string nameA = GetMainName(eA.Family.familyInfo);
            string nameB = GetMainName(eB.Family.familyInfo);
            bool aMale = GetIsMale(eA);
            bool bMale = GetIsMale(eB);

            // A là tổ tiên trực tiếp của B
            if (depthA == 0 && depthB > 0)
            {
                string aRole = GetAncestorTerm(depthB, aMale);
                string bRole = GetDescendantTerm(depthB, bMale);
                return $"{nameA} là {aRole} của {nameB}\n   ({nameB} là {bRole} của {nameA})";
            }

            // B là tổ tiên trực tiếp của A
            if (depthB == 0 && depthA > 0)
            {
                string bRole = GetAncestorTerm(depthA, bMale);
                string aRole = GetDescendantTerm(depthA, aMale);
                return $"{nameB} là {bRole} của {nameA}\n   ({nameA} là {aRole} của {nameB})";
            }

            // Bàng hệ — cùng tổ tiên chung nhưng khác nhánh
            return DescribeCollateralRelationship(nameA, nameB, aMale, bMale, depthA, depthB);
        }

        private static string DescribeCollateralRelationship(string nameA, string nameB,
            bool aMale, bool bMale, int depthA, int depthB)
        {
            // Đảm bảo depthA <= depthB (A gần gốc hơn hoặc bằng)
            // Nếu depthA > depthB thì đổi vai
            if (depthA > depthB)
            {
                return DescribeCollateralRelationship(nameB, nameA, bMale, aMale, depthB, depthA);
            }

            // depthA <= depthB
            string roleAtoB, roleBtoA;

            if (depthA == 1 && depthB == 1)
            {
                // Anh/chị/em ruột
                string termA = aMale ? "anh/em trai" : "chị/em gái";
                string termB = bMale ? "anh/em trai" : "chị/em gái";
                return $"{nameA} và {nameB} là anh/chị/em ruột\n"
                    + $"   ({nameA}: {termA}; {nameB}: {termB})";
            }

            if (depthA == 1 && depthB == 2)
            {
                // Chú/bác/cô ↔ cháu
                roleAtoB = aMale ? "chú/bác" : "cô";
                roleBtoA = bMale ? "cháu trai" : "cháu gái";
                return $"{nameA} là {roleAtoB} của {nameB}\n   ({nameB} là {roleBtoA} của {nameA})";
            }

            if (depthA == 1 && depthB == 3)
            {
                roleAtoB = aMale ? "ông chú/bác" : "bà cô";
                roleBtoA = "cháu họ";
                return $"{nameA} là {roleAtoB} của {nameB}\n   ({nameB} là {roleBtoA} của {nameA})";
            }

            if (depthA == 2 && depthB == 2)
            {
                // Anh/em họ (cùng ông nội)
                string termA = aMale ? "anh/em họ (nam)" : "chị/em họ (nữ)";
                string termB = bMale ? "anh/em họ (nam)" : "chị/em họ (nữ)";
                return $"{nameA} và {nameB} là anh/chị/em họ (cùng ông nội)\n"
                    + $"   ({nameA}: {termA}; {nameB}: {termB})";
            }

            if (depthA == 2 && depthB == 3)
            {
                roleAtoB = aMale ? "chú/bác họ" : "cô họ";
                roleBtoA = "cháu họ";
                return $"{nameA} là {roleAtoB} của {nameB}\n   ({nameB} là {roleBtoA} của {nameA})";
            }

            if (depthA == 3 && depthB == 3)
            {
                return $"{nameA} và {nameB} là anh/chị/em họ xa (cùng cụ)";
            }

            // Trường hợp chung — mô tả theo số đời
            int totalDist = depthA + depthB;
            if (depthA == depthB)
            {
                return $"{nameA} và {nameB} là họ hàng cùng đời (cách {depthA} đời đến tổ tiên chung)";
            }

            return $"{nameA} và {nameB} là họ hàng xa:\n"
                + $"   {nameA} cách tổ tiên chung {depthA} đời\n"
                + $"   {nameB} cách tổ tiên chung {depthB} đời";
        }

        private static string GetAncestorTerm(int depth, bool isMale)
        {
            switch (depth)
            {
                case 1: return isMale ? "cha" : "mẹ";
                case 2: return isMale ? "ông nội" : "bà nội";
                case 3: return isMale ? "cụ (ông cố)" : "cụ (bà cố)";
                case 4: return "kỵ";
                default: return $"tổ tiên đời {depth}";
            }
        }

        private static string GetDescendantTerm(int depth, bool isMale)
        {
            switch (depth)
            {
                case 1: return isMale ? "con trai" : "con gái";
                case 2: return isMale ? "cháu nội (trai)" : "cháu nội (gái)";
                case 3: return "chắt";
                case 4: return "chút";
                default: return $"hậu duệ đời {depth}";
            }
        }

        private static bool GetIsMale(SearchEntry e)
        {
            var main = e.Family.familyInfo.ListPerson.FirstOrDefault(p => p.IsMainPerson == 1)
                      ?? e.Family.familyInfo.ListPerson.FirstOrDefault();
            return main?.IsGioiTinhNam == 1;
        }

        private static bool ContainsAny(string s, params string[] keywords)
        {
            foreach (var kw in keywords)
            {
                if (s.Contains(kw))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Bỏ dấu tiếng Việt để so sánh không phân biệt dấu.</summary>
        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            // Bảng thay thế các ký tự có dấu → không dấu
            string[] from = {
                "àáâãäåăắặầấẩẫạậ", "èéêëếềệểễ", "ìíîïịỉ", "òóôõöộởờớọổỗ",
                "ùúûüưứừụủữựừ", "ýỳỵỷỹ", "đ",
                "ÀÁÂÃÄÅĂẮẶẦẤẨẪẠẬ", "ÈÉÊËẾỀỆỂỄ", "ÌÍÎÏỊỈ", "ÒÓÔÕÖỘỞỜỚỌỔỖ",
                "ÙÚÛÜƯỨỪỤỦỮỰỪ", "ÝỲỴỶỸ", "Đ",
            };
            string[] to = {
                "a", "e", "i", "o", "u", "y", "d",
                "A", "E", "I", "O", "U", "Y", "D",
            };

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                bool replaced = false;
                for (int i = 0; i < from.Length; i++)
                {
                    if (from[i].IndexOf(c) >= 0)
                    {
                        sb.Append(to[i]);
                        replaced = true;
                        break;
                    }
                }

                if (!replaced)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }

    internal sealed class SearchEntry
    {
        public FamilyViewModel Family;
        public FamilyViewModel Parent;
    }
}
