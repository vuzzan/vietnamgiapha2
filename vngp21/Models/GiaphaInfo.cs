using System;
using System.ComponentModel;

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
}
