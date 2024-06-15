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
                OnPropertyChanged(nameof(FileName));
            } }
        public GiaphaInfo()
        {
            FileName = "";
            GiaphaId = 0;
            Username = "";
            Password = "";
            GiaphaName = "Gia phả mẫu";
            GiaphaNameRoot = "Gia phả";
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
        public int IsMainPerson { get; set; }
        
        public FamilyInfo _familyInfo { get; set; }

        private string _MANS_NAME_HUY;
        public string MANS_NAME_HUY { 
            get {
                return _MANS_NAME_HUY;
            }
            set {
                _MANS_NAME_HUY = value;
                OnPropertyChanged("MANS_NAME_HUY");
                _familyInfo.OnPropertyChanged("");
            } 
        }
        public string MANS_NAME_TU { get; set; }
        public string MANS_NAME_THUONG { get; set; }
        public string MANS_NAME_THUY { get; set; }
        public string MANS_ID { get; set; }
        public string fid { get; set; }
        public string MANS_GENDER { get; set; }
        public string MANS_DOB { get; set; }
        public string MANS_DOD { get; set; }
        public string MANS_WOD { get; set; }
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
            MANS_GENDER = "";
            MANS_DOB = "";
            MANS_DOD = "";
            MANS_WOD = "";
            MANS_DETAIL = "";
            MANS_CONTHUMAY = "";
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
            json += "\"" + this.MANS_NAME_HUY + "\",";
            json += "\"" + this.MANS_NAME_TU + "\",";
            json += "\"" + this.MANS_NAME_THUONG + "\",";
            json += "\"" + this.MANS_NAME_THUY + "\",";
            json += "\"" + this.MANS_ID + "\",";
            json += "\"" + this.fid + "\",";
            //familyMember.MANS_GENDER = Convert.ToInt16(personInfoArray[6].ToString()) == 1 ? "Nam" : "Nữ";
            json += "\"" + (this.MANS_GENDER== "Nam"?1:0) + "\",";
            //json += "" + this.MANS_GENDER== "Nam"?1:0 + ",";
            json += "\"" + this.MANS_DOB + "\",";
            json += "\"" + this.MANS_DOD + "\",";
            json += "\"" + this.MANS_WOD + "\",";
            json += "\"" + vietnamgiapha.Util.Base64Encode(this.MANS_DETAIL) + "\",";
            json += "\"" + this.MANS_CONTHUMAY + "\" ";
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
                        tmp += item.MANS_NAME_HUY + " + ";
                        break;
                    }
                }
                foreach (var item in _listPerson)
                {
                    if (item.IsMainPerson == 0)
                    {
                        tmp += item.MANS_NAME_HUY + " + ";
                    }
                }
                return tmp.Substring(0, tmp.Length-2);
            }
        }

        public string Name0
        {
            get
            {
                String tmp = "";
                if (_listPerson.Count >= 1)
                {
                    tmp += _listPerson[0].MANS_NAME_HUY + " + ";
                    return tmp.Substring(0, tmp.Length - 2);
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

    }
}
