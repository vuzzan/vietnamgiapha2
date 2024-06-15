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

        public ICommand ExitAppCommand { get; }
        public ICommand OpenFileCommand { get; }
        public ICommand SaveFileCommand { get; }
        public ICommand SaveAsFileCommand { get; }
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
                        FamilyTree = new GiaPhaViewModel(gp);
                        _mainWindow.UpdateHtmlGiaPha();
                        this.OnPropertyChanged("FamilyTree");
                        log.Info("Mở file xong: " + openFileDialog.FileName);
                    }
                    else
                    {
                        MessageBox.Show("Lỗi mở file : "+ openFileDialog.FileName, "Có Lỗi");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi mở file : " +ex.Message, "Có Lỗi");
                    log.Error("Lỗi file: " + openFileDialog.FileName ) ;
                    log.Error(ex);
                }
            }
        }
        private  void SaveFileCommandFunc()
        {
            if( Database.SaveJson(FamilyTree))
            {
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
            }
            else
            {
                MessageBox.Show("Lưu file không được", "ERROR");
            }
        }

        public MainWindowViewModel(IDialogCoordinator dialogCoordinator, MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            //this.Title = "Flyout Binding Test";
            this._dialogCoordinator = dialogCoordinator;
            // Register menu command
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
            //GiaphaInfo gp = Database.FromJson("./DataDemo/data_full2.json");
            //if (gp != null)
            //{
            FamilyTree = new GiaPhaViewModel(new GiaphaInfo());
            //}
            //else
            //{
            //    //
            //}



            myTimer = new System.Threading.Timer(timer_Elapsed, null, 0, Timeout.Infinite);
        }
        void timer_Elapsed(object state)
        {
            Thread.Sleep(15000);
            //if (FamilyTree.GP.FileName.Length == 0)
            {
                SaveFileCommandFunc();
            }
                
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