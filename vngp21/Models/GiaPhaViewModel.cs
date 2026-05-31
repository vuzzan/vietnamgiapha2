using ControlzEx.Theming;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Media;
using vietnamgiapha.GiaPhaRender;

namespace vietnamgiapha
{
    public class GiaPhaViewModel : INotifyPropertyChanged
    {
        private GiaphaInfo gp;
        private FamilyTreeViewModel _family;

        private FamilyViewModel _familyCut;
        public FamilyViewModel FamilyCut
        {
            get { return _familyCut; }
            set { _familyCut = value; }
        }

        /// <summary>Nhánh đã copy (Ctrl+C) — bản sao FamilyInfo tách khỏi cây.</summary>
        public FamilyInfo FamilyCopyBranch { get; set; }

        /// <summary>Node gốc lúc copy — để không dán vào chính nhánh đó.</summary>
        public FamilyViewModel FamilyCopySource { get; set; }
        
        private ObservableCollection<string> _listStringUserAction;
        public ObservableCollection<string> listStringUserAction
        {
            get
            {
                if(_listStringUserAction== null)
                {
                    _listStringUserAction = new ObservableCollection<string>();
                }
                return this._listStringUserAction;
            }
        }
        public void AddUserAction(string action)
        {
            listStringUserAction.Add(action);
            this.OnPropertyChanged("listStringUserAction");
        }

        /// <summary>Gọi từ MainWindow trước thao tác sửa cây — lưu snapshot hoàn tác.</summary>
        public Action<string> RequestUndoSnapshot;

