using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vietnamgiapha
{
    public class GiaphaInfo : INotifyPropertyChanged
    {
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
        private string _FileName = "";
        public String FileName { get { 
                return _FileName;
            } set {
                _FileName = value;
                _FileName = _FileName.Replace(" ", "").Replace("-", "");
                OnPropertyChanged(nameof(FileName));
            } }
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
            familyRoot.ListPerson.Add(new PersonInfo("Thủy tổ", familyRoot));
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
        public string MANS_GENDER { get; set; }

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

    public class FamilyInfo : INotifyPropertyChanged
    {
        public int FamilyId { get; set; }
        public int FamilyUp { get; set; }
        public int FamilyOrder { get; set; }
        public int FamilyLevel { get; set; }
        public int FamilyNew { get; set; }
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
