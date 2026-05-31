using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vietnamgiapha
{

    public class FamilyInfo : INotifyPropertyChanged
    {
        public int FamilyId { get; set; }
        public int FamilyUp { get; set; }
        public int FamilyOrder { get; set; }
        public int FamilyLevel { get; set; }
        public int FamilyNew { get; set; }

        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>Tham chiếu khung SVG trong catalog file gia phả (index root 12).</summary>
        public string PhaDoShapeSvgId { get; set; }

        public FamilyInfo()
        {
            FamilyNew = 1;
        }
        // LIST CHILD FAMILY IN FAMILY
        ObservableCollection<FamilyInfo> _familyChildren = new ObservableCollection<FamilyInfo>();
        public ObservableCollection<FamilyInfo> FamilyChildren
        {
            get { 
                return _familyChildren; 
            }
            set { 
                _familyChildren = value; 
            }
        }
        // LIST PERSON IN FAMILY
        private ObservableCollection<PersonInfo> _listPerson = new ObservableCollection<PersonInfo>();
        public ObservableCollection<PersonInfo> ListPerson
        {
            get { 
                return _listPerson; 
            }
            set { 
                _listPerson = value; 
            }
        }
        // NAME OF FAMILY
        public string Name { 
            get
            {
                String tmp = FamilyLevel+". ";
                foreach( var item in _listPerson)
                {
                    if (item.IsMainPerson == 1)
                    {
                        tmp += Util.RemoveSpecialChar(item.MANS_NAME_HUY) + " + ";
                        break;
                    }
                }
                foreach (var item in _listPerson)
                {
                    if (item.IsMainPerson == 0)
                    {
                        tmp += Util.RemoveSpecialChar(item.MANS_NAME_HUY) + " + ";
                    }
                }

                tmp = tmp.Substring(0, tmp.Length-2);
                if( FamilyNew == 1)
                {
                    tmp += "*";
                }
                return tmp;
            }
        }

        public string Name0
        {
            get
            {
                String tmp = "";
                if (_listPerson.Count >= 1)
                {
                    tmp += Util.RemoveSpecialChar(_listPerson[0].MANS_NAME_HUY) + " + ";
                    tmp = tmp.Substring(0, tmp.Length - 2);
                    if (FamilyNew == 1)
                    {
                        //tmp += "*";
                    }
                    return tmp;
                }
                else if (_listPerson.Count == 0)
                {
                }
                return "No";
            }
        }

        /// <summary>Phần tên trên cây khi F2 — không gồm số đời và dấu *.</summary>
        public string GetTreeEditNameText()
        {
            var parts = GetPersonsInTreeNameOrder()
                .Select(p => Util.RemoveSpecialChar(p.MANS_NAME_HUY ?? "").Trim())
                .Where(s => s.Length > 0)
                .ToList();
            return parts.Count == 0 ? "" : string.Join(" + ", parts);
        }

        /// <summary>Ghi tên sau Enter; nhãn hiển thị vẫn tự thêm đời (FamilyLevel).</summary>
        public bool ApplyTreeEditNameText(string editText)
        {
            if (editText == null)
            {
                return false;
            }

            string text = editText.Trim();
            if (text.EndsWith("*", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            var names = text.Split(new[] { " + " }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            if (names.Count == 0)
            {
                return false;
            }

            var ordered = GetPersonsInTreeNameOrder();
            if (ordered.Count == 0)
            {
                var main = new PersonInfo(names[0], this) { IsMainPerson = 1 };
                _listPerson.Add(main);
                for (int i = 1; i < names.Count; i++)
                {
                    _listPerson.Add(new PersonInfo(names[i], this));
                }
            }
            else
            {
                int i;
                for (i = 0; i < ordered.Count && i < names.Count; i++)
                {
                    ordered[i].MANS_NAME_HUY = names[i];
                }

                for (; i < names.Count; i++)
                {
                    _listPerson.Add(new PersonInfo(names[i], this));
                }
            }

            OnPropertyChanged("Name");
            OnPropertyChanged("Name0");
            return true;
        }

        /// <summary>Tách tên từ chuỗi F2 (không đời, không dấu *).</summary>
        public static List<string> ParseTreeEditNames(string editText)
        {
            if (string.IsNullOrWhiteSpace(editText))
            {
                return new List<string>();
            }

            string text = editText.Trim();
            if (text.EndsWith("*", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            return text.Split(new[] { " + " }, StringSplitOptions.None)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        /// <summary>Sau F2: người đầu cùng họ → con trong tộc; các người sau giới tính ngược main.</summary>
        public void ApplyClanMembershipAfterNameEdit(string clanSurname, string editTextAfterCommit)
        {
            if (_listPerson == null || _listPerson.Count == 0)
            {
                return;
            }

            var namesInEditOrder = ParseTreeEditNames(editTextAfterCommit);
            if (namesInEditOrder.Count == 0)
            {
                return;
            }

            var orderedPersons = MatchPersonsToEditNames(namesInEditOrder);
            if (orderedPersons.Count == 0)
            {
                return;
            }

            ApplyGenderHintFromName(orderedPersons[0]);

            PersonInfo mainPerson = null;
            var first = orderedPersons[0];
            if (!string.IsNullOrWhiteSpace(clanSurname)
                && Util.SameClanSurname(first.MANS_NAME_HUY, clanSurname))
            {
                first.IsMainPerson = 1;
                mainPerson = first;
                if (IsGenderUnset(first))
                {
                    first.MANS_GENDER = "Nam";
                }
            }
            else
            {
                first.IsMainPerson = 0;
            }

            for (int i = 1; i < orderedPersons.Count; i++)
            {
                orderedPersons[i].IsMainPerson = 0;
            }

            // Giới tính người sau = ngược người chính (hoặc người đầu nếu không có con trong tộc)
            var refPerson = mainPerson ?? first;
            if (IsGenderUnset(refPerson))
            {
                refPerson.MANS_GENDER = "Nam";
            }

            string genderFollowers = refPerson.IsGioiTinhNam == 1 ? "Nữ" : "Nam";
            for (int i = 1; i < orderedPersons.Count; i++)
            {
                orderedPersons[i].MANS_GENDER = genderFollowers;
            }

            // Sắp xếp lại: con trong tộc đầu, thứ tự như lúc F2
            _listPerson.Clear();
            if (mainPerson != null)
            {
                _listPerson.Add(mainPerson);
                foreach (var p in orderedPersons)
                {
                    if (!ReferenceEquals(p, mainPerson))
                    {
                        _listPerson.Add(p);
                    }
                }
            }
            else
            {
                foreach (var p in orderedPersons)
                {
                    _listPerson.Add(p);
                }
            }

            OnPropertyChanged("Name");
            OnPropertyChanged("Name0");
        }

        private List<PersonInfo> MatchPersonsToEditNames(List<string> namesInEditOrder)
        {
            var ordered = new List<PersonInfo>();
            var used = new HashSet<PersonInfo>();
            foreach (var name in namesInEditOrder)
            {
                var person = _listPerson.FirstOrDefault(p =>
                    !used.Contains(p)
                    && string.Equals(
                        Util.RemoveSpecialChar(p.MANS_NAME_HUY ?? ""),
                        name,
                        StringComparison.OrdinalIgnoreCase));
                if (person == null)
                {
                    person = _listPerson.FirstOrDefault(p => !used.Contains(p));
                }

                if (person != null)
                {
                    ordered.Add(person);
                    used.Add(person);
                }
            }

            foreach (var p in _listPerson)
            {
                if (!used.Contains(p))
                {
                    ordered.Add(p);
                }
            }

            return ordered;
        }

        private static void ApplyGenderHintFromName(PersonInfo person)
        {
            if (person == null || string.IsNullOrWhiteSpace(person.MANS_NAME_HUY))
            {
                return;
            }

            string upper = person.MANS_NAME_HUY.ToUpperInvariant();
            if (upper.Contains(" VĂN "))
            {
                person.MANS_GENDER = "Nam";
            }
            else if (upper.Contains(" THỊ "))
            {
                person.MANS_GENDER = "Nữ";
            }
        }

        private static bool IsGenderUnset(PersonInfo person)
        {
            if (person == null)
            {
                return true;
            }

            string g = (person.MANS_GENDER ?? "").Trim();
            return g.Length == 0 || (g != "Nam" && g != "Nữ");
        }

        private List<PersonInfo> GetPersonsInTreeNameOrder()
        {
            var list = new List<PersonInfo>();
            foreach (var item in _listPerson)
            {
                if (item.IsMainPerson == 1)
                {
                    list.Add(item);
                }
            }

            foreach (var item in _listPerson)
            {
                if (item.IsMainPerson == 0)
                {
                    list.Add(item);
                }
            }

            if (list.Count == 0 && _listPerson.Count > 0)
            {
                list.Add(_listPerson[0]);
            }

            return list;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public int GetMaxFamilyId(int maxFamilyId)
        {
            if (maxFamilyId < FamilyId)
            {
                maxFamilyId = FamilyId;
            }
            foreach(var f in _familyChildren)
            {
                maxFamilyId = f.GetMaxFamilyId(maxFamilyId);
            }
            return maxFamilyId;
        }
    }
}
