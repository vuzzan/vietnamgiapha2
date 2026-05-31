using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
//using BusinessLib;

namespace vietnamgiapha
{
    /// <summary>
    /// This is the view-model of the UI.  It provides a data source
    /// for the TreeView (the FirstGeneration property), a bindable
    /// SearchText property, and the SearchCommand to perform a search.
    /// </summary>
    public class FamilyTreeViewModel : INotifyPropertyChanged
    {
        #region Data
        public ObservableCollection<FamilyViewModel> _firstGeneration;
        private FamilyViewModel _rootPerson;
        readonly ICommand _searchCommand;
        IEnumerator<FamilyViewModel> _matchingPeopleEnumerator;
        string _searchText = String.Empty;
        ObservableCollection<PersonInfo> _selectedListPerson;
        FamilyViewModel _selectedFamily;
        PersonInfo _selectedPerson;

        public GiaPhaViewModel _objFamilyTree;
        #endregion // Data

        #region Constructor
        public void UpdateRootPerson(FamilyViewModel rootPerson)
        {
            RootPerson = rootPerson;
            _firstGeneration.Clear();
            _firstGeneration.Add(rootPerson);
            SelectedFamily = RootPerson;
            OnPropertyChanged("RootPerson");
        }
        public FamilyTreeViewModel(FamilyInfo rootPerson, GiaPhaViewModel objFamilyTree)
        {
            _objFamilyTree = objFamilyTree;
            RootPerson = new FamilyViewModel(rootPerson, null, _objFamilyTree);
            _firstGeneration = new ObservableCollection<FamilyViewModel>(
                new FamilyViewModel[]
                {
                    RootPerson
                });
            SelectedFamily = RootPerson;
            _searchCommand = new SearchFamilyTreeCommand(this);
        }

        #endregion // Constructor

        public void ExpandAll()
        {
            RootPerson?.ExpandAllDescendants();
        }

        /// <summary>Chỉ mở nút gốc — dùng khi cây quá lớn.</summary>
        public void ExpandRootOnly()
        {
            if (RootPerson == null)
            {
                return;
            }

            RootPerson.IsExpanded = true;
        }

        public string ToJson()
        {
            return RootPerson.ToJson();
        }
        public int GetMaxFamilyId()
        {
            return _rootPerson.GetMaxFamilyId(0);
        }
        #region Properties

        public FamilyViewModel RootPerson
        {
            get { return _rootPerson; }
            set
            {
                _rootPerson = value;
                this.OnPropertyChanged("RootPerson");
            }
        }
        public FamilyViewModel SelectedParentFamily
        {
            get { return _selectedFamily; }
            set
            {
                _selectedFamily = value;
                _selectedListPerson = _selectedFamily.ListPerson;
                this.OnPropertyChanged("SelectedParentFamily");
            }
        }
        public FamilyViewModel SelectedChildrenFamily
        {
            get { return _selectedFamily; }
            set
            {
                _selectedFamily = value;
                _selectedListPerson = _selectedFamily.ListPerson;
                if (_selectedListPerson.Count > 0)
                {
                    _selectedPerson = _selectedListPerson[0];
                }
                this.OnPropertyChanged("SelectedChildrenFamily");
            }
        }
        public FamilyViewModel SelectedFamily
        {
            get { return _selectedFamily; }
            set
            {
                _selectedFamily = value;
                this.OnPropertyChanged("SelectedFamily");
                if (value == null)
                {
                    return;
                }

                _selectedListPerson = value.ListPerson;
                var main = PickPersonForGiaDinhDetail(value);
                if (main != null)
                {
                    _selectedPerson = main;
                    this.OnPropertyChanged("SelectedPerson");
                }
            }
        }

        /// <summary>Họ ông thủy tổ = từ đầu tiên của tên người chính ở gốc cây.</summary>
        public string GetThuyToClanSurname()
        {
            if (RootPerson?.ListPerson != null && RootPerson.ListPerson.Count > 0)
            {
                var main = RootPerson.ListPerson.FirstOrDefault(p => p.IsMainPerson == 1)
                    ?? RootPerson.ListPerson[0];
                string ho = Util.GetFirstWord(main?.MANS_NAME_HUY ?? "");
                if (!string.IsNullOrWhiteSpace(ho))
                {
                    return ho;
                }
            }

            return _objFamilyTree?.GP?.GiaphaNameRoot?.Trim() ?? "";
        }

