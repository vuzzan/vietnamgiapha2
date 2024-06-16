using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace vietnamgiapha
{
    /// <summary>
    /// A UI-friendly wrapper around a Person object.
    /// </summary>
    public class FamilyViewModel :  INotifyPropertyChanged
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");

        #region Data

        private ObservableCollection<FamilyViewModel> _familyListChildren;
        FamilyViewModel _familyParent;
        private FamilyInfo _familyInfo;

        bool _isExpanded;
        bool _isSelected;

        private GiaPhaViewModel _objFamilyTree;
        //private FamilyInfo rootPerson;
        //private object value;
        #endregion // Data

        #region Constructors
        public FamilyViewModel(FamilyInfo familyInfo, FamilyViewModel parent, GiaPhaViewModel objFamilyTree)
        {
            _familyInfo = familyInfo;
            _familyInfo.PropertyChanged += _familyInfo_PropertyChanged;
            _familyParent = parent;
            this._objFamilyTree = objFamilyTree;
            _familyListChildren = new ObservableCollection<FamilyViewModel>(
                    (from child in _familyInfo.FamilyChildren
                     select new FamilyViewModel(child, this, _objFamilyTree))
                     .ToList<FamilyViewModel>());

            // MENU FUNCTION
            DebugFamilyClick = new RelayCommand(DebugFamilyClickFunc);
            CutFamilyClick = new RelayCommand(CutFamilyClickFunc);
            PasteFamilyClick = new RelayCommand(PasteFamilyClickFunc);
            InsertFamilyClick = new RelayCommand(InsertFamilyClickFunc);
            InsertFamilyAnhClick = new RelayCommand(InsertFamilyAnhClickFunc);
            InsertFamilyEmClick = new RelayCommand(InsertFamilyEmClickFunc);
            InsertFamilyConClick = new RelayCommand(InsertFamilyConClickFunc);
            RemoveFamilyClick = new RelayCommand(RemoveFamilyClickFunc);
            RemoveFamilyOnlyClick = new RelayCommand(RemoveFamilyOnlyClickFunc);
            InsertPerson2FamilyClick = new RelayCommand(InsertPerson2FamilyClickFunc);
        }

        private void _familyInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Callback to here
            OnPropertyChanged("Name");
        }

        // MENU FUNCTION
        public ICommand DebugFamilyClick { get; set; }
        public ICommand CutFamilyClick { get; set; }
        public ICommand PasteFamilyClick { get; set; }
        public ICommand InsertFamilyClick { get; set; }
        public ICommand InsertFamilyAnhClick { get; set; }
        public ICommand InsertFamilyEmClick { get; set; }
        public ICommand InsertFamilyConClick { get; set; }
        public ICommand RemoveFamilyClick { get; set; }
        public ICommand RemoveFamilyOnlyClick { get; set; }
        public ICommand InsertPerson2FamilyClick { get; set; }

        // MENU FUNCTION
        private void DebugFamilyClickFunc()
        {
            string text = "Gia đình - " + this.Name0 + " | ID=[" + this._familyInfo.FamilyId +"]" + Environment.NewLine;
            text += "    --- " + Environment.NewLine;
            if (this.Parent != null)
            {
                text += "    Gia đình cha - " + this.Parent.Name0 + Environment.NewLine;
            }
            else
            {
                text += "    Gia đình cha - ROOT" + Environment.NewLine;
            }
            text += "    --- " + Environment.NewLine;
            if ( this.Children.Count > 0)
            {
                foreach( var child in this.Children)
                {
                    text += "    Gia đình con - " + child.Name0 + Environment.NewLine;
                }
            }
            else
            {
                text += "    Không con " + Environment.NewLine;
            }
            
            MessageBox.Show(
                text
                );
        }
        private void CutFamilyClickFunc()
        {
            _objFamilyTree.FamilyCut = this;
            // Lấy gia đình dán, lấy cha
            int index = this.Parent.Children.IndexOf(this);
            this.Parent.Children.Remove(this);
            //
            //MessageBox.Show("Đã chọn cắt nguyên nhánh của gia đình - " + _familyInfo.Name + Environment.NewLine 
            //    + "Chọn 1 ví trí gia đình khác, và dán vào." + Environment.NewLine
            //    + "Gia đình cắt sẽ năm ở vị trí con của Gia đình chọn dán"
            //    );
        }
        private void PasteFamilyClickFunc()
        {
            if (_objFamilyTree.FamilyCut == null)
            {
                MessageBox.Show("Chọn cắt nguyên nhánh của gia đình nào đó, rồi mới dán được");
                return;
            }
            if ( MessageBox.Show("Dán gia đình đã cắt " + _objFamilyTree.FamilyCut.Name0 + Environment.NewLine+
                "Vào dưới gia đình " + this.Name0
                , "Dán", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                // 1. remove gia đình ra khỏi cha
                if(_objFamilyTree.FamilyCut.Parent != null)
                {
                    // Lấy gia đình cha, list đám con, bỏ ra gia đình CUT
                    _objFamilyTree.FamilyCut.Parent.Children.Remove(_objFamilyTree.FamilyCut);
                    // Lấy gia đình dán, lấy cha
                    int index = this.Parent.Children.IndexOf(this);
                    // Thêm gia đình CUT vô làm con gia đình cha của this
                    this.Parent.Children.Insert(index+1, _objFamilyTree.FamilyCut);
                    // set gia đình cha cho gd cắt
                    _objFamilyTree.FamilyCut.Parent = this.Parent;
                    // 6. Update gia đình lấy bậc là bậc gia đình PASTE
                    UpdateLevel(_objFamilyTree.FamilyCut, this._familyInfo.FamilyLevel- _objFamilyTree.FamilyCut._familyInfo.FamilyLevel);
                }
                _objFamilyTree.FamilyCut = null;
            }
        }
        private void InsertPerson2FamilyClickFunc()
        {
            MessageBox.Show("Thêm người vào gia đình - " + _familyInfo.Name);
        }
        private void RemoveFamilyClickFunc()
        {
            if (this.Parent != null)
            {
                if (this.Parent.Children.IndexOf(this) > -1)
                {
                    if (MessageBox.Show("Xác nhận xóa gia đình " + Name0 + ", tất cả gia đình con của " + Name0 + " sẽ mất", "Xác nhận",
                        MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        this.Parent.Children.Remove(this);
                        this.Parent.IsExpanded = true;
                        this.Parent.IsSelected = true;
                        _objFamilyTree.OnPropertyChanged("GP");
                    }
                }
            }
        }
        private void RemoveFamilyOnlyClickFunc()
        {
            if (this.Parent != null)
            {
                if (this.Parent.Children.IndexOf(this) > -1)
                {
                    if (this.Children.Count == 1)
                    {
                        if (MessageBox.Show("Xác nhận xóa gia đình " + this.Name0+ ", đưa con là " + this.Children[0].Name0 + " thay vào vị trí này", "Xác nhận",
                            MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            int index = this.Parent.Children.IndexOf(this);
                            this.Parent.Children.Remove(this);
                            this.Parent.Children.Insert(index, this.Children[0]);
                            UpdateLevel(this.Children[0], - 1);
                            this.Children[0].Parent = this.Parent;
                            this.Parent.IsExpanded = true;
                            this.Parent.IsSelected = true;
                            _objFamilyTree.OnPropertyChanged("GP");
                        }
                    }
                }
            }
        }
        private void InsertFamilyConClickFunc()
        {
            // 1. Thêm thông tin gia đình mới vô 
            FamilyInfo insertFamily = new FamilyInfo();
            insertFamily.FamilyId = 0;
            insertFamily.FamilyLevel = this._familyInfo.FamilyLevel;
            insertFamily.FamilyUp = this._familyInfo.FamilyUp;
            insertFamily.FamilyOrder = this._familyInfo.FamilyOrder;
            // 2. Thêm 1 người mới tự động vô gia đình
            insertFamily.ListPerson = new ObservableCollection<PersonInfo>();
            var person = new PersonInfo(Name0+"Con" + (_familyListChildren.Count + 1), insertFamily);
            insertFamily.ListPerson.Add(person);
            // 3: Update info
            insertFamily.FamilyLevel = this._familyInfo.FamilyLevel + 1;
            insertFamily.FamilyUp = this._familyInfo.FamilyId;
            insertFamily.FamilyOrder = this._familyInfo.FamilyOrder;
            //
            //

            this.AddFamilyChild(insertFamily);
            this.IsExpanded = true;
            this.IsSelected = true;
            log.Info("======================= newChildFamily =======================");
            log.Info(this.Debug0);
            log.Info("======================= END =======================");
            _objFamilyTree.OnPropertyChanged("GP");
        }

        private void AddFamilyChild(FamilyInfo insertFamily)
        {
            this._familyInfo.FamilyChildren.Add(insertFamily);
            this._familyListChildren.Add(new FamilyViewModel(insertFamily, this, _objFamilyTree));
        }

        private void InsertFamilyEmClickFunc()
        {
            if (this.Parent == null)
            {
                return;
            }
            int indexThis = this.Parent.Children.IndexOf(this);
            if (indexThis < this.Parent.Children.Count-1)
            {
                this.Parent.Children.Move(indexThis, indexThis + 1);
            }
        }
        private void InsertFamilyAnhClickFunc()
        {
            if (this.Parent == null)
            {
                return;
            }
            int indexThis = this.Parent.Children.IndexOf(this);
            if (indexThis > 0)
            {
                this.Parent.Children.Move(indexThis, indexThis-1);
            }

        }
        private void InsertFamilyClickFunc()
        {
            // 1. Thêm thông tin gia đình mới vô 
            FamilyInfo newInsertFamily = new FamilyInfo();
            newInsertFamily.FamilyId = 0;
            newInsertFamily.FamilyLevel = this._familyInfo.FamilyLevel;
            newInsertFamily.FamilyUp = this._familyInfo.FamilyUp;
            newInsertFamily.FamilyOrder = this._familyInfo.FamilyOrder;
            // 3. Thêm 1 người mới tự động vô gia đình
            newInsertFamily.ListPerson = new ObservableCollection<PersonInfo>();
            //
            newInsertFamily.ListPerson.Add(new PersonInfo("Cha" + Name0 + "", newInsertFamily));

            newInsertFamily.FamilyChildren = new ObservableCollection<FamilyInfo>();
            // 2. Cho gia đình hiện tại trở thành con của gia đình chèn vô
            newInsertFamily.FamilyChildren.Add(this._familyInfo);


            // 4. Tạo thông tin gia đình mới, liên kết với phía gia đình hiện tại (là con)
            FamilyViewModel insert = new FamilyViewModel(newInsertFamily, this._familyParent, _objFamilyTree);

            // 5. Tạo thông tin gia đình mới, liên kết với phía gia đình cha
            if(this.Parent!=null)
            {
                if (this.Parent.Children.Contains(this))
                {
                    // 5.1 . Remove gia đình hiện tại, thêm gia đình chèn vô
                    int index = this.Parent.Children.IndexOf(this);
                    this.Parent.Children.Remove(this);
                    this.Parent.Children.Insert(index, insert);
                }
                this.Parent = insert;
            }
            else
            {
                // Insert truoc Thuy to
                this.Parent = insert;
            }

            // 6. Update gia đình this tăng 1 bậc sâu
            UpdateLevel(this, 1);

            this.IsExpanded = true;
            this.IsSelected = true;
            if (insert.Parent == null) {
                _objFamilyTree.Family.UpdateRootPerson(insert);
            }

            log.Info("======================= newInsertFamily =======================");
            log.Info(insert.Debug0);
            log.Info("======================= END =======================");
            _objFamilyTree.OnPropertyChanged("GP");
        }

        private void UpdateLevel(FamilyViewModel f, int levelUp)
        {
            f._familyInfo.FamilyLevel += levelUp;
            for (int i=0; i < f._familyListChildren.Count; i++)
            {
                UpdateLevel(f._familyListChildren[i], levelUp);
            }
        }


        #endregion // Constructors

        #region Person Properties
        public ObservableCollection<FamilyViewModel> Children
        {
            get { return _familyListChildren; }
        }

        public string Name
        {
            get { return _familyInfo.Name; }
        }

        public string Name0
        {
            get { return _familyInfo.Name0; }
        }

        public string Debug0
        {
            get {
                string debug = "DEBUG-";
                if(Children.Count > 0)
                {
                    debug += " CHILD= ";
                    foreach ( var child in Children)
                    {
                        debug += " :" + child.Name0;
                    }
                }
                else
                {
                    debug += " NOCHILD. ";
                }
                if (Parent!=null)
                {
                    debug += " PARENT= ";
                    debug += " :" + Parent.Name0;
                }
                else
                {
                    debug += " NO PARENT";
                }
                return debug;
            }
        }

        public ObservableCollection<PersonInfo> ListPerson
        {
            get { return _familyInfo.ListPerson; }
        }
        #endregion // Person Properties

        #region Presentation Members

        #region IsExpanded

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (value != _isExpanded)
                {
                    _isExpanded = value;
                    this.OnPropertyChanged("IsExpanded");
                }

                // Expand all the way up to the root.
                if (_isExpanded && _familyParent != null)
                    _familyParent.IsExpanded = true;
            }
        }

        #endregion // IsExpanded

        #region IsSelected

        /// <summary>
        /// Gets/sets whether the TreeViewItem 
        /// associated with this object is selected.
        /// </summary>
        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if (value != _isSelected)
                {
                    _isSelected = value;
                    this.OnPropertyChanged("IsSelected");
                }
            }
        }

        #endregion // IsSelected

        #region NameContainsText

        public bool NameContainsText(string text)
        {
            if (String.IsNullOrEmpty(text) || String.IsNullOrEmpty(this.Name))
                return false;

            return this.Name.IndexOf(text, StringComparison.InvariantCultureIgnoreCase) > -1;
        }

        #endregion // NameContainsText

        #region Parent

        public FamilyViewModel Parent
        {
            get { return _familyParent; }

            set {
                _familyParent = value;
                _objFamilyTree.OnPropertyChanged(nameof(Parent));
            }
        }

        #endregion // Parent

        #endregion // Presentation Members        

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion // INotifyPropertyChanged Members


        public string ToJson()
        {
            string json = "[";
            // Item 1 : Family info 
            json += "[";
            json += this._familyInfo.FamilyId + "," + this._familyInfo.FamilyLevel + "," + this._familyInfo.FamilyOrder + "," + this._familyInfo.FamilyUp;
            json += "],";
            // Item 2: List Person name
            json += "[";
            for (int i = 0; i < ListPerson.Count; i++)
            {
                var item = ListPerson[i];
                json += item.ToJson();
                if (i < ListPerson.Count - 1)
                {
                    json += ",";
                }
            }
            json += "],";
            // Item 2: List Child Family 
            json += "[";
            for (int i = 0; i < this.Children.Count; i++)
            {
                var item = Children[i];
                json += item.ToJson();
                if (i < Children.Count - 1)
                {
                    json += ",";
                }
            }
            json += "]";
            // Print end of family array
            json += "]";
            return json;
        }
    }
}