using GalaSoft.MvvmLight.Command;
using System;
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
            CutFamilyClick = new RelayCommand(CutFamilyClickFunc);
            PasteFamilyClick = new RelayCommand(PasteFamilyClickFunc);
            InsertFamilyClick = new RelayCommand(InsertFamilyClickFunc);
            RemoveFamilyClick = new RelayCommand(RemoveFamilyClickFunc);
            InsertPerson2FamilyClick = new RelayCommand(InsertPerson2FamilyClickFunc);
        }

        private void _familyInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Callback to here
            OnPropertyChanged("Name");
        }

        // MENU FUNCTION
        public ICommand CutFamilyClick { get; set; }
        public ICommand PasteFamilyClick { get; set; }
        public ICommand InsertFamilyClick { get; set; }
        public ICommand RemoveFamilyClick { get; set; }
        public ICommand InsertPerson2FamilyClick { get; set; }

        // MENU FUNCTION
        private void CutFamilyClickFunc()
        {
            MessageBox.Show("Chọn cắt (Cut) nhánh gia đình - " + _familyInfo.Name);

        }
        private void PasteFamilyClickFunc()
        {
            MessageBox.Show("Thêm người vào gia đình - " + _familyInfo.Name);
        }
        private void InsertPerson2FamilyClickFunc()
        {
            MessageBox.Show("Thêm người vào gia đình - " + _familyInfo.Name);
        }
        private void RemoveFamilyClickFunc()
        {
            MessageBox.Show("Xóa gia đình vị trí hiện tại - " + _familyInfo.Name);
        }
        private void InsertFamilyClickFunc()
        {
            //MessageBox.Show("Chèn gia đình mới vô vị trí hiện tại - " + _person.Name);
            

            // 1. Thêm thông tin gia đình mới vô 
            FamilyInfo insertFamily = new FamilyInfo();
            insertFamily.FamilyId = 0;
            insertFamily.FamilyLevel = this._familyInfo.FamilyLevel;
            insertFamily.FamilyUp = this._familyInfo.FamilyUp;
            insertFamily.FamilyOrder = this._familyInfo.FamilyOrder;
            // 3. Thêm 1 người mới tự động vô gia đình
            insertFamily.ListPerson = new ObservableCollection<PersonInfo>();
            insertFamily.ListPerson.Add(new PersonInfo("Người mới", insertFamily));

            if (this.Parent == null)
            {
                insertFamily.FamilyLevel = this._familyInfo.FamilyLevel+1;
                insertFamily.FamilyUp = this._familyInfo.FamilyId;
                insertFamily.FamilyOrder = this._familyInfo.FamilyOrder;

                this._familyListChildren.Add(new FamilyViewModel(insertFamily, this, _objFamilyTree));
                _objFamilyTree.OnPropertyChanged("GP");
                return;
            }
            insertFamily.FamilyChildren = new ObservableCollection<FamilyInfo>();
            // 2. Cho gia đình hiện tại trở thành con của gia đình chèn vô
            insertFamily.FamilyChildren.Add(this._familyInfo);

            // 4. Tạo thông tin gia đình mới, liên kết với phía gia đình hiện tại (là con)
            FamilyViewModel insert = new FamilyViewModel(insertFamily, this._familyParent, _objFamilyTree);

            // 5. Tạo thông tin gia đình mới, liên kết với phía gia đình cha
            if (this.Parent.Children.Contains(this))
            {
                // 5.1 . Remove gia đình hiện tại, thêm gia đình chèn vô
                int index = this.Parent.Children.IndexOf(this);
                this.Parent.Children.Remove(this);
                this.Parent.Children.Insert(index, insert);
            }

            
            // 6. Update gia đình this tăng 1 bậc sâu
            UpdateLevel(this, 1);

            this.IsExpanded = true;
            this.IsSelected = true;
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