// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using MahApps.Metro.Controls.Dialogs;

using System.Collections.ObjectModel;
using System.Windows.Data;
using ControlzEx.Theming;
using Microsoft.Win32;
using System.Threading;
using System.Configuration;
using System.IO;
using WpfDraw.Class;
using vngp21.Draw;
using System.Diagnostics;
using System.Windows.Controls;

namespace vietnamgiapha
{
    public class AccentColorMenuData
    {
        public string Name { get; set; }

        public Brush BorderColorBrush { get; set; }

        public Brush ColorBrush { get; set; }

        public AccentColorMenuData()
        {
            this.ChangeAccentCommand = new SimpleCommand<string>(o => true, this.DoChangeTheme);
        }

        public ICommand ChangeAccentCommand { get; }

        protected virtual void DoChangeTheme(string name)
        {
            ThemeManager.Current.ChangeThemeColorScheme(Application.Current, name);
        }
    }

    public class AppThemeMenuData : AccentColorMenuData
    {
        protected override void DoChangeTheme(string name)
        {
            ThemeManager.Current.ChangeThemeBaseColor(Application.Current, name);
        }
    }


    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");
        private MainWindow _mainWindow;
        private readonly IDialogCoordinator _dialogCoordinator;

        System.Threading.Timer myTimer;
        public string defaultSaveFolder = "";
        public string defaultSaveName = "";
        //int _integerGreater10Property = 2;
        //private bool _animateOnPositionChange = true;

        private string _StringAutoNameButton;
        public string StringAutoNameButton
        {
            get
            {
                return _StringAutoNameButton;

            }
            set
            {
                if (_StringAutoNameButton != value)
                {
                    _StringAutoNameButton = value;
                    this.OnPropertyChanged("StringAutoNameButton");
                }
            }

        }

        private string _StringAutoName;
        public string StringAutoName
        {
            get
            {
                return _StringAutoName;

            }
            set
            {
                if (_StringAutoName != value)
                {
                    _StringAutoName = value;
                }
            }

        }

        private ObservableCollection<string> _listStringAutoName;
        public ObservableCollection<string> listStringAutoName
        {
            get
            {
                return this._listStringAutoName;
            }
            set
            {
                if(_listStringAutoName == null)
                {
                    _listStringAutoName = new ObservableCollection<string>();
                }
                _listStringAutoName = value;
                this.OnPropertyChanged("listStringAutoName");
            }
        }


        public void AddUserAction(string action)
        {
            listStringUserAction.Add(DateTime.Now.ToString("HH:mm") + ": " + action);
            this.OnPropertyChanged("listStringUserAction");
        }
        public ObservableCollection<string> listStringUserAction
        {
            get
            {
                return _familyTree.listStringUserAction;
            }
        }

        private GiaPhaViewModel _familyTree;
        public GiaPhaViewModel FamilyTree { 
            get { return _familyTree;  }
            set { _familyTree = value; } 
        }

        public class AccentColorMenuData
        {
            public string Name { get; set; }

            public Brush BorderColorBrush { get; set; }

            public Brush ColorBrush { get; set; }

            public AccentColorMenuData()
            {
                this.ChangeAccentCommand = new SimpleCommand<string>(o => true, this.DoChangeTheme);
            }

            public ICommand ChangeAccentCommand { get; }

            protected virtual void DoChangeTheme(string name)
            {
                ThemeManager.Current.ChangeThemeColorScheme(Application.Current, name);
            }
        }

        public class AppThemeMenuData : AccentColorMenuData
        {
            protected override void DoChangeTheme(string name)
            {
                ThemeManager.Current.ChangeThemeBaseColor(Application.Current, name);
            }
        }

