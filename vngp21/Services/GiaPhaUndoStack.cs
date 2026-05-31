using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace vietnamgiapha
{
    /// <summary>Sao chép sâu một nhánh gia đình (Ctrl+C / Ctrl+V).</summary>
    public static class FamilyBranchCloneHelper
    {
        public static FamilyInfo CloneFamilyBranch(FamilyInfo source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = new FamilyInfo
            {
                FamilyId = source.FamilyId,
                FamilyUp = source.FamilyUp,
                FamilyOrder = source.FamilyOrder,
                FamilyLevel = source.FamilyLevel,
                FamilyNew = source.FamilyNew,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                PhaDoShapeSvgId = source.PhaDoShapeSvgId,
                ListPerson = new ObservableCollection<PersonInfo>(),
                FamilyChildren = new ObservableCollection<FamilyInfo>()
            };

            if (source.ListPerson != null)
            {
                foreach (var p in source.ListPerson)
                {
                    clone.ListPerson.Add(ClonePerson(p, clone));
                }
            }

            if (source.FamilyChildren != null)
            {
                foreach (var child in source.FamilyChildren)
                {
                    var childClone = CloneFamilyBranch(child);
                    if (childClone != null)
                    {
                        clone.FamilyChildren.Add(childClone);
                    }
                }
            }

            return clone;
        }

        public static void RemapFamilyIds(FamilyInfo root, ref int nextId)
        {
            if (root == null)
            {
                return;
            }

            root.FamilyId = nextId++;
            foreach (var child in root.FamilyChildren ?? new ObservableCollection<FamilyInfo>())
            {
                child.FamilyUp = root.FamilyId;
                RemapFamilyIds(child, ref nextId);
            }
        }

        private static PersonInfo ClonePerson(PersonInfo source, FamilyInfo owner)
        {
            var p = new PersonInfo(source.MANS_NAME_HUY ?? "", owner)
            {
                MANS_NAME_TU = source.MANS_NAME_TU ?? "",
                MANS_NAME_THUONG = source.MANS_NAME_THUONG ?? "",
                MANS_NAME_THUY = source.MANS_NAME_THUY ?? "",
                MANS_ID = source.MANS_ID ?? "",
                fid = source.fid ?? "",
                MANS_GENDER = string.IsNullOrWhiteSpace(source.MANS_GENDER) ? "Nam" : source.MANS_GENDER,
                MANS_DOB = source.MANS_DOB ?? "",
                MANS_DOD = source.MANS_DOD ?? "",
                MANS_WOD = source.MANS_WOD ?? "",
                MANS_DETAIL = source.MANS_DETAIL ?? "",
                MANS_CONTHUMAY = source.MANS_CONTHUMAY ?? "",
                IsMainPerson = source.IsMainPerson
            };
            return p;
        }
    }

    /// <summary>Sao chép cây gia phả trong bộ nhớ (cùng định dạng JSON file, không ghi đĩa).</summary>
    public static class GiaPhaCloneHelper
    {
        public static GiaphaInfo CloneFromTree(GiaPhaViewModel tree)
        {
            if (tree?.GP == null)
            {
                return null;
            }

            try
            {
                string json = tree.ToJson();
                string wrapped = "{\"code\":0,\"msg\":\" \", \"data\":" + json + "}";
                var jsonObject = (JsonObject)JsonObject.Parse(wrapped);
                var clone = Database.ParseJsonGiaPha(jsonObject);
                if (clone != null)
                {
                    clone.FileName = tree.GP.FileName;
                }

                return clone;
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class GiaPhaUndoEntry
    {
        public GiaphaInfo Snapshot { get; set; }
        public string Label { get; set; }
    }

    /// <summary>Hoàn tác tối đa 2 bước — lưu trạng thái cây ngay trước mỗi thao tác sửa.</summary>
    public sealed class GiaPhaUndoStack
    {
        public const int MaxSteps = 2;
        private readonly List<GiaPhaUndoEntry> _entries = new List<GiaPhaUndoEntry>();

        public int Count => _entries.Count;

        public void Push(GiaPhaViewModel tree, string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            var snap = GiaPhaCloneHelper.CloneFromTree(tree);
            if (snap == null)
            {
                return;
            }

            _entries.Insert(0, new GiaPhaUndoEntry { Snapshot = snap, Label = label.Trim() });
            while (_entries.Count > MaxSteps)
            {
                _entries.RemoveAt(_entries.Count - 1);
            }
        }

        public GiaPhaUndoEntry TryPop()
        {
            if (_entries.Count == 0)
            {
                return null;
            }

            var entry = _entries[0];
            _entries.RemoveAt(0);
            return entry;
        }

        public void Clear()
        {
            _entries.Clear();
        }
    }
}