        public void CaptureUndoSnapshot(string label)
        {
            RequestUndoSnapshot?.Invoke(label);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public String GiaphaNameRoot
        {
            get
            {
                return gp.GiaphaNameRoot;
            }
            set
            {
                gp.GiaphaNameRoot = value;
            }
        }
        private string _GiaphaWebHtml;
        public String GiaphaWebHtml
        {
            get
            {
                return _GiaphaWebHtml;
            }
            set
            {
                _GiaphaWebHtml = value;
            }
        }

        private string _GiaphaDrawIo;
        public String GiaphaDrawIo
        {
            get
            {
                return _GiaphaDrawIo;
            }
            set
            {
                _GiaphaDrawIo = value;
            }
        }
        public String GiaphaName
        {
            get
            {
                return gp.GiaphaName;
            }
            set
            {
                gp.GiaphaName = value;
            }
        }
        public String Password
        {
            get
            {
                return gp.Password;
            }
            set
            {
                string v = value;
                if (Util.HasNonASCIIChars(v)==false){
                    gp.Password = v;
                }
                else
                {
                    gp.Password = "";
                }
            }
        }
        public String Username
        {
            get
            {
                return gp.Username;
            }
            set
            {
                gp.Username = value;
            }
        }
        public GiaphaInfo GP
        {
            get
            {
                return gp;
            }
            set
            {
                gp = value;
                this.OnPropertyChanged("GP");
            }
        }
        public int GiaphaId
        {
            get
            {
                return gp.GiaphaId;
            }
            set
            {
                gp.GiaphaId = value;
            }
        }
        public String PhaKy
        {
            get{
                if (gp.PhaKy == null)
                {
                    gp.PhaKy = "";
                }
                return gp.PhaKy.Replace("\n", "\r\n");
            }
            set {
                gp.PhaKy = value;
            }
        }
        public String Tocuoc
        {
            get
            {
                if (gp.Tocuoc == null)
                {
                    gp.Tocuoc = "";
                }
                return gp.Tocuoc;
            }
            set
            {
                gp.Tocuoc = value;
            }
        }
        public String ThuyTo
        {
            get
            {
                if (gp.ThuyTo == null)
                {
                    gp.ThuyTo = "";
                }
                return gp.ThuyTo;
            }
            set
            {
                gp.ThuyTo = value;
            }
        }
        public String HuongHoa
        {
            get
            {
                if (gp.HuongHoa == null)
                {
                    gp.HuongHoa = "";
                }
                return gp.HuongHoa;
            }
            set
            {
                gp.HuongHoa = value;
            }
        }
        public FamilyTreeViewModel Family
        {
            get { 
                return _family; 
            }
            set
            {
                _family = value;
                this.OnPropertyChanged("Family");
            }
        }

        public FamilyViewModel FamilyViewModelRoot {
            get
            {
                return _family.RootPerson;
            }
        }

        public GiaPhaViewModel(GiaphaInfo gp)
        {
            if(gp == null)
            {
                // Default gia phả
                gp = new GiaphaInfo();
            }
            GP = gp;
            _family = new FamilyTreeViewModel(gp.familyRoot, this);
            _GiaphaWebHtml = "";
            // Không ExpandAll ở đây — file lớn sẽ materialize toàn bộ TreeView và khóa STA.
        }

        /// <summary>Đồng bộ tên gia phả trên form/tab sau khi sửa thủy tổ.</summary>
        public void SyncGiaphaNameFromRootThuyTo()
        {
            GP?.SyncGiaphaNameFromRootThuyTo();
            OnPropertyChanged(nameof(GiaphaName));
            OnPropertyChanged(nameof(GiaphaNameRoot));
        }

        public virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        
        public string ToJson()
        {
            // Catalog SVG giữ toàn bộ khung đã lưu (thư viện), không prune theo family.

            // Save vào json file
            String json = "[";
            // Build Json Gia phả
            json += "" + GiaphaId.ToString() + ", ";             //0
            json += "\"" + GiaphaName.ToString() + "\", ";       //1
                                                                 // Build Famile Tree String 
            json += "" + Family.ToJson() + ", ";             //2
            json += "\"" + Util.Base64Encode(PhaKy) + "\", ";         //3
            json += "\"" + Util.Base64Encode(ThuyTo) + "\", ";        //4
            json += "\"" + Util.Base64Encode(Tocuoc) + "\", ";        //5
            json += "\"" + Util.Base64Encode(HuongHoa) + "\", ";      //6
            json += "\"" + (GP.RF_OTAI) + "\", ";                   //7
            json += "\"" + (GP.RF_DAYS) + "\", ";                   //8
            json += "\"" + (GP.RF_CHANNGON) + "\", ";                //9
            json += "\"" + (GP.Username) + "\", ";                  //10
            json += "\"" + (GP.Password) + "\", ";                  //11
            json += PhaDoSvgCatalog.ToJsonArray(GP?.SvgShapesById);  //12 catalog SVG
            json += "]";// END 
            //
            return json;
        }

        public FamilyInfo FindFamilyInfoById(int familyId)
        {
            return FindFamilyInfoRecursive(GP?.familyRoot, familyId);
        }

        private static FamilyInfo FindFamilyInfoRecursive(FamilyInfo node, int familyId)
        {
            if (node == null || familyId <= 0)
            {
                return null;
            }

            if (node.FamilyId == familyId)
            {
                return node;
            }

            if (node.FamilyChildren == null)
            {
                return null;
            }

            foreach (var child in node.FamilyChildren)
            {
                var found = FindFamilyInfoRecursive(child, familyId);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        public string CheckValid()
        {
            string check = "";
            if( GP.GiaphaName.Trim().Length==0)
            {
                check += "Tên Gia Phả phải có. " + Environment.NewLine;
            }
            if (GP.RF_OTAI.Trim().Length == 0)
            {
                check += "Gia phả ở tại đâu phải có. " + Environment.NewLine;
            }
            if (GP.RF_DAYS.Trim().Length == 0)
            {
                check += "Ngày hội mả, cúng tế phải có. " + Environment.NewLine;
            }
            if (GP.RF_CHANNGON.Trim().Length == 0)
            {
                check += "Slogan phải có. Ví dụ: Cây có cội chi chi đó. " + Environment.NewLine;
            }
            string errorMessage = "";
            if( FamilyViewModel.CheckValid(this.Family.RootPerson, ref errorMessage) ==false)
            {
                check += errorMessage;
            }

            return check;
        }
    }
}