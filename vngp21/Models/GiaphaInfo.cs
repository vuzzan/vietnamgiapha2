using System;
using System.Collections.Generic;
using System.ComponentModel;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    public class GiaphaInfo : INotifyPropertyChanged
    {
        public const string DefaultNewGiaPhaSlogan =
            "Đừng quên rằng các con cùng một mẹ sinh ra";

        public const string DefaultNewGiaPhaLocation = "Việt Nam";

        public int GiaphaId { get; set; }
        public String Username { get; set; }
        public String Password { get; set; }
        public String GiaphaName { get; set; }
        public String GiaphaNameRoot { get; set; }
        public String PhaKy { get; set; }
        public String Tocuoc { get; set; }
        public String ThuyTo { get; set; }
        public String HuongHoa { get; set; }
        public String RF_OTAI { get; set; }
        public String RF_DAYS { get; set; }
        public String RF_CHANNGON { get; set; }
        public FamilyInfo familyRoot { get; set; }

        /// <summary>Catalog svgId → SVG Base64 + viewBox (lưu ở index 12 file .json).</summary>
        public Dictionary<string, PhaDoSvgShape> SvgShapesById { get; set; }
            = new Dictionary<string, PhaDoSvgShape>(StringComparer.Ordinal);

        private DateTime _FileNameUpdate = DateTime.Now;
        public DateTime FileNameUpdate
        {
            get
            {
                return _FileNameUpdate;
            }
            set
            {
                _FileNameUpdate = value;
                OnPropertyChanged(nameof(FileNameUpdate));
            }
        }

        private string _FileName = "";
        public String FileName { get { 
                return _FileName;
            } 
            set {
                _FileName = value;
                //_FileName = _FileName.Replace(" ", "").Replace("-", "");
                OnPropertyChanged(nameof(FileName));
            } 
        }
        public GiaphaInfo()
        {
            FileName = "";
            GiaphaId = 0;
            Username = "";
            Password = "";
            GiaphaName = "";
            GiaphaNameRoot = "";
            PhaKy = "";
            Tocuoc = "";
            ThuyTo = "";
            HuongHoa = "";
            RF_OTAI = "";
            RF_DAYS = "";
            RF_CHANNGON = "";
            familyRoot = new FamilyInfo();
            familyRoot.FamilyId = 1;
            familyRoot.FamilyUp = 0;
            familyRoot.FamilyLevel = 1;
            familyRoot.FamilyOrder = 1;
            familyRoot.FamilyNew = 1;
            var thuyTo = new PersonInfo("Thủy tổ", familyRoot);
            thuyTo.IsMainPerson = 1;
            familyRoot.ListPerson.Add(thuyTo);
            ApplyNewGiaPhaDocumentDefaults();
        }

        /// <summary>Gán tên gia phả theo họ thủy tổ (nam), slogan và ở tại mặc định.</summary>
        public void ApplyNewGiaPhaDocumentDefaults()
        {
            RF_OTAI = DefaultNewGiaPhaLocation;
            RF_CHANNGON = DefaultNewGiaPhaSlogan;

            var rootMainMale = GetRootMainMalePerson();
            if (rootMainMale != null)
            {
                rootMainMale.IsMainPerson = 1;
                if (string.IsNullOrWhiteSpace(rootMainMale.MANS_GENDER))
                {
                    rootMainMale.MANS_GENDER = "Nam";
                }
            }

            SyncGiaphaNameFromRootThuyTo();
            OnPropertyChanged(nameof(RF_OTAI));
            OnPropertyChanged(nameof(RF_CHANNGON));
        }

        /// <summary>Cập nhật GiaphaName / GiaphaNameRoot từ họ người chính nam ở gốc cây.</summary>
        public void SyncGiaphaNameFromRootThuyTo()
        {
            var rootMainMale = GetRootMainMalePerson();
            if (rootMainMale == null)
            {
                return;
            }

            string ho = Util.GetFirstWord(rootMainMale.MANS_NAME_HUY ?? "");
            if (string.IsNullOrWhiteSpace(ho))
            {
                return;
            }

            GiaphaNameRoot = ho.Trim();
            GiaphaName = ho.Trim().ToUpperInvariant();
            OnPropertyChanged(nameof(GiaphaName));
            OnPropertyChanged(nameof(GiaphaNameRoot));
        }

        /// <summary>Người chính nam ở gia đình gốc (thủy tổ).</summary>
        private PersonInfo GetRootMainMalePerson()
        {
            if (familyRoot?.ListPerson == null || familyRoot.ListPerson.Count == 0)
            {
                return null;
            }

            foreach (var person in familyRoot.ListPerson)
            {
                if (person.IsMainPerson == 1 && person.IsGioiTinhNam == 1)
                {
                    return person;
                }
            }

            foreach (var person in familyRoot.ListPerson)
            {
                if (person.IsGioiTinhNam == 1)
                {
                    return person;
                }
            }

            return familyRoot.ListPerson[0];
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
