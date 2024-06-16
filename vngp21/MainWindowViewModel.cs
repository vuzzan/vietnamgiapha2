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
using System.Windows.Shapes;

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
                log.Info("SaveFileCommandFunc: file: " + FamilyTree.GP.FileName);
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

            defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
            defaultSaveName = ConfigurationManager.AppSettings["defaultSaveName"];
            log.Info("Folder: " + defaultSaveFolder);
            log.Info("File : " + defaultSaveName);
            FamilyTree = new GiaPhaViewModel(new GiaphaInfo());
            myTimer = new System.Threading.Timer(timer_Elapsed, null, 0, Timeout.Infinite);

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
            Thread.Sleep(5000);
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
                    long length0 = new System.IO.FileInfo(FamilyTree.GP.FileName).Length;
                    long length1 = new System.IO.FileInfo(defaultSaveFolder + "\\backup\\" + lastFile).Length;
                    if( length0 != length1)
                    {
                        log.Info("Backup Last File 1: " + length0+ " " + length1 +" " + lastFile);
                        System.IO.File.Copy(FamilyTree.GP.FileName, defaultSaveFolder + "\\backup\\" + FamilyTree.GP.GiaphaName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyy_dd_MM_hh_mm_ss_") + defaultSaveName);
                    }
                }
                else {
                    log.Info("Backup Last File 2: " + lastFile);
                    System.IO.File.Copy(FamilyTree.GP.FileName, defaultSaveFolder + "\\backup\\" + FamilyTree.GP.GiaphaName.Replace(" ", "_") + "_" + DateTime.Now.ToString("yyyy_dd_MM_hh_mm_ss_") + defaultSaveName);
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

    }
}