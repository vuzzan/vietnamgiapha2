using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using vietnamgiapha.ValueConverter;
using WpfDraw.Class;
using static System.Net.Mime.MediaTypeNames;

namespace vietnamgiapha
{
    /// <summary>
    /// A UI-friendly wrapper around a Person object.
    /// </summary>
    public class FamilyViewModel :  INotifyPropertyChanged
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");
        #region for Graph
        private Node _node;

        /// <summary>Node đồ thị — tạo lười để load file lớn không dựng hàng nghìn TextBlock WPF.</summary>
        public Node Node
        {
            get
            {
                if (_node == null)
                {
                    _node = new Node(this, _familyInfo.X, _familyInfo.Y);
                    _node.UpdateNodeSize += _node_UpdateNodeSize;
                }

                return _node;
            }
        }
        #endregion for Graph
        #region Data

        private ObservableCollection<FamilyViewModel> _familyListChildren;
        FamilyViewModel _familyParent;
        private FamilyInfo _familyInfo;

        bool _isExpanded;
        bool _isSelected;
        private static FamilyViewModel _treeLabelEditingFamily;
        private string _treeEditText = "";

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
            CheckFamilyClick = new RelayCommand(CheckFamilyClickFunc);
            DebugFamilyClick = new RelayCommand(DebugFamilyClickFunc);
            CutFamilyClick = new RelayCommand(CutFamilyClickFunc);
            PasteFamilyEmClick = new RelayCommand(PasteFamilyEmClickFunc);
            PasteFamilyConClick = new RelayCommand(PasteFamilyConClickFunc);
            InsertFamilyClick = new RelayCommand(InsertFamilyClickFunc);
            InsertFamilyAnhClick = new RelayCommand(InsertFamilyAnhClickFunc);
            InsertFamilyEmClick = new RelayCommand(InsertFamilyEmClickFunc);
            InsertFamilyConClick = new RelayCommand(InsertFamilyConClickFunc);
            RemoveFamilyClick = new RelayCommand(RemoveFamilyClickFunc);
            RemoveFamilyOnlyClick = new RelayCommand(RemoveFamilyOnlyClickFunc);
            InsertPerson2FamilyClick = new RelayCommand(InsertPerson2FamilyClickFunc);
            //DeletePersonFromFamilyClick = new RelayCommand(DeletePersonFromFamilyClickFunc);
        }

        private void _node_SelectedNodeEvent(double x, double y, double w, double h)
        {
            throw new NotImplementedException();
        }

        private void _node_UpdateNodeSize(double x, double y, double w, double h)
        {
            if( _familyInfo != null)
            {
                _familyInfo.X = (int)x;
                _familyInfo.Y = (int)y;
                _familyInfo.Width = (int)w;
                _familyInfo.Height = (int)h;
            }
        }

        private void _familyInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Callback to here
            OnPropertyChanged("Name");
        }

        // MENU FUNCTION
        public ICommand CheckFamilyClick { get; set; }
        public ICommand DebugFamilyClick { get; set; }
        public ICommand CutFamilyClick { get; set; }
        public ICommand PasteFamilyEmClick { get; set; }
        public ICommand PasteFamilyConClick { get; set; }
        public ICommand InsertFamilyClick { get; set; }
        public ICommand InsertFamilyAnhClick { get; set; }
        public ICommand InsertFamilyEmClick { get; set; }
        public ICommand InsertFamilyConClick { get; set; }
        public ICommand RemoveFamilyClick { get; set; }
        public ICommand RemoveFamilyOnlyClick { get; set; }
        public ICommand InsertPerson2FamilyClick { get; set; }
        
        public void MakeOrderChild()
        {
            if( Children.Count > 0 )
            {
                //
                for(int i=0; i< Children.Count; i++)
                {
                    Children[i]._familyInfo.FamilyOrder = (i + 1);
                }
                //
                //Children.OrderBy(i => i._familyInfo.FamilyOrder);
            }
        }
        // MENU FUNCTION
        public void AddUserAction(string action)
        {
            if (_objFamilyTree != null)
            {
                _objFamilyTree.AddUserAction(action);
            }
        }
        public static bool CheckValid(FamilyViewModel root, ref string errorMessage)
        {
            if( root == null)
            {
                errorMessage = "Check NULL";
                return false;
            }
            if (root.Parent != null)
            {
                if (root.Parent._familyInfo.FamilyId == root._familyInfo.FamilyUp)
                {
                    // ok
                }
                else
                {
                    // sai
                    errorMessage += "Sai ID cha giữa gd con [" + root.Name0 + "] và [" + root.Parent.Name0 + "]" + Environment.NewLine;
                    if( MessageBox.Show("Tự động sửa ??", "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        root._familyInfo.FamilyUp = root.Parent._familyInfo.FamilyId;
                    }
                }
            }
            if (root.Children.Count > 0)
            {
                foreach (var child in root.Children)
                {
                    if (child._familyInfo.FamilyUp != root._familyInfo.FamilyId)
                    {
                        errorMessage += "- Sai ID cha giữa gd con [" + child.Name0 + "] và [" + root.Name0 + "] " + Environment.NewLine;
                        if (MessageBox.Show("Tự động sửa ??", "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            child._familyInfo.FamilyUp = root._familyInfo.FamilyId;
                        }
                    }
                    CheckValid(child, ref errorMessage);
                }
            }
            else
            {
            }

            return errorMessage.Length==0;
        }
        private void CheckFamilyClickFunc()
        {
            string errorMessage = "";
            CheckValid(this, ref errorMessage);
            if (errorMessage.Length > 0)
            {
                MessageBox.Show(errorMessage, "Có lỗi");
            }
            // Auto corerct 

            errorMessage = "";
            //AutoCorrect(this, ref errorMessage);
            //if (errorMessage.Length > 0)
            //{
            //    MessageBox.Show(errorMessage, "Tự động chỉnh");
            //}
        }
        public static bool AutoCorrect(FamilyViewModel root, ref string errorMessage, string THEO_HO = "NGUYỄN")
        {
            if (root == null)
            {
                errorMessage = "Check NULL";
                return false;
            }
            bool isMain = false;
            int countNam = 0;
            int countNu = 0;
            int countSameHo = 0;
            //string THEO_HO = "NGUYỄN";
            THEO_HO = THEO_HO.ToUpper();
            foreach (var mans in root.ListPerson)
            {
                if(mans.MANS_NAME_HUY.ToUpper().Contains(THEO_HO + " "))
                {
                    // COUNT 2 NGUOI CUNG 1 HO
                    countSameHo++;
                    // NGUOI TRONG GIA ĐÌNH
                    if (isMain == false || mans.IsMainPerson == 0)
                    {
                        mans.IsMainPerson = 1;
                        isMain = true;
                        errorMessage += mans.MANS_NAME_HUY + ": thành người thuộc tộc họ. " + Environment.NewLine;
                    }
                }
                if (mans.MANS_NAME_HUY.ToUpper().Contains(" VĂN "))
                {
                    mans.MANS_GENDER = "Nam";
                    errorMessage += mans.MANS_NAME_HUY + ": Giới tính -> Nam" + Environment.NewLine;
                }
                if (mans.MANS_NAME_HUY.ToUpper().Contains(" THỊ "))
                {
                    mans.MANS_GENDER = "Nữ";
                    errorMessage += mans.MANS_NAME_HUY + ": Giới tính -> Nữ" + Environment.NewLine;
                }

                if (mans.MANS_GENDER == "Nam")
                {
                    countNam++;
                }
                else if (mans.MANS_GENDER == "Nữ")
                {
                    countNu++;
                }
            }
            // NEU 1 NAM + NHIEU NU -> LAY NAM LÀM MAIN
            if( countNam==1 && countNu > 1)
            {
                // LAY NAM LAM MAIN
                foreach (var mans in root.ListPerson)
                {
                    if (mans.MANS_GENDER == "Nam")
                    {
                        mans.IsMainPerson = 1;
                        errorMessage += mans.MANS_NAME_HUY + ": thành người thuộc tộc họ." + Environment.NewLine;
                    }
                    else
                    {
                        mans.IsMainPerson = 0;
                    }
                }
            }
            if (countSameHo>1)
            {
                foreach (var mans in root.ListPerson)
                {
                    // UU TIEN NGUOI CO 11.2..
                    if (mans.MANS_NAME_HUY.ToUpper().IndexOf(THEO_HO + " ")>1)
                    {
                        mans.IsMainPerson = 1;
                        errorMessage += mans.MANS_NAME_HUY + ": thành người thuộc tộc họ." + Environment.NewLine;
                    }
                    else
                    {
                        mans.IsMainPerson = 0;
                    }
                }
            }
            // SORT BY MAIN
            var list = root._familyInfo.ListPerson.OrderByDescending(v => v.IsMainPerson).ToList();
            root._familyInfo.ListPerson.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                if (i == 0)
                {
                    root._familyInfo.ListPerson.Add(list[i]);
                }
                else
                {
                    list[i].MANS_GENDER = list[0].IsGioiTinhNam == 1 ? "Nữ" : "Nam";
                    root._familyInfo.ListPerson.Add(list[i]);
                }
            }


            int countManMain = 0;
            foreach (var mans in root.ListPerson)
            {
                if (mans.IsMainPerson == 1)
                {
                    countManMain++;
                }
            }
            if (countManMain > 1)
            {
                //errorMessage += "Sai nhieu nguoi chính ở GD: " + root.Name0 +"  " + Environment.NewLine;
            }
            if (root.Children.Count > 0)
            {
                foreach (var child in root.Children)
                {
                    AutoCorrect(child, ref errorMessage, THEO_HO);
                }
            }
            else
            {
            }

            return errorMessage.Length == 0;
        }
        public void DebugFamilyClickFunc()
        {
            string text = "Gia đình - " + this.Name0 + " | ID=[" + this._familyInfo.FamilyId + "] ID GD CHA=[" + this._familyInfo.FamilyUp  + "]" + Environment.NewLine;
            text += "--- " + Environment.NewLine;
            if (this.Parent != null)
            {
                text += "Gia đình cha - " + this.Parent.Name0 + " ID=[" + this.Parent._familyInfo.FamilyId + "]" + Environment.NewLine;
                if(this.Parent._familyInfo.FamilyId== this._familyInfo.FamilyUp)
                {
                    // ok
                }
                else
                {
                    // sai
                    MessageBox.Show("Sai ID cha giữa gd con ["+ this.Name0+ "] và [" + this.Parent.Name0 +"] ", "Có lỗi");
                    if (MessageBox.Show("Tự động sửa ??", "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        this._familyInfo.FamilyUp = this.Parent._familyInfo.FamilyId;
                    }
                }
            }
            else
            {
                text += "Gia đình cha - ROOT" + Environment.NewLine;
            }
            text += "GĐ CON:" + Environment.NewLine;
            string checkError = "";
            if ( this.Children.Count > 0)
            {
                foreach( var child in this.Children)
                {
                    text += "Gia đình con - " + child.Name0 + " ID=[" + child._familyInfo.FamilyId + "] Up=[" + this._familyInfo.FamilyId  + "]" + Environment.NewLine;
                    if (child._familyInfo.FamilyUp != this._familyInfo.FamilyId)
                    {
                        string tempError = "- Sai ID cha giữa gd con [" + child.Name0 + "] và [" + this.Name0 + "] " + Environment.NewLine;
                        checkError += tempError;
                        if (MessageBox.Show(tempError + "Tự động sửa ??", "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            child._familyInfo.FamilyUp = this._familyInfo.FamilyId;
                        }
                    }
                }
                if( checkError.Length> 0 ) {
                    MessageBox.Show(checkError, "Có lỗi");
                }
            }
            else
            {
                text += "Không con " + Environment.NewLine;
            }
            
            MessageBox.Show(
                text, "Chi tiết gia đình: " + this.Name0
                );
        }
        private void CaptureUndoBefore(string label)
        {
            _objFamilyTree?.CaptureUndoSnapshot(label);
        }

        /// <summary>Ctrl+C — copy nguyên nhánh (không xóa trên cây).</summary>
        public void CopyBranchToClipboard()
        {
            _objFamilyTree.FamilyCopyBranch = FamilyBranchCloneHelper.CloneFamilyBranch(_familyInfo);
            _objFamilyTree.FamilyCopySource = this;
            _objFamilyTree.FamilyCut = null;
            AddUserAction("Copy nhánh: " + Name0);
        }

        /// <summary>Ctrl+V — dán nhánh đã copy làm con của gia đình đang chọn.</summary>
        public FamilyViewModel PasteCopiedBranchAsChild()
        {
            var copy = _objFamilyTree?.FamilyCopyBranch;
            if (copy == null)
            {
                MessageBox.Show("Chưa copy nhánh nào (Ctrl+C trên cây).", "Dán nhánh");
                return null;
            }

            var source = _objFamilyTree.FamilyCopySource;
            if (source != null
                && (ReferenceEquals(this, source) || IsDescendantOf(source)))
            {
                MessageBox.Show("Không thể dán nhánh vào chính nó hoặc vào con cháu của nhánh gốc đã copy.", "Dán nhánh");
                return null;
            }

            CaptureUndoBefore("Dán nhánh (copy)");
            var cloned = FamilyBranchCloneHelper.CloneFamilyBranch(copy);
            int nextId = _objFamilyTree.Family.GetMaxFamilyId() + 1;
            FamilyBranchCloneHelper.RemapFamilyIds(cloned, ref nextId);

            cloned.FamilyUp = _familyInfo.FamilyId;
            int levelDelta = _familyInfo.FamilyLevel + 1 - cloned.FamilyLevel;
            var added = InsertChildFamilyAt(0, cloned);
            UpdateLevel(added, levelDelta);
            IsExpanded = true;
            AddUserAction("Dán nhánh copy vào con của " + Name0 + " ← " + added.Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return added;
        }

        public bool RemoveChildFully(FamilyViewModel child)
        {
            if (child == null)
            {
                return false;
            }

            int i = _familyListChildren.IndexOf(child);
            if (i < 0)
            {
                return false;
            }

            _familyListChildren.RemoveAt(i);
            _familyInfo.FamilyChildren.RemoveAt(i);
            // Gỡ liên kết cha — caller phải lưu Parent trước khi gọi nếu còn dùng node con
            child._familyParent = null;
            MakeOrderChild();
            return true;
        }

        /// <summary>Shift+Insert — chèn GD mới, GD hiện tại thành con.</summary>
        public FamilyViewModel InsertParentFamilyFromTree()
        {
            CaptureUndoBefore("Chèn gia đình cha");
            var newInsertFamily = new FamilyInfo
            {
                FamilyId = _objFamilyTree.Family.GetMaxFamilyId() + 1,
                FamilyLevel = _familyInfo.FamilyLevel,
                FamilyUp = _familyInfo.FamilyUp,
                FamilyOrder = _familyInfo.FamilyOrder,
                ListPerson = new ObservableCollection<PersonInfo>(),
                FamilyChildren = new ObservableCollection<FamilyInfo>()
            };
            newInsertFamily.ListPerson.Add(new PersonInfo("Cha" + Name0, newInsertFamily));

            FamilyViewModel insert;
            var parent = Parent;
            if (parent != null)
            {
                int index = parent.Children.IndexOf(this);
                parent.RemoveChildFully(this);
                newInsertFamily.FamilyChildren.Add(_familyInfo);
                _familyInfo.FamilyUp = newInsertFamily.FamilyId;
                insert = parent.InsertChildFamilyAt(index, newInsertFamily);
                foreach (var child in insert.Children)
                {
                    if (ReferenceEquals(child._familyInfo, _familyInfo))
                    {
                        UpdateLevel(child, 1);
                        break;
                    }
                }
            }
            else
            {
                newInsertFamily.FamilyChildren.Add(_familyInfo);
                _familyInfo.FamilyUp = newInsertFamily.FamilyId;
                insert = new FamilyViewModel(newInsertFamily, null, _objFamilyTree);
                _objFamilyTree.Family.UpdateRootPerson(insert);
                if (insert.Children.Count > 0)
                {
                    UpdateLevel(insert.Children[0], 1);
                }
            }

            insert.IsExpanded = true;
            insert.IsSelected = true;
            AddUserAction("Chèn gia đình cha trước " + Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return insert;
        }

        /// <summary>Xóa GD, đưa đúng 1 con lên thay (không hỏi lại).</summary>
        public bool TryDeleteFamilyPromoteOnlyChild()
        {
            if (Parent == null || Children.Count != 1)
            {
                return false;
            }

            CaptureUndoBefore("Xóa gia đình, giữ nhánh con");
            int index = Parent.Children.IndexOf(this);
            var promoted = Children[0];

            Parent._familyListChildren.RemoveAt(index);
            Parent._familyInfo.FamilyChildren.RemoveAt(index);
            _familyInfo.FamilyChildren.Remove(promoted._familyInfo);

            Parent._familyInfo.FamilyChildren.Insert(index, promoted._familyInfo);
            Parent._familyListChildren.Insert(index, promoted);
            promoted._familyParent = Parent;
            promoted._familyInfo.FamilyUp = Parent._familyInfo.FamilyId;
            UpdateLevel(promoted, -1);
            Parent.MakeOrderChild();
            Parent.IsExpanded = true;
            Parent.IsSelected = true;
            _objFamilyTree?.Family?.SyncGiaDinhTabForFamily(Parent);

            AddUserAction("Xóa gia đình " + Name0 + ", đưa con " + promoted.Name0 + " thay vị trí");
            _objFamilyTree?.OnPropertyChanged("GP");
            return true;
        }

        /// <summary>Xóa cả nhánh (không hỏi lại). Trả về gia đình cha để chọn lại trên cây.</summary>
        public FamilyViewModel TryDeleteFamilyBranch()
        {
            if (Parent == null)
            {
                MessageBox.Show("Không thể xóa gia đình gốc (thủy tổ) bằng phím tắt.", "Xóa gia đình");
                return null;
            }

            var parent = Parent;
            if (parent.Children.IndexOf(this) < 0)
            {
                return null;
            }

            CaptureUndoBefore("Xóa gia đình và nhánh con");
            parent.RemoveChildFully(this);
            parent.IsExpanded = true;
            parent.IsSelected = true;
            _objFamilyTree?.Family?.SyncGiaDinhTabForFamily(parent);
            AddUserAction("Xóa gia đình " + Name0 + ", tất cả gia đình con");
            _objFamilyTree?.OnPropertyChanged("GP");
            return parent;
        }

        private void CutFamilyClickFunc()
        {
            CaptureUndoBefore("Cắt gia đình");
            _objFamilyTree.FamilyCut = this;
            // Lấy gia đình dán, lấy cha
            int index = this.Parent.Children.IndexOf(this);
            this.Parent.Children.Remove(this);
            AddUserAction("Cắt GD " + this.Name0);
        }

        /// <summary>Kiểm tra node có nằm dưới nhánh this (con/cháu) không.</summary>
        public bool IsDescendantOf(FamilyViewModel ancestor)
        {
            if (ancestor == null)
            {
                return false;
            }

            for (FamilyViewModel p = Parent; p != null; p = p.Parent)
            {
                if (ReferenceEquals(p, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Di chuyển this thành con của targetParent (sau khi user đã xác nhận).</summary>
        public bool TryMoveAsChildOf(FamilyViewModel targetParent)
        {
            if (targetParent == null
                || ReferenceEquals(this, targetParent)
                || IsDescendantOf(targetParent))
            {
                return false;
            }

            if (Parent != null)
            {
                Parent.Children.Remove(this);
            }

            targetParent.Children.Insert(0, this);
            Parent = targetParent;
            _familyInfo.FamilyUp = targetParent._familyInfo.FamilyId;
            int levelDelta = targetParent._familyInfo.FamilyLevel - _familyInfo.FamilyLevel + 1;
            UpdateLevel(this, levelDelta);
            targetParent.MakeOrderChild();
            targetParent.IsExpanded = true;
            IsSelected = true;
            _objFamilyTree?.OnPropertyChanged("GP");
            AddUserAction("Kéo thả: " + Name0 + " → con của " + targetParent.Name0);
            return true;
        }

        /// <summary>Đặt this ngay sau targetSibling — cùng cha chỉ đổi thứ tự (đời không đổi).</summary>
        public bool TryMoveAsSiblingAfter(FamilyViewModel targetSibling)
        {
            if (targetSibling == null
                || ReferenceEquals(this, targetSibling)
                || targetSibling.IsDescendantOf(this))
            {
                return false;
            }

            FamilyViewModel newParent = targetSibling.Parent;
            if (newParent == null)
            {
                return false;
            }

            // Cùng cha: chỉ sắp xếp lại anh em — không đụng FamilyLevel / FamilyUp
            bool reorderAmongSiblings = Parent != null && ReferenceEquals(Parent, newParent);

            if (Parent != null)
            {
                Parent.Children.Remove(this);
            }

            int insertIndex = newParent.Children.IndexOf(targetSibling);
            if (insertIndex < 0)
            {
                insertIndex = newParent.Children.Count - 1;
            }
            else
            {
                insertIndex++;
            }

            if (insertIndex > newParent.Children.Count)
            {
                insertIndex = newParent.Children.Count;
            }

            newParent.Children.Insert(insertIndex, this);
            Parent = newParent;

            if (!reorderAmongSiblings)
            {
                _familyInfo.FamilyUp = newParent._familyInfo.FamilyId;
                // Đời anh em = đời node đích, không lấy bậc của cha (cha thấp hơn 1 đời)
                int levelDelta = targetSibling._familyInfo.FamilyLevel - _familyInfo.FamilyLevel;
                UpdateLevel(this, levelDelta);
            }

            newParent.MakeOrderChild();
            newParent.IsExpanded = true;
            IsSelected = true;
            _objFamilyTree?.OnPropertyChanged("GP");
            AddUserAction("Kéo thả: " + Name0 + " → sau " + targetSibling.Name0
                + (reorderAmongSiblings ? " (cùng cha)" : " (anh em nhánh khác)"));
            return true;
        }

        /// <summary>Mô tả thao tác kéo thả để hỏi xác nhận.</summary>
        public static string DescribeTreeDragMove(FamilyViewModel source, FamilyViewModel target)
        {
            if (source == null || target == null)
            {
                return "";
            }

            if (source.Parent != null
                && target.Parent != null
                && ReferenceEquals(source.Parent, target.Parent)
                && !ReferenceEquals(source, target))
            {
                return "Đặt gia đình \"" + source.Name0 + "\" ngay sau \""
                    + target.Name0 + "\" (cùng cha: " + source.Parent.Name0 + ")?";
            }

            return "Di chuyển gia đình \"" + source.Name0 + "\" thành con của \""
                + target.Name0 + "\"?";
        }
        private void PasteFamilyEmClickFunc()
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
                CaptureUndoBefore("Dán gia đình (em)");
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
                    // Update lại ID Gia đình cha, cho gia đình dán
                    _objFamilyTree.FamilyCut._familyInfo.FamilyUp = this._familyInfo.FamilyUp;

                    // 6. Update gia đình lấy bậc là bậc gia đình PASTE
                    UpdateLevel(_objFamilyTree.FamilyCut, this._familyInfo.FamilyLevel- _objFamilyTree.FamilyCut._familyInfo.FamilyLevel);

                    AddUserAction("Dán gia đình đã cắt " + _objFamilyTree.FamilyCut.Name0 + " vào dưới gia đình " + this.Name0);
                }
                _objFamilyTree.FamilyCut = null;
            }
        }

        private void PasteFamilyConClickFunc()
        {
            if (_objFamilyTree.FamilyCut == null)
            {
                MessageBox.Show("Chọn cắt nguyên nhánh của gia đình nào đó, rồi mới dán được");
                return;
            }
            if (MessageBox.Show("Dán gia đình đã cắt " + _objFamilyTree.FamilyCut.Name0 + Environment.NewLine +
                "Vào làm gia đình con của gia đình " + this.Name0
                , "Dán", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                CaptureUndoBefore("Dán gia đình (con)");
                // 1. remove gia đình ra khỏi cha
                if (_objFamilyTree.FamilyCut.Parent != null)
                {
                    // Lấy gia đình cha, list đám con, bỏ ra gia đình CUT
                    _objFamilyTree.FamilyCut.Parent.Children.Remove(_objFamilyTree.FamilyCut);
                    // Thêm gia đình CUT list gia đình con của this
                    this.Children.Insert(0, _objFamilyTree.FamilyCut);
                    // set gia đình cha cho gd cắt
                    _objFamilyTree.FamilyCut.Parent = this;
                    // Update lại ID Gia đình cha, cho gia đình dán
                    _objFamilyTree.FamilyCut._familyInfo.FamilyUp = this._familyInfo.FamilyId;
                    // 6. Update gia đình lấy bậc là bậc gia đình PASTE
                    UpdateLevel(_objFamilyTree.FamilyCut, this._familyInfo.FamilyLevel - _objFamilyTree.FamilyCut._familyInfo.FamilyLevel + 1);

                    AddUserAction("Dán gia đình đã cắt " + _objFamilyTree.FamilyCut.Name0 + " vào làm gia đình con của gia đình " + this.Name0);
                }
                _objFamilyTree.FamilyCut = null;
            }
        }
        private void InsertPerson2FamilyClickFunc()
        {
            CaptureUndoBefore("Thêm người vào gia đình");
            if( this.ListPerson.Count>=1)
            {
                var person = new PersonInfo("Người mới", this._familyInfo);
                foreach(var item in this.ListPerson )
                {
                    if (item.IsMainPerson == 1)
                    {
                        if (item.IsGioiTinhNam == 1)
                        {
                            // person - gt = nu
                            person.MANS_GENDER = "Nữ";
                        }
                        else
                        {
                            // person - gt = nam
                            person.MANS_GENDER = "Nam";
                        }
                    }
                }
                this.ListPerson.Add(person);
            }
        }



        private void RemoveFamilyClickFunc()
        {
            if (Parent == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "Xác nhận xóa gia đình " + Name0 + ", tất cả gia đình con của " + Name0 + " sẽ mất",
                    "Xác nhận",
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                TryDeleteFamilyBranch();
            }
        }

        private void RemoveFamilyOnlyClickFunc()
        {
            if (Parent == null || Children.Count != 1)
            {
                if (Parent != null && Children.Count != 1)
                {
                    MessageBox.Show("Chỉ xóa và đưa con lên khi gia đình có đúng 1 gia đình con.", "Xóa gia đình");
                }

                return;
            }

            if (MessageBox.Show(
                    "Xác nhận xóa gia đình " + Name0 + ", đưa con là " + Children[0].Name0 + " thay vào vị trí này",
                    "Xác nhận",
                    MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                TryDeleteFamilyPromoteOnlyChild();
            }
        }
        private void InsertFamilyConClickFunc()
        {
            var added = InsertNewChildFamilyFromTree();
            if (added != null)
            {
                added.IsSelected = true;
            }
        }

        /// <summary>Shift+[+] — thêm gia đình đời con dưới node đang chọn.</summary>
        public FamilyViewModel InsertNewChildFamilyFromTree()
        {
            CaptureUndoBefore("Thêm gia đình con");
            FamilyInfo insertFamily = new FamilyInfo();
            insertFamily.FamilyId = this._objFamilyTree.Family.GetMaxFamilyId() + 1;
            insertFamily.FamilyLevel = this._familyInfo.FamilyLevel + 1;
            insertFamily.FamilyUp = this._familyInfo.FamilyId;
            insertFamily.FamilyOrder = this._familyInfo.FamilyOrder;
            insertFamily.ListPerson = new ObservableCollection<PersonInfo>();
            var person = new PersonInfo(Name0 + "Con" + (_familyListChildren.Count + 1), insertFamily);
            person.MANS_CONTHUMAY = "" + (_familyListChildren.Count + 1);
            insertFamily.ListPerson.Add(person);

            var added = AddFamilyChild(insertFamily);
            IsExpanded = true;
            AddUserAction("Thêm gia đình con " + Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return added;
        }

        /// <summary>Shift+E — thêm gia đình em (cùng đời, ngay sau node đang chọn).</summary>
        public FamilyViewModel InsertNewSiblingEmFromTree()
        {
            if (Parent == null)
            {
                return null;
            }

            CaptureUndoBefore("Thêm gia đình em");
            int indexThis = Parent.Children.IndexOf(this);
            if (indexThis < 0)
            {
                indexThis = Parent.Children.Count - 1;
            }

            var insertFamily = CreateSiblingFamilyInfo("Em", Parent.Children.Count + 1);
            var added = Parent.InsertChildFamilyAt(indexThis + 1, insertFamily);
            Parent.IsExpanded = true;
            AddUserAction("Thêm gia đình em " + added.Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return added;
        }

        /// <summary>Shift+A — thêm gia đình anh (cùng đời, ngay trước node đang chọn).</summary>
        public FamilyViewModel InsertNewSiblingAnhFromTree()
        {
            if (Parent == null)
            {
                return null;
            }

            CaptureUndoBefore("Thêm gia đình anh");
            int indexThis = Parent.Children.IndexOf(this);
            if (indexThis < 0)
            {
                indexThis = 0;
            }

            var insertFamily = CreateSiblingFamilyInfo("Anh", Parent.Children.Count + 1);
            var added = Parent.InsertChildFamilyAt(indexThis, insertFamily);
            Parent.IsExpanded = true;
            AddUserAction("Thêm gia đình anh " + added.Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return added;
        }

        private FamilyInfo CreateSiblingFamilyInfo(string suffix, int ordinal)
        {
            var insertFamily = new FamilyInfo
            {
                FamilyId = _objFamilyTree.Family.GetMaxFamilyId() + 1,
                FamilyLevel = _familyInfo.FamilyLevel,
                FamilyUp = _familyInfo.FamilyUp,
                FamilyOrder = _familyInfo.FamilyOrder,
                ListPerson = new ObservableCollection<PersonInfo>()
            };
            insertFamily.ListPerson.Add(new PersonInfo(Name0 + suffix + ordinal, insertFamily));
            return insertFamily;
        }

        private FamilyViewModel InsertChildFamilyAt(int index, FamilyInfo insertFamily)
        {
            if (index < 0)
            {
                index = 0;
            }

            if (index > _familyListChildren.Count)
            {
                index = _familyListChildren.Count;
            }

            _familyInfo.FamilyChildren.Insert(index, insertFamily);
            var added = new FamilyViewModel(insertFamily, this, _objFamilyTree);
            _familyListChildren.Insert(index, added);
            MakeOrderChild();
            return added;
        }

        private FamilyViewModel AddFamilyChild(FamilyInfo insertFamily)
        {
            this._familyInfo.FamilyChildren.Add(insertFamily);
            var added = new FamilyViewModel(insertFamily, this, _objFamilyTree);
            this._familyListChildren.Add(added);
            MakeOrderChild();
            return added;
        }

        private void InsertFamilyEmClickFunc()
        {
            TryMoveSiblingOrderDown();
        }

        private void InsertFamilyAnhClickFunc()
        {
            TryMoveSiblingOrderUp();
        }

        /// <summary>Shift+Up — đổi thứ tự lên (anh hơn). Đã là anh cả thì không đổi.</summary>
        public bool TryMoveSiblingOrderUp()
        {
            if (Parent == null)
            {
                return false;
            }

            int indexThis = Parent.Children.IndexOf(this);
            if (indexThis <= 0)
            {
                return false;
            }

            CaptureUndoBefore("Đổi thứ tự anh/em: lên");
            Parent.MoveChildSiblingAt(indexThis, indexThis - 1);
            AddUserAction("Chỉnh lên (anh hơn): " + Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return true;
        }

        /// <summary>Shift+Down — đổi thứ tự xuống (em hơn). Đã là em út thì không đổi.</summary>
        public bool TryMoveSiblingOrderDown()
        {
            if (Parent == null)
            {
                return false;
            }

            int indexThis = Parent.Children.IndexOf(this);
            if (indexThis < 0 || indexThis >= Parent.Children.Count - 1)
            {
                return false;
            }

            CaptureUndoBefore("Đổi thứ tự anh/em: xuống");
            Parent.MoveChildSiblingAt(indexThis, indexThis + 1);
            AddUserAction("Chỉnh xuống (em hơn): " + Name0);
            _objFamilyTree?.OnPropertyChanged("GP");
            return true;
        }

        /// <summary>Đổi vị trí con trong cây — đồng bộ ViewModel và FamilyInfo.</summary>
        private void MoveChildSiblingAt(int fromIndex, int toIndex)
        {
            if (fromIndex < 0
                || toIndex < 0
                || fromIndex >= _familyListChildren.Count
                || toIndex >= _familyListChildren.Count
                || fromIndex == toIndex)
            {
                return;
            }

            _familyListChildren.Move(fromIndex, toIndex);
            _familyInfo.FamilyChildren.Move(fromIndex, toIndex);
            MakeOrderChild();
        }
        private void InsertFamilyClickFunc()
        {
            InsertParentFamilyFromTree();
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
        public override string ToString()
        {
            return Name0 + " ID=" + this._familyInfo.FamilyId + " Up=" + this._familyInfo.FamilyUp;
        }
        public ObservableCollection<FamilyViewModel> Children
        {
            get { return _familyListChildren; }
        }

        public FamilyInfo familyInfo
        {
            get { return _familyInfo; }
        }

        public string Name
        {
            get { return _familyInfo.Name; }
        }

        public string Name0
        {
            get { return _familyInfo.Name0; }
        }

        /// <summary>Đang F2 sửa nhãn trên TreeView.</summary>
        public bool IsTreeLabelEditing
        {
            get { return ReferenceEquals(_treeLabelEditingFamily, this); }
        }

        /// <summary>Chỉ phần tên (không có "đời."); Enter xong Name tự ghép lại đời.</summary>
        public string TreeEditText
        {
            get { return _treeEditText; }
            set
            {
                if (_treeEditText == value)
                {
                    return;
                }

                _treeEditText = value ?? "";
                OnPropertyChanged(nameof(TreeEditText));
            }
        }

        public void BeginTreeLabelEdit()
        {
            if (_treeLabelEditingFamily != null && !ReferenceEquals(_treeLabelEditingFamily, this))
            {
                _treeLabelEditingFamily.CancelTreeLabelEdit();
            }

            _treeLabelEditingFamily = this;
            TreeEditText = _familyInfo.GetTreeEditNameText();
            OnPropertyChanged(nameof(IsTreeLabelEditing));
        }

        public bool CommitTreeLabelEdit()
        {
            if (!IsTreeLabelEditing)
            {
                return false;
            }

            string before = _familyInfo.GetTreeEditNameText();
            string after = (_treeEditText ?? "").Trim();
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                CaptureUndoBefore("Sửa tên trên cây");
                if (_familyInfo.ApplyTreeEditNameText(after))
                {
                    string clanHo = _objFamilyTree?.Family?.GetThuyToClanSurname();
                    _familyInfo.ApplyClanMembershipAfterNameEdit(clanHo ?? "", after);

                    // Gia đình gốc (thủy tổ): đổi tên gia phả theo họ người chính nam
                    if (Parent == null)
                    {
                        _objFamilyTree?.SyncGiaphaNameFromRootThuyTo();
                    }

                    AddUserAction("Sửa tên GD trên cây: " + Name);
                    _objFamilyTree?.OnPropertyChanged("GP");
                    _objFamilyTree?.Family?.SyncGiaDinhTabForFamily(this);
                }
            }

            EndTreeLabelEdit();
            return true;
        }

        public void CancelTreeLabelEdit()
        {
            if (!IsTreeLabelEditing)
            {
                return;
            }

            EndTreeLabelEdit();
        }

        private void EndTreeLabelEdit()
        {
            if (ReferenceEquals(_treeLabelEditingFamily, this))
            {
                _treeLabelEditingFamily = null;
            }

            OnPropertyChanged(nameof(IsTreeLabelEditing));
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

        /// <summary>Mở rộng toàn bộ nhánh con trên TreeView.</summary>
        public void ExpandAllDescendants()
        {
            IsExpanded = true;
            if (_familyListChildren == null)
            {
                return;
            }

            foreach (var child in _familyListChildren)
            {
                child?.ExpandAllDescendants();
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
            //json += this._familyInfo.FamilyId + "," + this._familyInfo.FamilyLevel + "," + this._familyInfo.FamilyOrder + "," + this._familyInfo.FamilyUp + "," + this._familyInfo.FamilyNew;
            json += this._familyInfo.FamilyId + "," + this._familyInfo.FamilyLevel + "," + this._familyInfo.FamilyOrder + "," + this._familyInfo.FamilyUp + "," + this._familyInfo.FamilyNew 
                + ",\"" + _familyInfo.X + "\",\"" + _familyInfo.Y + "\",\"" + _familyInfo.Width + "\",\"" + _familyInfo.Height + "\"";
            if (!string.IsNullOrWhiteSpace(_familyInfo.PhaDoShapeSvgId))
            {
                json += ",\"" + GiaPhaRender.PhaDoSvgCatalog.EscapeJsonString(_familyInfo.PhaDoShapeSvgId) + "\"";
            }

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

        internal int GetMaxFamilyId(int max)
        {
            if(max < _familyInfo.FamilyId)
            {
                max = _familyInfo.FamilyId;
            }
            foreach (var item in this.Children)
            {
                max = item.GetMaxFamilyId(max);
            }
            return max;
        }
    }
}