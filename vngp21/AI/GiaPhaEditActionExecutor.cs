using System;
using System.Collections.Generic;
using System.Linq;

namespace vietnamgiapha.AI
{
    /// <summary>Thực thi lệnh sửa gia phả từ intent — phải chạy trên UI thread.</summary>
    public static class GiaPhaEditActionExecutor
    {
        public static GiaPhaQueryResult Execute(
            GiaPhaIntent intent,
            FamilyViewModel currentFamily,
            FamilyViewModel fileRoot,
            GiaPhaQueryEngine engine,
            GiaPhaParseSource parseSource)
        {
            if (intent == null)
            {
                return GiaPhaQueryResult.Fail("Không có intent.");
            }

            if (fileRoot == null)
            {
                return GiaPhaQueryResult.Fail("⚠️ Chưa mở file gia phả.");
            }

            string kind = intent.Kind;
            FamilyViewModel target;
            string resolveError;
            if (!TryResolveTargetFamily(intent, currentFamily, engine, out target, out resolveError))
            {
                return GiaPhaQueryResult.Fail(resolveError);
            }

            try
            {
                switch (kind)
                {
                    case GiaPhaIntentKinds.AddPerson:
                        return ExecuteAddPerson(intent, target, parseSource);
                    case GiaPhaIntentKinds.AddChildFamily:
                        return ExecuteAddChildFamily(target, parseSource);
                    case GiaPhaIntentKinds.AddSiblingEm:
                        return ExecuteAddSiblingEm(target, parseSource);
                    case GiaPhaIntentKinds.AddSiblingAnh:
                        return ExecuteAddSiblingAnh(target, parseSource);
                    case GiaPhaIntentKinds.RenamePerson:
                        return ExecuteRenamePerson(intent, target, engine, parseSource);
                    case GiaPhaIntentKinds.DeletePerson:
                        return ExecuteDeletePersonGuidance(target);
                    default:
                        return GiaPhaQueryResult.Fail("Intent sửa \"" + kind + "\" chưa được hỗ trợ.");
                }
            }
            catch (Exception ex)
            {
                return GiaPhaQueryResult.Fail("❌ Lỗi khi sửa gia phả: " + ex.Message);
            }
        }

        private static GiaPhaQueryResult ExecuteAddPerson(
            GiaPhaIntent intent,
            FamilyViewModel family,
            GiaPhaParseSource parseSource)
        {
            if (family?.familyInfo?.ListPerson == null || family.familyInfo.ListPerson.Count == 0)
            {
                return GiaPhaQueryResult.Fail("Gia đình chưa có người — hãy thêm gia đình hoặc chọn nhánh khác.");
            }

            string displayName = string.IsNullOrWhiteSpace(intent.NewPersonName)
                ? "Người mới"
                : intent.NewPersonName.Trim();

            var person = new PersonInfo(displayName, family.familyInfo);
            foreach (var item in family.familyInfo.ListPerson)
            {
                if (item.IsMainPerson == 1)
                {
                    person.MANS_GENDER = item.IsGioiTinhNam == 1 ? "Nữ" : "Nam";
                }
            }

            family.familyInfo.ListPerson.Add(person);
            family.AddUserAction("AI: Thêm [" + displayName + "] vào gia đình " + family.familyInfo.Name0);
            family.familyInfo.OnPropertyChanged("Name");

            string familyLabel = family.familyInfo.Name0 ?? ("GĐ #" + family.familyInfo.FamilyId);
            return OkEdit(
                "✅ Đã thêm người \"" + displayName + "\" vào gia đình " + familyLabel
                + ".\nHãy sửa thông tin chi tiết trên lưới hoặc Phả đồ.",
                intent,
                parseSource,
                family);
        }