        public ICommand OpenNewFileCommand { get; }
        public ICommand ExitAppCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand SaveAsFileCommand { get; }
        private void OpenNewFileCommandFunc()
        {
            log.Info("OpenNewFileCommandFunc: Lưu file hiện tại... ");
            SaveFileCommandFunc();
            log.Info("OpenNewFileCommandFunc: Mở file mới... ");
            FamilyTree = new GiaPhaViewModel(new GiaphaInfo());
            this.OnPropertyChanged("FamilyTree");
        }
        private void OpenFileCommandFunc()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = ".json";
            openFileDialog.Filter = "JSON files (*.json)|*.json";
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    GiaphaInfo gp = Database.FromJson(openFileDialog.FileName);
                    if (gp != null)
                    {
                        gp.FileName = openFileDialog.FileName;
                        UpdateGiaPha(gp);
                        //FamilyTree = new GiaPhaViewModel(gp);
                        //_mainWindow.UpdateHtmlGiaPha();
                        //this.OnPropertyChanged("FamilyTree");
                        log.Info("OpenFileCommandFunc: Mở file xong: " + openFileDialog.FileName);
                    }
                    else
                    {
                        MessageBox.Show("Lỗi mở file : "+ openFileDialog.FileName, "Có Lỗi");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi mở file : " +ex.Message, "Có Lỗi");
                    log.Error("OpenFileCommandFunc: Lỗi file: " + openFileDialog.FileName ) ;
                    log.Error(ex);
                }
            }
        }
        private void SaveFileCommandFunc()
        {
            if( Database.SaveJson(FamilyTree))
            {
                
                //log.Info("SaveFileCommandFunc: file: " + FamilyTree.GP.FileName);
            }
            else
            {
                MessageBox.Show("Lưu file không được", "ERROR");
            }
        }
        private  void SaveAsFileCommandFunc()
        {
            if (Database.SaveJsonAs(FamilyTree))
            {
                log.Info("SaveFileCommandFunc: file: " + FamilyTree.GP.FileName);
            }
            else
            {
                MessageBox.Show("Lưu file không được", "ERROR");
            }
        }
        public void UpdateGiaPha(GiaphaInfo gp)
        {
            FamilyTree = new GiaPhaViewModel(gp);
            _mainWindow.UpdateHtmlGiaPha();
            
            SaveFileCommandFunc();
            this.OnPropertyChanged("FamilyTree");
        }
        public MainWindowViewModel(IDialogCoordinator dialogCoordinator, MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            this.listStringAutoName = new ObservableCollection<string>();

            this.StringAutoNameButton = "Tự Động Chỉnh";

            //this.listStringAutoName.Add("Nhập tên tộc họ");
            //this.listStringAutoName.Add("Nhập tên tộc họ2");
            //this.listStringAutoName.Add("Nhập tên tộc họ3");
            //this.Title = "Flyout Binding Test";
            this._dialogCoordinator = dialogCoordinator;
            // Register menu command
            this.OpenNewFileCommand = new SimpleCommand<object>(o => true, x => this.OpenNewFileCommandFunc());
            this.OpenFileCommand = new SimpleCommand<object>(o => true, x => this.OpenFileCommandFunc());
            this.SaveFileCommand = new SimpleCommand<object>(o => true, x => this.SaveFileCommandFunc());
            this.SaveAsFileCommand = new SimpleCommand<object>(o => true, x => this.SaveAsFileCommandFunc());
            this.ExitAppCommand = new SimpleCommand<object>(o => true, x => { 
                //Application.Current.Shutdown();
            });
            // create accent color menu items for the demo
            Console.WriteLine(ThemeManager.Current.Themes.Count);
            this.AccentColors = ThemeManager.Current.Themes
                                            .GroupBy(x => x.ColorScheme)
                                            .OrderBy(a => a.Key)
                                            .Select(a => new AccentColorMenuData { Name = a.Key, ColorBrush = a.First().ShowcaseBrush })
                                            .ToList();

            // create metro theme color menu items for the demo
            this.AppThemes = ThemeManager.Current.Themes
                                         .GroupBy(x => x.BaseColorScheme)
                                         .Select(x => x.First())
                                         .Select(a => new AppThemeMenuData { Name = a.BaseColorScheme, BorderColorBrush = a.Resources["MahApps.Brushes.ThemeForeground"] as Brush, ColorBrush = a.Resources["MahApps.Brushes.ThemeBackground"] as Brush })
                                         .ToList();

            this.ThemeResources = new ObservableCollection<ThemeResource>();
            var view = CollectionViewSource.GetDefaultView(this.ThemeResources);
            view.SortDescriptions.Add(new SortDescription(nameof(ThemeResource.Key), ListSortDirection.Ascending));
            this.UpdateThemeResources();
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            defaultSaveFolder = config.AppSettings.Settings["defaultSaveFolder"].Value;
            defaultSaveName = config.AppSettings.Settings["defaultSaveName"].Value;
            log.Info("Folder: " + defaultSaveFolder);
            log.Info("File : " + defaultSaveName);
            FamilyTree = new GiaPhaViewModel(new GiaphaInfo());
            myTimer = new System.Threading.Timer(timer_Elapsed, null, 0, Timeout.Infinite);
            _graphData = new GraphData();
            _ItemsLog.Add("Start App");
        }
        private List<string> _ItemsLog = new List<string>();
        public List<String> ItemsLogs
        {
            get { 
                return _ItemsLog; 
            }
        }
        public void AddItemLogs(string str)
        {
            ItemsLogs.Add(str);
            this.OnPropertyChanged(nameof(ItemsLogs));
        }
        void timer_Elapsed(object state)
        {
            Thread.Sleep(15000);
            if (!Directory.Exists(defaultSaveFolder))
            {
                log.Info("Auto Tạo Thư mục: " + defaultSaveFolder);
                Directory.CreateDirectory(defaultSaveFolder);
            }
            if (!Directory.Exists(defaultSaveFolder + "\\backup"))
            {
                log.Info("Auto Tạo Thư mục backup: " + defaultSaveFolder + "\\backup");
                Directory.CreateDirectory(defaultSaveFolder + "\\backup");
            }

            if (FamilyTree.GP.FileName.Length == 0)
            {
                FamilyTree.GP.FileName = defaultSaveFolder + "\\" + defaultSaveName;
                log.Info("Auto Save File : " + FamilyTree.GP.FileName);
            }
            else
            {
                // Backup save

                // Delete file < 
                string fileSearch = FamilyTree.GP.GiaphaName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyy_dd_MM_") + "*.json";
                var directory = new DirectoryInfo(defaultSaveFolder + "\\backup");
                var query = directory.GetFiles(fileSearch, SearchOption.AllDirectories);
                string lastFile = "";
                foreach (var file in query.OrderByDescending(file => file.CreationTime))
                {
                    // last file 
                    lastFile = file.Name;
                    log.Info("Last File : " + lastFile);
                    break;
                }
                foreach (var file in query.OrderByDescending(file => file.CreationTime).Skip(10))
                {
                    // Deleet all, keep last 10 files
                    file.Delete();
                }
                if(lastFile.Length> 0)
                {
                    try
                    {
                        long length0 = new System.IO.FileInfo(FamilyTree.GP.FileName).Length;
                        long length1 = new System.IO.FileInfo(defaultSaveFolder + "\\backup\\" + lastFile).Length;
                        if (length0 != length1)
                        {
                            log.Info("Backup Last File 1: " + length0 + " " + length1 + " " + lastFile);
                            System.IO.File.Copy(FamilyTree.GP.FileName, defaultSaveFolder + "\\backup\\" + FamilyTree.GP.Username.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyy_dd_MM_hh_mm_ss_") + defaultSaveName);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error("Backup " + ex.Message);
                        log.Error(ex);
                    }
                }
                else {
                    //log.Info("Backup Last File 2: " + lastFile);
                    if(FamilyTree.GP.Username.Length> 0)
                    {

                        System.IO.File.Copy(FamilyTree.GP.FileName, defaultSaveFolder + "\\backup\\" + FamilyTree.GP.Username.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyy_dd_MM_hh_mm_ss_") + defaultSaveName);
                    }
                }
            }
            //
            SaveFileCommandFunc();
            //
            myTimer.Change(100, Timeout.Infinite);
        }

        public void Dispose()
        {
            //HotkeyManager.Current.Remove("demo");
        }

        public string Title { get; set; }

        public List<AccentColorMenuData> AccentColors { get; set; }

        public List<AppThemeMenuData> AppThemes { get; set; }

        public List<CultureInfo> CultureInfos { get; set; }

        private CultureInfo currentCulture = CultureInfo.CurrentCulture;

        public CultureInfo CurrentCulture
        {
            get => this.currentCulture;
            set => this.Set(ref this.currentCulture, value);
        }

        public ObservableCollection<ThemeResource> ThemeResources { get; }
        
        public void UpdateThemeResources()
        {
            this.ThemeResources.Clear();

            if (Application.Current.MainWindow != null)
            {
                var theme = ThemeManager.Current.DetectTheme(Application.Current.MainWindow);
                //var theme = ThemeManager.Current.GetTheme("Light", "Blue");
                if (theme == null)
                {
                    return;
                }
                var libraryTheme = theme.LibraryThemes.FirstOrDefault(x => x.Origin == "MahApps.Metro");
                var resourceDictionary = libraryTheme.Resources.MergedDictionaries.FirstOrDefault();

                if (resourceDictionary != null)
                {
                    foreach (var dictionaryEntry in resourceDictionary.OfType<DictionaryEntry>())
                    {
                        this.ThemeResources.Add(new ThemeResource(theme, libraryTheme, resourceDictionary, dictionaryEntry));
                    }
                }
            }
        }


        #region DRAW
        public Rect _bounds;
        public Rect bounds { get
            {
                return _bounds;
            }
            set
            {
                if (_bounds != value)
                {
                    _bounds = value;
                }
            }
        }
        public string WidthHeight
        {
            get {
                return (int)_graphData.maxWidth + " x " + (int)(_graphData.NodeHeight * _graphData.HEIGHT_LENGTH);
            }
        }
        private GraphData _graphData;
        public GraphData data
        {
            get { return _graphData; }
        }
        public void Draw(System.Windows.Controls.Canvas theCanvas)
        {
            theCanvas.Width = 100;
            theCanvas.Height = 100;

            _graphData.Unlink(theCanvas);
            theCanvas.Children.Clear();
            _graphData.listSharp.Clear();
            _graphData.listLayer.Clear();

            _graphData.dicNode.Clear();
            //
            BuildGraphData(_graphData, FamilyTree.FamilyViewModelRoot, 1);
            // Render data
            // Console.WriteLine(dicNode.Count);
            
            // MaxWidth - Do render
            if( _graphData.AUTO_SIZE==true)
            {
                // Tim vị trí nhiều nhất, level + maxwidth của bản đồ
                _graphData.CalculateDicNode();
                // Reset X Y to 0 + MARGIN
                foreach (var level in _graphData.dicNode.Keys)
                {
                    int i = 1;
                    foreach (var node in _graphData.dicNode[level])
                    {
                        //Node node = (Node)_graphData.dicNode[level][i];
                        // Check node have child or not
                        if(node.familyViewModel.Children.Count > 0)
                        {
                            node.orderInSameLevel = i;
                            i++;
                        }
                        node.p.X = 0;
                        node.p.Y = 0;
                    }
                    //int maxOrderInsameLelel = (i-1)>0? (i - 1):1;
                    int maxOrderInsameLelel = i;
                    foreach (var node in _graphData.dicNode[level])
                    {
                        node.maxOrderInsameLelel = maxOrderInsameLelel;
                    }
                }
                // Reset X Y to 0
                double HEIGHT = _graphData.HEIGHT_LENGTH;
                double MARGIN_WIDTH = _graphData.MARGIN_WIDTH;
                double lastX = 0, lastY = 0;
                foreach (var level in _graphData.dicNode.Keys)
                {
                    int indexCount = 0;
                    lastX = 0;
                    foreach (var node in _graphData.dicNode[level])
                    {
                        indexCount++;

                        node.p.X = lastX + node.p.X + node.width + MARGIN_WIDTH;
                        if (level == 1)
                        {
                            node.p.Y = 40;
                        }
                        else
                        {
                            node.p.Y = HEIGHT * (level-1);
                        }

                        lastX = node.p.X;
                        lastY = node.p.Y;
                        //
                    }
                }

                // Tu vi tri maxAtLevel - Kéo lên phía trên
                // Cân đối cây
                for (int key= _graphData.maxAtLevel-1; key > 1; key--)
                {
                    //FamilyViewModel nodeFarent = null;
                    //double startX = 0;
                    // Bắt đầu xử lý ở đời cha - trên node Max
                    int colorIndex = 1;
                    foreach (var node in _graphData.dicNode[key])
                    {
                        //node.maxOrderInsameLelel = 0;

                        int childCount = node.familyViewModel.Children.Count;
                        if (childCount > 0)
                        {
                            // Có con
                            FamilyViewModel gd1 = node.familyViewModel.Children[0];
                            FamilyViewModel gd2 = node.familyViewModel.Children[childCount-1];
                            if (childCount == 1)
                            {
                                // Node cha
                                node.p.X = gd1.Node.p.X;
                            }
                            else
                            {
                                // Node cha
                                node.p.X = (gd1.Node.p.X + gd2.Node.p.X)/2;
                            }
                            colorIndex = (colorIndex + 1) % 2;
                            foreach (var childFamily in node.familyViewModel.Children)
                            {
                                // Chỉnh màu của từng nhóm con
                                childFamily.Node.color = colorIndex;
                            }
                        }
                        //         C5
                        // 1 2 3 4 5 6 7 8 9
                        // Xu ly vi tri của node cha
                    }
                    //Sau khi chỉnh node cha xong, chỉnh phía node anh em của đời cha -
                    for (int i = 1; i < _graphData.dicNode[key].Count; i++)
                    {
                        Node em = _graphData.dicNode[key][i - 1];
                        Node anh = _graphData.dicNode[key][i];
                        if (anh.p.X < em.p.X + em.width)
                        {
                            // Chỉnh node anh lại
                            anh.p.X = em.p.X + em.width + data.MARGIN_WIDTH;
                        }
                    }

                    // ===========================================================
                    // Xử lý các cái box - Cha con
                    // ===========================================================
                    bool startBox = false;
                    int LevelInBoxIndex = 0;
                    double rect1_XMIN = 0;
                    double rect1_XMAX = 0;
                    List<FamilyViewModel> listFamilyInbox = new List<FamilyViewModel> ();
                    //FamilyViewModel familyStartBox = null;

                    for (int i = 0; i < _graphData.dicNode[key].Count; i++)
                    {
                        // Thông tin node thứ i
                        Node node = _graphData.dicNode[key][i];
                        if (startBox == false)
                        {
                            int childCount = node.familyViewModel.Children.Count;
                            // Tìm vị trí box đầu tiền
                            if (childCount > 0)
                            {
                                // Start BOX
                                LevelInBoxIndex = 1;
                                node.orderInSameLevel = LevelInBoxIndex;
                                LevelInBoxIndex++;
                                startBox = true;
                                listFamilyInbox.Clear();
                                listFamilyInbox.Add(node.familyViewModel);

                                //familyStartBox = node.familyViewModel;
                                //==============================================================================
                                // Rect đầu : min của node child 0, và node cha
                                rect1_XMIN = Math.Min(node.p.X, node.familyViewModel.Children[0].Node.p.X);
                                //  X0
                                //              ChaX  Witth
                                //  Con0X       ConNWidth
                                //                       X1
                                double maxXNodeCon = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                        node.familyViewModel.Children[childCount - 1].Node.width;
                                // Lấy điểm cuối box là node X của cha
                                rect1_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon);
                                //==============================================================================
                                log.Info("1 BEGIN BOX: " + node.familyViewModel.Name0);
                                bool isEndNode = true;
                                for (int k = i + 1; k < _graphData.dicNode[key].Count; k++)
                                {
                                    Node nn = _graphData.dicNode[key][k];
                                    if (nn.familyViewModel.Children.Count > 0)
                                    {
                                        isEndNode = false;
                                        break;
                                        //
                                    }
                                }
                                log.Info("   1 Check Endnode: " + node.familyViewModel.Name0 + " " + isEndNode);
                                //if (i == _graphData.dicNode[key].Count - 1)
                                if (isEndNode == true)
                                {
                                    //Close box -
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    // Set cac value con lai
                                    //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                    // Set cac value con lai
                                    foreach (var fami in listFamilyInbox)
                                    {
                                        fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                        log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                    }
                                    LevelInBoxIndex = 1;
                                    //
                                }
                            }
                        }
                        else
                        {
                            // Có startBox tìm EndBox
                            double rect2_XMIN = node.p.X;
                            double rect2_XMAX = node.p.X + node.width;
                            int childCount = node.familyViewModel.Children.Count;
                            if (childCount >0 )
                            {
                                rect2_XMIN = Math.Min(node.p.X, node.familyViewModel.Children[0].Node.p.X);
                                rect2_XMAX = Math.Max(node.p.X, node.familyViewModel.Children[childCount-1].Node.p.X);
                                // Check 2 hình chữ nhật có điểm giao hay không?
                                //
                                if (rect1_XMAX < rect2_XMIN)
                                {
                                    // END BOX - TÍNH LẠI 
                                    startBox = false;
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    // Set cac value con lai
                                    foreach( var fami in listFamilyInbox)
                                    {
                                        fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                    }

                                    //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                    // Start new box ---
                                    // Tìm vị trí box đầu tiền
                                    if (childCount > 0)
                                    {
                                        // Start BOX
                                        LevelInBoxIndex = 1;
                                        node.orderInSameLevel = LevelInBoxIndex;
                                        LevelInBoxIndex++;
                                        startBox = true;
                                        listFamilyInbox.Clear();
                                        listFamilyInbox.Add(node.familyViewModel);
                                        log.Info("11 CLOSE BOX: " + node.familyViewModel.Name0);

                                        //familyStartBox = node.familyViewModel;
                                        //==============================================================================
                                        // Rect đầu : min của node child 0, và node cha
                                        rect1_XMIN = Math.Min(node.p.X, node.familyViewModel.Children[0].Node.p.X);
                                        //  X0
                                        //              ChaX  Witth
                                        //  Con0X       ConNWidth
                                        //                       X1
                                        double maxXNodeCon = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                                node.familyViewModel.Children[childCount - 1].Node.width;
                                        // Lấy điểm cuối box là node X của cha
                                        rect1_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon);
                                        //==============================================================================
                                        log.Info("1 BEGIN BOX: " + node.familyViewModel.Name0);
                                        bool isEndNode = true;
                                        for (int k = i + 1; k < _graphData.dicNode[key].Count; k++)
                                        {
                                            Node nn = _graphData.dicNode[key][k];
                                            if (nn.familyViewModel.Children.Count > 0)
                                            {
                                                isEndNode = false;
                                                break;
                                                //
                                            }
                                        }
                                        log.Info("   1 Check Endnode: " + node.familyViewModel.Name0 + " " + isEndNode);
                                        //if (i == _graphData.dicNode[key].Count - 1)
                                        if (isEndNode == true)
                                        {
                                            //Close box -
                                            node.orderInSameLevel = LevelInBoxIndex;
                                            LevelInBoxIndex++;
                                            // Set cac value con lai
                                            //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                            // Set cac value con lai
                                            foreach (var fami in listFamilyInbox)
                                            {
                                                fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                                log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                            }
                                            LevelInBoxIndex = 1;
                                            //
                                        }

                                    }
                                }
                                else
                                {
                                    // Update End box X1
                                    double maxXNodeCon = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                            node.familyViewModel.Children[childCount - 1].Node.width;
                                    // Lấy điểm cuối box là node X của cha
                                    rect1_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon);
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    listFamilyInbox.Add(node.familyViewModel);

                                    //==============================================================================
                                    bool isEndNode = true;
                                    for (int k = i + 1; k < _graphData.dicNode[key].Count; k++)
                                    {
                                        Node nn = _graphData.dicNode[key][k];
                                        if (nn.familyViewModel.Children.Count > 0)
                                        {
                                            isEndNode = false;
                                            break;
                                            //
                                        }
                                    }
                                    log.Info("   11 Check Endnode: " + node.familyViewModel.Name0 + " " + isEndNode);
                                    //if (i == _graphData.dicNode[key].Count - 1)
                                    if (isEndNode == true)
                                    {
                                        //Close box -
                                        node.orderInSameLevel = LevelInBoxIndex;
                                        LevelInBoxIndex++;
                                        // Set cac value con lai
                                        //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                        // Set cac value con lai
                                        foreach (var fami in listFamilyInbox)
                                        {
                                            fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                            log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                        }
                                        LevelInBoxIndex = 1;
                                        //
                                    }
                                    //
                                }
                                // END: Check 2 hình chữ nhật có điểm giao hay không?
                            }
                        }
                    }
                    // ===========================================================
                    // Xử lý các cái box - Cha con - END
                    // ===========================================================
                    // Update Max Index
                    // End BOX
                }

                // Từ vị trí MaxLevel kéo xuống dưới -Cân đối cây
                // Can đối với MaxWidth
                //  Parent -                     1 2 3 4
                // Children -                          
                for (int key = _graphData.maxAtLevel + 1; key <= _graphData.dicNode.Count; key++)
                {
                    //double endXpos = 0;
                    FamilyViewModel lastParent = null;
                    int colorIndex = 1;
                    double totalWidthAtLevel = 0;
                    double startXFirst = 0;
                    foreach (var node in _graphData.dicNode[key])
                    {
                        if (startXFirst == 0)
                        {
                            startXFirst = node.familyViewModel.Parent.Node.p.X;
                        }
                        totalWidthAtLevel += node.width + data.MARGIN_WIDTH;
                    }
                    double startX = (_graphData.maxWidth - totalWidthAtLevel) / 2;
                    //
                    foreach (var node in _graphData.dicNode[key])
                    {
                        //node.p.X = startX + node.width;
                    }
                    double endXpos = 0;
                    foreach (var node in _graphData.dicNode[key])
                    {
                        // node cha
                        FamilyViewModel parent = node.familyViewModel.Parent;
                        if (lastParent != parent)
                        {
                            // Đổi màu các đàn con
                            colorIndex = (colorIndex + 1) % 2;
                            node.color = colorIndex;
                            lastParent = parent;
                        }
                        else
                        {
                            // Đổi màu các đàn con
                            node.color = colorIndex;
                        }
                        if (startXFirst < startX)
                        {
                            //
                            Node parentNode = parent.Node;
                            if (endXpos < parentNode.p.X)
                            {
                                endXpos = parentNode.p.X;
                            }
                            // Duyet qua list gia đình con
                            foreach (var childFamily in parent.Children)
                            {
                                if (childFamily == node.familyViewModel)
                                {
                                    childFamily.Node.p.X = endXpos + data.MARGIN_WIDTH;
                                    endXpos = childFamily.Node.p.X + childFamily.Node.width;
                                    break;
                                }
                            }
                            // Set X of node
                        }
                        else
                        {
                            // Can doi cay don gian
                            node.p.X = startX + node.width;
                        }

                    }
                }
                for (int key = _graphData.maxAtLevel; key <= _graphData.dicNode.Count; key++)
                {
                    // ===========================================================
                    // Xử lý các cái box - Cha con
                    // ===========================================================
                    for (int i = 1; i < _graphData.dicNode[key].Count; i++)
                    {
                        Node em = _graphData.dicNode[key][i - 1];
                        Node anh = _graphData.dicNode[key][i];
                        if (anh.p.X < em.p.X + em.width + data.MARGIN_WIDTH)
                        {
                            // Chỉnh node anh lại
                            anh.p.X = em.p.X + em.width + data.MARGIN_WIDTH;
                        }
                    }
                    // ===========================================================
                    // Xử lý các cái box - Cha con
                    // ===========================================================
                    bool startBox = false;
                    int LevelInBoxIndex = 0;
                    double rect1_XMIN = 0;
                    double rect1_XMAX = 0;
                    List<FamilyViewModel> listFamilyInbox = new List<FamilyViewModel>();
                    //FamilyViewModel familyStartBox = null;

                    for (int i = 0; i < _graphData.dicNode[key].Count; i++)
                    {
                        // Thông tin node thứ i
                        Node node = _graphData.dicNode[key][i];
                        if (startBox == false)
                        {
                            int childCount = node.familyViewModel.Children.Count;
                            // Tìm vị trí box đầu tiền
                            if (childCount > 0)
                            {
                                // Start BOX
                                LevelInBoxIndex = 1;
                                node.orderInSameLevel = LevelInBoxIndex;
                                LevelInBoxIndex++;
                                startBox = true;
                                listFamilyInbox.Clear();
                                listFamilyInbox.Add(node.familyViewModel);
                                log.Info("BEGIN BOX: " + node.familyViewModel.Name0);
                                //familyStartBox = node.familyViewModel;
                                //==============================================================================
                                // Rect đầu : min của node child 0, và node cha
                                rect1_XMIN = Math.Min(node.p.X, node.familyViewModel.Children[0].Node.p.X);
                                //  X0
                                //              ChaX  Witth
                                //  Con0X       ConNWidth
                                //                       X1
                                double maxXNodeCon = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                        node.familyViewModel.Children[childCount - 1].Node.width;
                                // Lấy điểm cuối box là node X của cha
                                rect1_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon);
                                //==============================================================================
                                //==============================================================================
                                // Check nếu là node cuối cùng của nhánh
                                bool isEndNode = true;
                                for (int k = i + 1; k < _graphData.dicNode[key].Count; k++)
                                {
                                    Node nn = _graphData.dicNode[key][k];
                                    if (nn.familyViewModel.Children.Count > 0)
                                    {
                                        isEndNode = false;
                                        break;
                                        //
                                    }
                                }
                                //if (i == _graphData.dicNode[key].Count - 1)
                                log.Info("        Check EndNode: " + node.familyViewModel.Name0 +" "+ isEndNode);
                                if (isEndNode == true)
                                {
                                    //Close box -
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    // Set cac value con lai
                                    //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                    // Set cac value con lai
                                    foreach (var fami in listFamilyInbox)
                                    {
                                        fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                        log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                    }
                                    LevelInBoxIndex = 1;
                                    //
                                }
                            }
                        }
                        else
                        {
                            // Có startBox tìm EndBox
                            double rect2_XMIN = node.p.X;
                            double rect2_XMAX = node.p.X + node.width;
                            int childCount = node.familyViewModel.Children.Count;
                            if (childCount > 0)
                            {

                                rect2_XMIN = Math.Min(node.p.X, node.familyViewModel.Children[0].Node.p.X);
                                double maxXNodeCon1 = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                        node.familyViewModel.Children[childCount - 1].Node.width;
                                rect2_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon1);
                                // Check 2 hình chữ nhật có điểm giao hay không?
                                //
                                if (rect1_XMAX < rect2_XMIN)
                                {
                                    // END BOX - TÍNH LẠI 
                                    startBox = false;
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    // Set cac value con lai
                                    foreach (var fami in listFamilyInbox)
                                    {
                                        fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                        log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                    }
                                    //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                    // Start new box ---
                                    // Tìm vị trí box đầu tiền
                                    // Start BOX
                                    LevelInBoxIndex = 1;
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    startBox = true;
                                    listFamilyInbox.Clear();
                                    listFamilyInbox.Add(node.familyViewModel);
                                    //familyStartBox = node.familyViewModel;
                                    //==============================================================================
                                    // Rect đầu : min của node child 0, và node cha
                                    rect1_XMIN = Math.Min(node.p.X, node.familyViewModel.Children[0].Node.p.X);
                                    //  X0
                                    //              ChaX  Witth
                                    //  Con0X       ConNWidth
                                    //                       X1
                                    double maxXNodeCon = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                            node.familyViewModel.Children[childCount - 1].Node.width;
                                    // Lấy điểm cuối box là node X của cha
                                    rect1_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon);
                                    //==============================================================================
                                    // Check nếu là node cuối cùng của nhánh
                                    log.Info("1 BEGIN BOX: " + node.familyViewModel.Name0);
                                    bool isEndNode = true;
                                    for(int k=i+1; k< _graphData.dicNode[key].Count; k++)
                                    {
                                        Node nn = _graphData.dicNode[key][k];
                                        if( nn.familyViewModel.Children.Count>0)
                                        {
                                            isEndNode = false;
                                            break;
                                            //
                                        }
                                    }
                                    log.Info("   1 Check Endnode: " + node.familyViewModel.Name0 + " " + isEndNode);
                                    //if (i == _graphData.dicNode[key].Count - 1)
                                    if (isEndNode==true)
                                    {
                                        //Close box -
                                        node.orderInSameLevel = LevelInBoxIndex;
                                        LevelInBoxIndex++;
                                        // Set cac value con lai
                                        //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                        // Set cac value con lai
                                        foreach (var fami in listFamilyInbox)
                                        {
                                            fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                            log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                        }
                                        LevelInBoxIndex = 1;
                                        //
                                    }
                                }
                                else
                                {
                                    // Update End box X1
                                    double maxXNodeCon = node.familyViewModel.Children[childCount - 1].Node.p.X +
                                                            node.familyViewModel.Children[childCount - 1].Node.width;
                                    // Lấy điểm cuối box là node X của cha
                                    rect1_XMAX = Math.Max(node.p.X + node.width, maxXNodeCon);
                                    node.orderInSameLevel = LevelInBoxIndex;
                                    LevelInBoxIndex++;
                                    listFamilyInbox.Add(node.familyViewModel);
                                    //
                                    // Check nếu là node cuối cùng của nhánh
                                    log.Info("2 BEGIN BOX: " + node.familyViewModel.Name0);
                                    bool isEndNode = true;
                                    for (int k = i + 1; k < _graphData.dicNode[key].Count; k++)
                                    {
                                        Node nn = _graphData.dicNode[key][k];
                                        if (nn.familyViewModel.Children.Count > 0)
                                        {
                                            isEndNode = false;
                                            break;
                                            //
                                        }
                                    }
                                    log.Info("   2 Check Endnode: " + node.familyViewModel.Name0 + " " + isEndNode);
                                    //if (i == _graphData.dicNode[key].Count - 1)
                                    if (isEndNode == true)
                                    {
                                        //Close box -
                                        node.orderInSameLevel = LevelInBoxIndex;
                                        LevelInBoxIndex++;
                                        // Set cac value con lai
                                        //familyStartBox.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                        // Set cac value con lai
                                        foreach (var fami in listFamilyInbox)
                                        {
                                            fami.Node.maxOrderInsameLelel = LevelInBoxIndex;
                                            log.Info("--------" + fami.Node.ToString() + " " + fami.Node.orderInSameLevel + " / " + fami.Node.maxOrderInsameLelel);
                                        }
                                        LevelInBoxIndex = 1;
                                        //
                                    }
                                    //
                                }
                                // END: Check 2 hình chữ nhật có điểm giao hay không?
                            }
                        }
                    }
                    // ===========================================================
                    // Xử lý các cái box - Cha con - END
                    // ===========================================================
                }
                // Auto điều chỉnh --> cho các hình ảnh ko đè lên nhau
                //foreach (var key in _graphData.dicNode.Keys)
                //{
                //    for (int i = 1; i < _graphData.dicNode[key].Count; i++)
                //    {
                //        Node em = _graphData.dicNode[key][i - 1];
                //        Node anh = _graphData.dicNode[key][i];
                //        if (anh.p.X < em.p.X + em.width + data.MARGIN_WIDTH)
                //        {
                //            // Chỉnh node anh lại
                //            anh.p.X = em.p.X + em.width + data.MARGIN_WIDTH;
                //        }
                //    }
                //}
                // Auto điều chỉnh phía dưới MAX_WIDTH --> Tính toán số đường cắt
            }

            _graphData.maxWidth = 0;
            foreach (var level in _graphData.dicNode.Keys)
            {
                LayerLevel layer = new LayerLevel(_graphData.dicNode[level][0].p.X, _graphData.dicNode[level][0].p.Y);
                layer._objGraphData = _graphData;
                layer.level = level;
                layer.height = _graphData.dicNode[level][0].height ;
                _graphData.AddLayer(layer);
                //
                Node end = _graphData.dicNode[level][_graphData.dicNode[level].Count - 1];
                if(_graphData.maxWidth < end.p.X + end.width)
                {
                    _graphData.maxWidth = end.p.X + end.width;
                    _graphData.maxAtLevel = level;
                    _graphData.maxWidthCount = _graphData.dicNode.Count;
                }
            }
            //
            //theCanvas.SizeChanged -= TheCanvas_SizeChanged;
            //theCanvas.Loaded -= TheCanvas_Loaded;

            _graphData.Draw(theCanvas);

            //theCanvas.SizeChanged += TheCanvas_SizeChanged;
            //theCanvas.Loaded += TheCanvas_Loaded;
            //theCanvas.ManipulationCompleted += TheCanvas_ManipulationCompleted;
            //theCanvas.DataContextChanged += TheCanvas_DataContextChanged;
            //theCanvas.SourceUpdated += TheCanvas_SourceUpdated;
            //Thread.Sleep(1000);

            //bounds = VisualTreeHelper.GetDescendantBounds(theCanvas);
            //_bounds.Width = Math.Round(_graphData.maxWidth, 0);
            //_bounds.Height = Math.Round(_graphData.HEIGHT_LENGTH * _graphData.dicNode.Count, 0); //+ _graphData.HEIGHT_LENGTH;
            //theCanvas.Width = _bounds.Width;
            //theCanvas.Height = _bounds.Height;
            //int key1 = _graphData.maxAtLevel;
            //key <= _graphData.dicNode.Count
            if (_graphData.maxWidth < 1024)
            {
                theCanvas.Width = 1024;
            }
            else
            {
                theCanvas.Width = _graphData.maxWidth;
            }
            theCanvas.Height = _graphData.HEIGHT_LENGTH * _graphData.dicNode.Count;

            this.OnPropertyChanged("bounds");
            this.OnPropertyChanged("theCanvas");
            //
        }

        private void TheCanvas_SourceUpdated(object sender, DataTransferEventArgs e)
        {
            Console.WriteLine("TheCanvas_SourceUpdated");
        }

        private void TheCanvas_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            Console.WriteLine("TheCanvas_DataContextChanged");
        }

        private void TheCanvas_ManipulationCompleted(object sender, ManipulationCompletedEventArgs e)
        {
            Console.WriteLine("TheCanvas_ManipulationCompleted");
        }

        private void TheCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("TheCanvas_Loaded");
        }

        private void TheCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Console.WriteLine("TheCanvas_SizeChange");
            //Canvas theCanvas = (Canvas)sender;
            //bounds = VisualTreeHelper.GetDescendantBounds(theCanvas);
            //_bounds.Width = Math.Round(bounds.Width, 0);
            //_bounds.Height = Math.Round(bounds.Height, 0);
            //theCanvas.Width = _bounds.Width;
            //theCanvas.Height = _bounds.Height;

            //this.OnPropertyChanged("bounds");
            //this.OnPropertyChanged("theCanvas");
        }

        private void BuildGraphData(GraphData data, FamilyViewModel familyRoot, int level)
        {
            try
            {
                if (_graphData.dicNode.ContainsKey(level) == false)
                {
                    _graphData.dicNode.Add(level, new List<Node>());
                }
                Node testNode = familyRoot.Node;
                testNode.SelectedNodeEvent += TestNode_SelectedNodeEvent;
                //testNode.p.X = 0;
                //testNode.p.Y = 0;
                data.AddSharp(testNode);
                _graphData.dicNode[level].Add(testNode);

                if (familyRoot.Parent != null)
                {
                    Link lnk = new Link(familyRoot.Parent.Node, familyRoot.Node, 0, 0);
                    data.AddSharp(lnk);
                }
                //
                foreach (var f in familyRoot.Children)
                {
                    BuildGraphData(data, f, level + 1);
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void TestNode_SelectedNodeEvent(Node node)
        {
            this.FamilyTree.Family.SelectedFamily = node.familyViewModel;
            this.FamilyTree.Family.SelectedFamily.IsExpanded = true;
            this.FamilyTree.Family.SelectedFamily.IsSelected = true;
        }

        private bool CheckAutoSizeNeed()
        {
            double lastY = 0;

            foreach (var level in _graphData.dicNode.Keys)
            {
                foreach (var node in _graphData.dicNode[level])
                {
                    if (node.p.Y - lastY < _graphData.HEIGHT_LENGTH * level)
                    {
                        _graphData.AUTO_SIZE = true;
                        return _graphData.AUTO_SIZE;
                    }
                    lastY = node.p.Y;
                    break;
                }
            }
            _graphData.AUTO_SIZE = false;
            return false;
        }

        internal void ExportHtml(string fileName)
        {
            string data = "";
            foreach (var level in _graphData.dicNode.Keys)
            {
                foreach (var node in _graphData.dicNode[level])
                {
                    data += "drawNode(ctx, \"" + node.name + "\", "+ (int)node.p.X +"," + (int)node.p.Y + "," + (int)node.width + "," + (int)node.height + "); " + Environment.NewLine;
                }
            }
            System.IO.File.WriteAllText(fileName, data);
        }

        internal void ExportHtmlMxFile()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = this.FamilyTree.GiaphaName;
            saveFileDialog.DefaultExt = ".html";
            saveFileDialog.Filter = "HTML files (*.html)|*.html";
            if (saveFileDialog.ShowDialog() == true)
            {
                if (File.Exists(saveFileDialog.FileName))
                {
                    if (MessageBox.Show("Có muốn lưu đè vào file có sẵn: " + saveFileDialog.FileName,
                        "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                    }
                    else
                    {
                        return;
                    }
                }
                this.FamilyTree.GiaphaWebHtml = saveFileDialog.FileName;
                // Copy file
                if (File.Exists("view-static.min.js"))
                {
                    try
                    {
                        string path = Path.GetDirectoryName(saveFileDialog.FileName);
                        // File copy
                        File.Copy("view-static.min.js", path + "\\view-static.min.js", true);
                    }
                    catch(Exception exx) {
                        log.Error("Có lỗi: " + exx.Message);

                    }
                    //
                    _graphData.ExportHtmlFile(this.FamilyTree.GiaphaName, this.FamilyTree.GiaphaWebHtml);
                    log.Info("Đã xuất ra file : " + this.FamilyTree.GiaphaWebHtml);
                    if (MessageBox.Show("Có muốn mở coi không ??",
                        "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        try
                        {
                            if (Convert.ToInt32(this.FamilyTree.GiaphaId) > 0)
                            {
                                Process.Start(new ProcessStartInfo("file://"+ this.FamilyTree.GiaphaWebHtml));
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }
            else
            {
                log.Info("Hủy chọn được file để lưu.");
            }
            
        }

        internal void ExportDrawioFile()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = this.FamilyTree.GiaphaName;
            saveFileDialog.DefaultExt = ".drawio";
            saveFileDialog.Filter = "drawio files (*.drawio)|*.drawio";
            if (saveFileDialog.ShowDialog() == true)
            {
                if (File.Exists(saveFileDialog.FileName))
                {
                    if (MessageBox.Show("Có muốn lưu đè vào file có sẵn: " + saveFileDialog.FileName,
                        "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                    }
                    else
                    {
                        return;
                    }
                }
                this.FamilyTree.GiaphaDrawIo = saveFileDialog.FileName;
                // Copy file
                string path = Path.GetDirectoryName(saveFileDialog.FileName);
                //
                _graphData.ExportDrawioFile(this.FamilyTree.GiaphaName, this.FamilyTree.GiaphaDrawIo);
                log.Info("Đã xuất ra file : " + this.FamilyTree.GiaphaDrawIo);
                if (MessageBox.Show("Có muốn mở coi không ??",
                    "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    try
                    {
                        MessageBox.Show("1. Từ web diagram.et, mở file có tên là:" + this.FamilyTree.GiaphaDrawIo+ "\n"+
                                "2. Mở trong web đó, edit và in ra giấy cỡ bự nhất ", "Hướng dẫn nhanh");
                        if (Convert.ToInt32(this.FamilyTree.GiaphaId) > 0)
                        {
                            Process.Start(new ProcessStartInfo("https://app.diagrams.net/"));
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                log.Info("Hủy chọn được file để lưu.");
            }

        }
        #endregion DRAW
    }
}