        /// <summary>Đồng bộ tab 2.Gia Đình sau sửa tên trên cây.</summary>
        public void SyncGiaDinhTabForFamily(FamilyViewModel family)
        {
            if (family == null)
            {
                return;
            }

            family.OnPropertyChanged(nameof(FamilyViewModel.ListPerson));
            family.OnPropertyChanged(nameof(FamilyViewModel.Name));
            SelectedFamily = family;
            family.IsSelected = true;
        }

        private static PersonInfo PickPersonForGiaDinhDetail(FamilyViewModel family)
        {
            if (family?.ListPerson == null || family.ListPerson.Count == 0)
            {
                return null;
            }

            return family.ListPerson.FirstOrDefault(p => p.IsMainPerson == 1) ?? family.ListPerson[0];
        }

        public PersonInfo SelectedPerson
        {
            get { return _selectedPerson; }
            set
            {
                _selectedPerson = value;
                this.OnPropertyChanged("SelectedPerson");
                this.OnPropertyChanged("SelectedFamily");
            }
        }

        /// <summary>Chọn gia đình trên cây (mở rộng nhánh cha, đồng bộ SelectedFamily / SelectedPerson).</summary>
        public void SelectFamily(FamilyViewModel family)
        {
            if (family == null)
            {
                return;
            }

            for (var parent = family.Parent; parent != null; parent = parent.Parent)
            {
                parent.IsExpanded = true;
            }

            SyncGiaDinhTabForFamily(family);
        }

        #region FirstGeneration

        /// <summary>
        /// Returns a read-only collection containing the first person 
        /// in the family tree, to which the TreeView can bind.
        /// </summary>
        public ObservableCollection<FamilyViewModel> FirstGeneration
        {
            get { return _firstGeneration; }
        }

        #endregion // FirstGeneration

        #region SearchCommand

        /// <summary>
        /// Returns the command used to execute a search in the family tree.
        /// </summary>
        public ICommand SearchCommand
        {
            get { return _searchCommand; }
        }

        private class SearchFamilyTreeCommand : ICommand
        {
            readonly FamilyTreeViewModel _familyTree;

            public SearchFamilyTreeCommand(FamilyTreeViewModel familyTree)
            {
                _familyTree = familyTree;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            event EventHandler ICommand.CanExecuteChanged
            {
                // I intentionally left these empty because
                // this command never raises the event, and
                // not using the WeakEvent pattern here can
                // cause memory leaks.  WeakEvent pattern is
                // not simple to implement, so why bother.
                add { }
                remove { }
            }

            public void Execute(object parameter)
            {
                _familyTree.PerformSearch();
            }
        }

        #endregion // SearchCommand

        #region SearchText

        /// <summary>
        /// Gets/sets a fragment of the name to search for.
        /// </summary>
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (value == _searchText)
                    return;

                _searchText = value;

                _matchingPeopleEnumerator = null;
            }
        }

        #endregion // SearchText

        #endregion // Properties

        #region Search Logic

        void PerformSearch()
        {
            if(_searchText.Trim().Length==0)
            {
                MessageBox.Show("Nhập tên để tìm...");
                return;
            }
            if (_matchingPeopleEnumerator == null || !_matchingPeopleEnumerator.MoveNext())
                this.VerifyMatchingPeopleEnumerator();

            var person = _matchingPeopleEnumerator.Current;

            if (person == null)
                return;

            // Ensure that this person is in view.
            if (person.Parent != null)
            {
                person.Parent.IsExpanded = true;
            }

            person.IsSelected = true;
        }

        void VerifyMatchingPeopleEnumerator()
        {
            var matches = this.FindMatches(_searchText, _rootPerson);
            _matchingPeopleEnumerator = matches.GetEnumerator();

            if (!_matchingPeopleEnumerator.MoveNext())
            {
                MessageBox.Show(
                    "Không tìm thấy",
                    "Không tìm thấy",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                    );
            }
        }

        IEnumerable<FamilyViewModel> FindMatches(string searchText, FamilyViewModel person)
        {
            if (person.NameContainsText(searchText))
                yield return person;

            foreach (FamilyViewModel child in person.Children)
                foreach (FamilyViewModel match in this.FindMatches(searchText, child))
                    yield return match;
        }

        #endregion // Search Logic

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion // INotifyPropertyChanged Members
    }
}