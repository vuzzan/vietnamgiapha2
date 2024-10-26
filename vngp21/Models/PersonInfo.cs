using System.ComponentModel;

namespace vietnamgiapha
{
    public class PersonInfo : INotifyPropertyChanged
    {
        private int _IsMainPerson;
        public int IsMainPerson {
            get { 
                return _IsMainPerson;
            }
            set {
                _IsMainPerson = value;
                if (_IsMainPerson == 1)
                {
                    // Only 1 isMainPerson
                    foreach( var person in _familyInfo.ListPerson)
                    {
                        if (person != this)
                        {
                            person.IsMainPerson = 0;
                        }
                    }
                }
                else
                {
                    // Only 1 isMainPerson
                    //foreach (var person in _familyInfo.ListPerson)
                    //{
                    //    if (person != this)
                    //    {
                    //        person.IsMainPerson = 1;
                    //        break;
                    //    }
                    //}
                }
                OnPropertyChanged("IsMainPerson");
                _familyInfo.OnPropertyChanged("");
            }
        }
        
        public FamilyInfo _familyInfo { get; set; }
        public string NAMEHUY
        {
            get
            {
                return (IsMainPerson==1?"*":"") + _MANS_NAME_HUY;
            }
            set
            {
                _MANS_NAME_HUY = value.Replace("*", "");
                OnPropertyChanged("MANS_NAME_HUY");
                _familyInfo.OnPropertyChanged("NAMEHUY");
            }
        }

        private string _MANS_NAME_HUY;
        public string MANS_NAME_HUY { 
            get {
                return _MANS_NAME_HUY;
            }
            set {
                _MANS_NAME_HUY = Util.RemoveSpecialChar(value);
                OnPropertyChanged("MANS_NAME_HUY");
                _familyInfo.OnPropertyChanged("");
            } 
        }

        private string _MANS_NAME_TU;
        public string MANS_NAME_TU
        {
            get
            {
                return _MANS_NAME_TU;
            }
            set
            {
                _MANS_NAME_TU = Util.RemoveSpecialChar(value);
            }
        }

        private string _MANS_NAME_THUONG;
        public string MANS_NAME_THUONG
        {
            get
            {
                return _MANS_NAME_THUONG;
            }
            set
            {
                _MANS_NAME_THUONG = Util.RemoveSpecialChar(value);
            }
        }
        private string _MANS_NAME_THUY;
        public string MANS_NAME_THUY
        {
            get
            {
                return _MANS_NAME_THUY;
            }
            set
            {
                _MANS_NAME_THUY = Util.RemoveSpecialChar(value);
            }
        }
        public string MANS_ID { get; set; }
        public string fid { get; set; }
        private string _GENDER;
        public string MANS_GENDER
        {
            get
            {
                return _GENDER;
            }
            set
            {
                _GENDER = value;
                OnPropertyChanged(nameof(MANS_GENDER));
            }
        }

        public int IsGioiTinhNam { 
            get {
                return MANS_GENDER == "Nam" ? 1 : 0;
            } 
            set
            {
                MANS_GENDER = value == 1 ? "Nam" : "Nữ";
                OnPropertyChanged(nameof(MANS_GENDER));
            }
        }

        private string _MANS_DOB;
        public string MANS_DOB
        {
            get
            {
                return _MANS_DOB;
            }
            set
            {
                _MANS_DOB = Util.RemoveSpecialChar(value);
            }
        }
        private string _MANS_DOD;
        public string MANS_DOD
        {
            get
            {
                return _MANS_DOD;
            }
            set
            {
                _MANS_DOD = Util.RemoveSpecialChar(value);
            }
        }
        private string _MANS_WOD;
        public string MANS_WOD
        {
            get
            {
                return _MANS_WOD;
            }
            set
            {
                _MANS_WOD = Util.RemoveSpecialChar(value);
            }
        }
        public string MANS_DETAIL { get; set; }
        public string MANS_CONTHUMAY { get; set; }
        
        public PersonInfo(string HUY, FamilyInfo familyInfo)
        {
            _familyInfo = familyInfo;
            _MANS_NAME_HUY = HUY;
            MANS_NAME_TU = "";
            MANS_NAME_THUONG = "";
            MANS_NAME_THUY = "";
            MANS_ID = "";
            fid = "";
            MANS_GENDER = "Nam";
            MANS_DOB = "";
            MANS_DOD = "";
            MANS_WOD = "";
            MANS_DETAIL = "";
            MANS_CONTHUMAY = ""+familyInfo.FamilyOrder;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public string ToJson()
        {
            string json = "[";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_NAME_HUY) + "\",";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_NAME_TU) + "\",";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_NAME_THUONG) + "\",";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_NAME_THUY) + "\",";
            json += "\"" + this.MANS_ID + "\",";
            json += "\"" + this.fid + "\",";
            json += "\"" + (this.MANS_GENDER== "Nam"?1:0) + "\",";//6
            json += "\"" + Util.RemoveSpecialChar(this.MANS_DOB) + "\",";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_DOD) + "\",";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_WOD) + "\",";
            json += "\"" + vietnamgiapha.Util.Base64Encode(this.MANS_DETAIL) + "\",";
            json += "\"" + Util.RemoveSpecialChar(this.MANS_CONTHUMAY) + "\" ";
            // Print end of family array
            json += "]";
            return json;
        }
    }
}