        private static GiaPhaQueryResult ExecuteAddChildFamily(FamilyViewModel family, GiaPhaParseSource parseSource)
        {
            FamilyViewModel added = family.InsertNewChildFamilyFromTree();
            if (added == null)
            {
                return GiaPhaQueryResult.Fail("Không thêm được gia đình con.");
            }

            string label = added.familyInfo?.Name0 ?? "gia đình con mới";
            return OkEdit(
                "✅ Đã thêm gia đình con: " + label + " (đời " + added.familyInfo.FamilyLevel + ").",
                null,
                parseSource,
                added);
        }

        private static GiaPhaQueryResult ExecuteAddSiblingEm(FamilyViewModel family, GiaPhaParseSource parseSource)
        {
            if (family.Parent == null)
            {
                return GiaPhaQueryResult.Fail("Không thêm em được — gia đình gốc (thủy tổ) không có anh em.");
            }

            FamilyViewModel added = family.InsertNewSiblingEmFromTree();
            if (added == null)
            {
                return GiaPhaQueryResult.Fail("Không thêm được gia đình em.");
            }

            return OkEdit(
                "✅ Đã thêm gia đình em: " + (added.familyInfo?.Name0 ?? "mới") + ".",
                null,
                parseSource,
                added);
        }

        private static GiaPhaQueryResult ExecuteAddSiblingAnh(FamilyViewModel family, GiaPhaParseSource parseSource)
        {
            if (family.Parent == null)
            {
                return GiaPhaQueryResult.Fail("Không thêm anh được — gia đình gốc (thủy tổ) không có anh em.");
            }

            FamilyViewModel added = family.InsertNewSiblingAnhFromTree();
            if (added == null)
            {
                return GiaPhaQueryResult.Fail("Không thêm được gia đình anh.");
            }

            return OkEdit(
                "✅ Đã thêm gia đình anh: " + (added.familyInfo?.Name0 ?? "mới") + ".",
                null,
                parseSource,
                added);
        }

        private static GiaPhaQueryResult ExecuteRenamePerson(
            GiaPhaIntent intent,
            FamilyViewModel family,
            GiaPhaQueryEngine engine,
            GiaPhaParseSource parseSource)
        {
            if (string.IsNullOrWhiteSpace(intent.NewPersonName))
            {
                return GiaPhaQueryResult.Fail("Thiếu tên mới. Ví dụ: \"đổi tên Nguyễn Văn A thành Nguyễn Văn B\".");
            }

            string oldName = intent.Names != null && intent.Names.Count > 0
                ? intent.Names[0]
                : null;

            PersonInfo person = null;
            if (!string.IsNullOrWhiteSpace(oldName))
            {
                person = FindPersonInFamily(family, oldName, engine);
                if (person == null)
                {
                    return GiaPhaQueryResult.Fail(
                        "Không tìm thấy \"" + oldName + "\" trong gia đình "
                        + (family.familyInfo?.Name0 ?? "") + ".");
                }
            }
            else if (family.familyInfo?.ListPerson != null)
            {
                person = family.familyInfo.ListPerson.FirstOrDefault(p => p.IsMainPerson == 1)
                    ?? family.familyInfo.ListPerson.FirstOrDefault();
            }

            if (person == null)
            {
                return GiaPhaQueryResult.Fail("Không xác định được người cần đổi tên.");
            }

            string previous = person.MANS_NAME_HUY ?? "";
            string newName = intent.NewPersonName.Trim();
            person.MANS_NAME_HUY = newName;
            family.AddUserAction("AI: Đổi tên [" + previous + "] → [" + newName + "]");

            return OkEdit(
                "✅ Đã đổi tên \"" + previous + "\" → \"" + newName + "\".",
                intent,
                parseSource,
                family);
        }

        private static GiaPhaQueryResult ExecuteDeletePersonGuidance(FamilyViewModel family)
        {
            string label = family?.familyInfo?.Name0 ?? "đang chọn";
            return new GiaPhaQueryResult
            {
                Success = true,
                AnswerText = "⚠️ Xóa người cần xác nhận trên giao diện.\n"
                    + "Cách làm: chọn người trong danh sách gia đình \"" + label + "\" → phím Delete.\n"
                    + "Hoặc menu Người → Xóa người.",
                FactsPack = "",
                StatusHint = "Hướng dẫn xóa — chưa tự xóa qua chat",
                AffectedFamily = family
            };
        }

        private static GiaPhaQueryResult OkEdit(
            string message,
            GiaPhaIntent intent,
            GiaPhaParseSource parseSource,
            FamilyViewModel affected)
        {
            return new GiaPhaQueryResult
            {
                Success = true,
                AnswerText = message,
                FactsPack = message,
                Intent = intent,
                ParseSource = parseSource,
                StatusHint = "Đã sửa gia phả",
                AffectedFamily = affected
            };
        }

        private static bool TryResolveTargetFamily(
            GiaPhaIntent intent,
            FamilyViewModel currentFamily,
            GiaPhaQueryEngine engine,
            out FamilyViewModel target,
            out string error)
        {
            target = null;
            error = null;

            bool needsCurrent = intent.UseCurrentFamily
                || intent.Kind == GiaPhaIntentKinds.AddChildFamily
                || intent.Kind == GiaPhaIntentKinds.AddSiblingEm
                || intent.Kind == GiaPhaIntentKinds.AddSiblingAnh;

            if (needsCurrent || (intent.Names == null || intent.Names.Count == 0))
            {
                if (currentFamily?.familyInfo != null)
                {
                    target = currentFamily;
                    return true;
                }

                if (needsCurrent || GiaPhaIntentKinds.IsEditAction(intent.Kind))
                {
                    error = "⚠️ Chưa chọn gia đình trên cây.\n"
                        + "Hãy click chọn một ô gia đình (hoặc nói rõ tên người, vd. \"thêm người vào gia đình của Nguyễn Văn A\").";
                    return false;
                }
            }

            if (intent.Names != null && intent.Names.Count > 0 && engine != null)
            {
                List<FamilyViewModel> found = engine.FindFamiliesByPersonName(intent.Names[0]);
                if (found.Count == 0)
                {
                    error = "Không tìm thấy \"" + intent.Names[0] + "\" trong gia phả.";
                    return false;
                }

                if (found.Count > 1)
                {
                    error = "Tìm thấy nhiều gia đình có \"" + intent.Names[0]
                        + "\" — hãy chọn gia đình trên cây hoặc nói rõ hơn.";
                    return false;
                }

                target = found[0];
                return true;
            }

            error = "Không xác định được gia đình cần sửa.";
            return false;
        }

        private static PersonInfo FindPersonInFamily(
            FamilyViewModel family,
            string name,
            GiaPhaQueryEngine engine)
        {
            if (family?.familyInfo?.ListPerson == null || string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string norm = engine != null
                ? engine.NormalizeNameForMatch(name)
                : name.Trim().ToLowerInvariant();

            foreach (PersonInfo p in family.familyInfo.ListPerson)
            {
                if (PersonNameMatches(p, norm, engine))
                {
                    return p;
                }
            }

            return null;
        }

        private static bool PersonNameMatches(PersonInfo p, string normKeyword, GiaPhaQueryEngine engine)
        {
            if (p == null || string.IsNullOrWhiteSpace(normKeyword))
            {
                return false;
            }

            string huy = engine != null
                ? engine.NormalizeNameForMatch(p.MANS_NAME_HUY)
                : (p.MANS_NAME_HUY ?? "").ToLowerInvariant();
            string tu = engine != null
                ? engine.NormalizeNameForMatch(p.MANS_NAME_TU)
                : (p.MANS_NAME_TU ?? "").ToLowerInvariant();

            return (!string.IsNullOrEmpty(huy) && huy.Contains(normKeyword))
                || (!string.IsNullOrEmpty(tu) && tu.Contains(normKeyword));
        }
    }
}
