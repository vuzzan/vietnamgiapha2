﻿using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;
using System;
using System.Linq;
using log4net.Repository;
using System.IO;
using System.Reflection;
using AutoUpdaterDotNET;
using System.Configuration;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Xml.Linq;
using WpfDraw.Class;
using System.Collections.Generic;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using vngp21.Draw;
using System.Net;
using GalaSoft.MvvmLight.Command;

namespace vietnamgiapha
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");
        private readonly MainWindowViewModel viewModel;
        public MainWindow()
        {
            

            ILoggerRepository repository = log4net.LogManager.GetRepository(Assembly.GetCallingAssembly());
            var fileInfo = new FileInfo(@"log4net.config");
            log4net.Config.XmlConfigurator.Configure(repository, fileInfo);
            //
            InitializeComponent();

            log.Info("Application started...SecurityProtocol ");
            System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 |
            SecurityProtocolType.Tls11 |
            SecurityProtocolType.Tls;
            log.Info("Application started...version END ");
            //
            try
            {
                Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                string path = config.AppSettings.Settings["defaultSaveFolder"].Value;
                FileInfo f = new FileInfo(path);
                string drive = System.IO.Path.GetPathRoot(f.FullName);
                // check drive exist
                if (Directory.Exists(drive))
                {
                    // OK
                }
                else
                {
                    // Else
                    config.AppSettings.Settings["defaultSaveFolder"].Value = path.Replace(drive, "c:\\");
                    config.Save(ConfigurationSaveMode.Modified);
                    //
                    //ConfigurationManager.AppSettings["defaultSaveFolder"] = path.Replace(drive, "c:\\");
                }
            }
            catch (Exception ex)
            {

            }

            string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            log.Info("Application started...version " + ver);
            AutoUpdater.Start("http://download.vietnamgiapha.com/files/autoupdate.xml");
            InitEvents();
            this.Title = this.Title + " - " + ver;
            this.viewModel = new MainWindowViewModel(DialogCoordinator.Instance, this);
            this.DataContext = this.viewModel;

            DeletePersonFromFamilyClick = new RelayCommand(DeletePersonFromFamilyClickFunc);

        }
        public void UpdateHtmlGiaPha()
        {
            if (viewModel != null)
            {
                htmlEditorTocUoc.ContentHtml = viewModel.FamilyTree.Tocuoc;
                htmlEditorPhaKy.ContentHtml = viewModel.FamilyTree.PhaKy;
                htmlEditorHuongHoa.ContentHtml = viewModel.FamilyTree.HuongHoa;
                htmlEditorThuyto.ContentHtml = viewModel.FamilyTree.ThuyTo;

                if (viewModel.FamilyTree.GP.FileName.Length == 0)
                {
                    string defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
                    //viewModel.FamilyTree.GP.FileName = defaultSaveFolder +"\\"+viewModel.FamilyTree.GP.GiaphaName.Replace(" ", "_") + "_" + ".json";
                    viewModel.FamilyTree.GP.FileName = defaultSaveFolder + "\\" + viewModel.FamilyTree.GP.Username.Replace(" ", "_") + "_" + ".json";
                }
            }
        }
        void InitEvents()
        {
            htmlEditorPhaKy.DocumentReady += HtmlEditorPhaKy_DocumentReady;
            htmlEditorHuongHoa.DocumentReady += HtmlEditorHuongHoa_DocumentReady;
            htmlEditorThuyto.DocumentReady += HtmlEditorThuyto_DocumentReady;
            htmlEditorTocUoc.DocumentReady += HtmlEditorTocUoc_DocumentReady;
        }
        private void htmlEditorPhaKy_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.PhaKy = htmlEditorPhaKy.ContentHtml;
        }
        private void htmlEditorHuongHoa_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.HuongHoa    = htmlEditorHuongHoa.ContentHtml;
        }
        private void htmlEditorThuyto_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.ThuyTo = htmlEditorThuyto.ContentHtml;
        }
        private void htmlEditorTocUoc_LostFocus(object sender, RoutedEventArgs e)
        {
            viewModel.FamilyTree.Tocuoc = htmlEditorTocUoc.ContentHtml;
        }

        private void HtmlEditorTocUoc_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorTocUoc.ContentHtml = viewModel.FamilyTree.Tocuoc;
            }
            else
            {
                htmlEditorTocUoc.ContentHtml = "";
            }
        }

        private void HtmlEditorThuyto_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorThuyto.ContentHtml = viewModel.FamilyTree.ThuyTo;
            }
            else
            {
                htmlEditorThuyto.ContentHtml = "";
            }
        }

        private void HtmlEditorHuongHoa_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorHuongHoa.ContentHtml = viewModel.FamilyTree.HuongHoa;
            }
            else
            {
                htmlEditorHuongHoa.ContentHtml = "";
            }
        }

        private void HtmlEditorPhaKy_DocumentReady(object sender, RoutedEventArgs e)
        {
            if (viewModel != null)
            {
                htmlEditorPhaKy.ContentHtml = viewModel.FamilyTree.PhaKy;
            }
            else
            {
                htmlEditorPhaKy.ContentHtml = "";
            }
        }

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
        }

        void searchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                viewModel.FamilyTree.Family.SearchCommand.Execute(null);
            }
        }
        //private double treeViewHorizScrollPos = 0.0;
        //private bool treeViewResetHorizScroll = false;
        //private ScrollViewer treeViewScrollViewer = null;
        private bool mSuppressRequestBringIntoView;

        private void TreeViewItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Ignore re-entrant calls
            if (mSuppressRequestBringIntoView)
                return;

            // Cancel the current scroll attempt
            e.Handled = true;

            // Call BringIntoView using a rectangle that extends into "negative space" to the left of our
            // actual control. This allows the vertical scrolling behaviour to operate without adversely
            // affecting the current horizontal scroll position.
            mSuppressRequestBringIntoView = true;

            TreeViewItem tvi = sender as TreeViewItem;
            if (tvi != null)
            {
                Rect newTargetRect = new Rect(-200, 0, tvi.ActualWidth + 1000, tvi.ActualHeight);
                tvi.BringIntoView(newTargetRect);
            }

            mSuppressRequestBringIntoView = false;
        }

        // Correctly handle programmatically selected items
        private void OnSelected(object sender, RoutedEventArgs e)
        {
            TreeViewItem tvi = ((TreeViewItem)sender);
            tvi.BringIntoView();
            FamilyViewModel personModel = (FamilyViewModel)tvi.DataContext;
            viewModel.FamilyTree.Family.SelectedFamily = personModel;
            log.Info("Chọn trên cây: " + viewModel.FamilyTree.Family.SelectedFamily.Name);
            if (tabControl.SelectedIndex != 1)
            {
                tabControl.SelectedIndex = 1;
            }
            e.Handled = true;
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            TreeViewItem tvi = ((TreeViewItem)sender);
            FamilyViewModel personModel = (FamilyViewModel)tvi.DataContext;
            e.Handled = true;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            
        }
        private void TreeViewItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var treeViewItem = VisualUpwardSearch<TreeViewItem>(e.OriginalSource as DependencyObject) as TreeViewItem;
            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }
        static T VisualUpwardSearch2<T>(DependencyObject source) where T : DependencyObject
        {
            DependencyObject returnVal = source;

            while (returnVal != null && !(returnVal is T))
            {
                DependencyObject tempReturnVal = null;
                if (returnVal is Visual || returnVal is Visual3D)
                {
                    tempReturnVal = VisualTreeHelper.GetParent(returnVal);
                }
                if (tempReturnVal == null)
                {
                    returnVal = LogicalTreeHelper.GetParent(returnVal);
                }
                else returnVal = tempReturnVal;
            }

            return returnVal as T;
        }

        static DependencyObject VisualUpwardSearch<T>(DependencyObject source)
        {
            while (source != null && source.GetType() != typeof(T))
                source = VisualTreeHelper.GetParent(source);

            return source;
        }
        private void Treeview_Family_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Auto select người trong gia đình
            if (viewModel.FamilyTree.Family.SelectedFamily != null )
            {
                //viewModel.FamilyTree.Family.SelectedFamily.DebugFamilyClickFunc();
            }
        }
        private void ListView_ListGiaDinhCon_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (((ListView)sender).SelectedItem==null)
            {
                return;
            }
            viewModel.FamilyTree.Family.SelectedFamily = ((ListView)sender).SelectedItem as FamilyViewModel;
            // Auto select người trong gia đình
            if(viewModel.FamilyTree.Family.SelectedFamily!=null && 
                viewModel.FamilyTree.Family.SelectedFamily.Children.Count > 0)
            {
                viewModel.FamilyTree.Family.SelectedPerson = viewModel.FamilyTree.Family.SelectedFamily.ListPerson.First();
                viewModel.FamilyTree.Family.SelectedFamily.IsExpanded = true;
                viewModel.FamilyTree.Family.SelectedFamily.IsSelected = true;
                log.Info("Chọn người: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void ListView_ListNguoiTrongGiaDinhChaMe_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

            PersonInfo listViewItem = ((ListView)sender).SelectedItem as PersonInfo;
            if (listViewItem != null && viewModel.FamilyTree.Family.SelectedFamily != null)
            {
                viewModel.FamilyTree.Family.SelectedFamily = viewModel.FamilyTree.Family.SelectedFamily.Parent;
                viewModel.FamilyTree.Family.SelectedFamily.IsExpanded = true;
                viewModel.FamilyTree.Family.SelectedFamily.IsSelected = true;
                log.Info("Chọn gia đình cha mẹ: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void ListView_ListNguoiTrongGiaDinh_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            PersonInfo personInfo = ((ListView)sender).SelectedItem as PersonInfo;
            if (personInfo != null)
            {
                viewModel.FamilyTree.Family.SelectedPerson = personInfo;
                viewModel.FamilyTree.Family.SelectedFamily.IsExpanded = true;
                viewModel.FamilyTree.Family.SelectedFamily.IsSelected = true;
                log.Info("Chọn người: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void ListView_ListNguoiTrongGiaDinh_MouseDown_1(object sender, MouseButtonEventArgs e)
        {
            PersonInfo personInfo = ((ListView)sender).SelectedItem as PersonInfo;
            if (personInfo != null)
            {
                viewModel.FamilyTree.Family.SelectedPerson = personInfo;
                log.Info("Chọn người: " + viewModel.FamilyTree.Family.SelectedPerson.MANS_NAME_HUY);
            }
        }

        private void htmlViewer_PhaKy_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            
        }

        private void MetroWindow_Initialized(object sender, EventArgs e)
        {
        }
        
        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            //this.viewModel.AddItemLogs("Begin login");
        }

        private async void BtnDownloadGiaPha_Click(object sender, RoutedEventArgs e)
        {
            if(viewModel.FamilyTree.Username.Trim().Length==0 || viewModel.FamilyTree.Password.Trim().Length == 0)
            {
                MessageBox.Show("Nhập user name và password của trang web vietnamgiapha.com");
                return;
            }
            if (MessageBox.Show("Xác nhận: Download gia phả từ web vietnamgiapha." + Environment.NewLine +
                "Khi download về, sẽ đè bẹp lên gia phả đang làm ?", "Xác nhận", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
            {
                return;
            }
            //_progressDialogController = await _dialogCoordinator.ShowProgressAsync(this, "Please wait", null, true);
            //_progressDialogController.SetIndeterminate();
            //_progressDialogController.Canceled += ProgressDialogControllerCanceled;
            //await GetResultListTask();
            //await _progressDialogController.CloseAsync();
            //_progressDialogController.Canceled -= ProgressDialogControllerCanceled;
            //_progressDialogController = null;
            var _progressDialogController = await this.ShowProgressAsync("Đợi download từ web...", "Đang download từ web vietnamgiapha.com");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            
            try
            {

                //BtnDownloadGiaPha.IsEnabled = false;
                GiaphaInfo gp = await Database.Download(viewModel.FamilyTree.Username.Trim().ToLower(), viewModel.FamilyTree.Password.Trim());
                _progressDialogController.SetProgress(1);
                if (gp != null)
                {
                    viewModel.UpdateGiaPha(gp);
                    //viewModel.FamilyTree = new GiaPhaViewModel(gp);
                    UpdateHtmlGiaPha();
                    //viewModel.FamilyTree.OnPropertyChanged("FamilyTree");
                    
                    log.Info("BtnDownloadGiaPha_Click: download từ web ngon lành ");
                    MessageBox.Show("Download từ web xong", "Ngon lành cành đào");
                    viewModel.AddUserAction("Download gia phả xong, ngon lành");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi download từ web: " + ex.Message, "Có Lỗi");
                log.Error("BtnDownloadGiaPha_Click: Lỗi download từ web");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }

        private async void BtnUploadGiaPha_Click(object sender, RoutedEventArgs e)
        {
            if (viewModel.FamilyTree.Username.Trim().Length == 0 || viewModel.FamilyTree.Password.Trim().Length == 0)
            {
                MessageBox.Show("Nhập user name và password của trang web vietnamgiapha.com");
                return;
            }
            if (viewModel.FamilyTree.CheckValid().Length>0)
            {
                MessageBox.Show(viewModel.FamilyTree.CheckValid(), "Có lỗi");
                return;
            }

            if (viewModel.FamilyTree.GP.GiaphaId == 0)
            {
            }
            var _progressDialogController = await this.ShowProgressAsync("Đợi upload lên web...", "Đang upload từ web vietnamgiapha.com");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();

            try
            {
                //BtnDownloadGiaPha.IsEnabled = false;
                string json = await Database.UploadWeb(
                    viewModel.FamilyTree.Username.Trim().ToLower(), 
                    viewModel.FamilyTree.Password.Trim(),
                    viewModel.FamilyTree.ToJson()
                    );
                JsonObject objData = (JsonObject)JsonObject.Parse(json);
                _progressDialogController.SetProgress(1);
                if ( Convert.ToInt32 (objData["code"].ToString()) == 0)
                {
                    log.Info("BtnUploadGiaPha_Click: upload lên web success: ");
                    //MessageBox.Show(gp, "Upload");
                    // Do reload gia phả
                    GiaphaInfo gp = Database.ParseJsonGiaPha(objData);
                    if (gp != null)
                    {
                        viewModel.UpdateGiaPha(gp);
                        UpdateHtmlGiaPha();
                        log.Info("BtnUploadGiaPha_Click: update lên web ngon lành ");
                        viewModel.AddUserAction("Update gia phả xong, ngon lành");
                        if ( MessageBox.Show("Update lên web xong, muốn coi lại không ???", "Ngon lành cành đào", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            Process.Start(new ProcessStartInfo("https://www.vietnamgiapha.com/XemPhaHe/" + viewModel.FamilyTree.GiaphaId + "/gp.html"));
                        }
                    }
                }
                else
                {
                    //
                    MessageBox.Show("Lỗi upload web: " + Convert.ToInt32(objData["code"].ToString()) + Environment.NewLine +
                        objData["msg"].ToString()
                        , "Có Lỗi");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi upload web: " + ex.Message, "Có Lỗi");
                log.Error("BtnUploadGiaPha_Click: Lỗi upload web: " + ex.Message);
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }

        private void ListViewItem_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.Key == Key.Delete)
                {
                    // XOA
                    if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
                    {
                        PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                        if (MessageBox.Show("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình này ?", "Xác Nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                        {
                            if (obj._familyInfo.ListPerson.IndexOf(obj) > -1)
                            {
                                obj._familyInfo.ListPerson.Remove(obj);
                                obj._familyInfo.OnPropertyChanged("Name");
                                log.Info("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình");
                                viewModel.AddUserAction("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình " + obj._familyInfo.Name0);
                            }
                        }
                    }
                }
                else if ((Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                {

                    if (e.Key == Key.Up)
                    {
                        // SHILF UP//
                        if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
                        {
                            // Chọn người
                            PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                            // XOa người ra khỏi list
                            int index = obj._familyInfo.ListPerson.IndexOf(obj);
                            if (index > 1 && index < obj._familyInfo.ListPerson.Count)
                            {
                                obj._familyInfo.ListPerson.Remove(obj);
                                //Thêm vô phía trên
                                obj._familyInfo.ListPerson.Insert(index - 1, obj);

                                viewModel.AddUserAction("Chỉnh lên trước [" + obj.MANS_NAME_HUY + "] " + obj._familyInfo.Name0);
                            }
                        }
                    }
                    else if (e.Key == Key.Down)
                    {
                        // DOWN
                        if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
                        {
                            // Chọn người
                            PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                            // XOa người ra khỏi list
                            int index = obj._familyInfo.ListPerson.IndexOf(obj);
                            if (index > 0 && index < obj._familyInfo.ListPerson.Count)
                            {
                                obj._familyInfo.ListPerson.Remove(obj);
                                //Thêm vô phía trên
                                obj._familyInfo.ListPerson.Insert(index + 1, obj);

                                viewModel.AddUserAction("Chỉnh xuống dưới [" + obj.MANS_NAME_HUY + "] " + obj._familyInfo.Name0);
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                log.Error("ListViewItem_PreviewKeyDown: " + ex.Message);
                log.Error(ex);
            }
            
        }
        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void Hyperlink_RequestNavigate_1(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(viewModel.FamilyTree.GiaphaId) > 0)
                {
                    Process.Start(new ProcessStartInfo("https://www.vietnamgiapha.com/XemPhaHe/" + viewModel.FamilyTree.GiaphaId + "/gp.html"));
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {

            }
            e.Handled = false;
        }

        private void ToggleSwitch_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            log.Error("OKOK");
        }

        private void ToggleSwitch_DataContextChanged_1(object sender, DependencyPropertyChangedEventArgs e)
        {

        }

        private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            //log.Error("OKOK");
            PersonInfo personInfo = ((ToggleSwitch)sender).DataContext as PersonInfo;
            if (personInfo.IsMainPerson == 0)
            {
                if (personInfo._familyInfo != null && personInfo._familyInfo.ListPerson != null)
                {
                    int countMain = 0;
                    for (int i = 0; i < personInfo._familyInfo.ListPerson.Count; i++)
                    {
                        countMain += personInfo._familyInfo.ListPerson[i].IsMainPerson;
                    }
                    if (countMain == 0)
                    {
                        // NO main person
                        personInfo.IsMainPerson = 1;
                    }
                }
                return;
            }
            if( personInfo._familyInfo!=null && personInfo._familyInfo.ListPerson!=null)
            {
                int countMain = 0;
                foreach (var person in personInfo._familyInfo.ListPerson)
                {
                    if( person.IsMainPerson==1)
                    {
                        countMain++;
                    }
                }

                var list = personInfo._familyInfo.ListPerson.OrderByDescending(v=> v.IsMainPerson).ToList();
                personInfo._familyInfo.ListPerson.Clear();
                for(int i=0;i<list.Count; i++)
                {
                    if( i==0)
                    {
                        personInfo._familyInfo.ListPerson.Add(list[i]);
                    }
                    else
                    {
                        list[i].MANS_GENDER = list[0].IsGioiTinhNam == 1 ? "Nữ" : "Nam";
                        personInfo._familyInfo.ListPerson.Add(list[i]);
                    }
                }
            }

            // "Nữ" + "Nam" 
        }

        private void ToggleSwitch_GioiTinh_Toggled(object sender, RoutedEventArgs e)
        {
            //log.Error("OKOK");
            PersonInfo personInfo = ((ToggleSwitch)sender).DataContext as PersonInfo;
            if (personInfo == null)
            {
                return;
            }
            if (personInfo.IsMainPerson==1 && personInfo._familyInfo != null && personInfo._familyInfo.ListPerson != null)
            {
                for (int i = 1; i < personInfo._familyInfo.ListPerson.Count; i++)
                {
                    personInfo._familyInfo.ListPerson[i].MANS_GENDER = personInfo.IsGioiTinhNam == 1 ? "Nữ" : "Nam";
                }
            }
        }

        private void commandAutoChinh_Click(object sender, RoutedEventArgs e)
        {
            if(viewModel.StringAutoNameButton == "Undo Chỉnh")
            {
                // UNDO
                string defaultSaveFolder = ConfigurationManager.AppSettings["defaultSaveFolder"];
                string undo_filename = defaultSaveFolder + "\\auto_undo.json";
                try
                {
                    GiaphaInfo gp = Database.FromJson(undo_filename);
                    if (gp != null)
                    {
                        viewModel.UpdateGiaPha(gp);
                        log.Info("OpenFileCommandFunc: Mở file UNDO xong: " + undo_filename);
                        MessageBox.Show("Đã UNDO tác vụ chỉnh tự động. Mọi thay đổi đã hoàn tác");
                        viewModel.StringAutoNameButton = "Tự Động Chỉnh";
                    }
                    else
                    {
                        MessageBox.Show("Lỗi mở file : " + undo_filename, "Có Lỗi");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi mở file : " + ex.Message, "Có Lỗi");
                    log.Error("OpenFileCommandFunc: Lỗi file: " + undo_filename);
                    log.Error(ex);
                }
            }
            else
            {
                if (viewModel.StringAutoName != null && viewModel.StringAutoName.Length > 0)
                {
                    viewModel.listStringAutoName.Clear();
                    // Do search
                    // Save undo file
                    Database.SaveJson(viewModel.FamilyTree, "auto_undo.json");
                    string message = "";
                    FamilyViewModel.AutoCorrect(viewModel.FamilyTree.FamilyViewModelRoot, ref message, viewModel.StringAutoName);
                    if (message.Length > 0)
                    {
                        viewModel.listStringAutoName = new ObservableCollection<string>(message.Split(new string[] { Environment.NewLine }, StringSplitOptions.None).ToList());
                        //
                        viewModel.StringAutoNameButton = "Undo Chỉnh";
                        MessageBox.Show("Đã tự động điều chỉnh, kiếm tra nếu có sai nhiều quá, thì bấm [Undo Chỉnh]. Hoặc mở lại file [auto_undo.json]");

                        viewModel.AddUserAction("Chạy xong tự động chỉnh");
                    }
                    else
                    {

                    }
                }
                else
                {
                    viewModel.listStringAutoName.Insert(0, "Có lỗi: Nhập tên tộc họ");
                }
            }
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var _progressDialogController = await this.ShowProgressAsync("Đợi vẽ...", "Đang tính toán và vẽ phả hệ");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            try
            {
                viewModel.Draw(theCanvas);
                viewModel.AddUserAction("Vẽ phả hệ xong..." + viewModel.WidthHeight);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi vẽ phả hệ: " + ex.Message, "Có Lỗi");
                log.Error("vẽ phả hệ");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }
        
        private async void ExportToHhtmlWeb(object sender, RoutedEventArgs e)
        {
            var _progressDialogController = await this.ShowProgressAsync("Đợi...", "Đang tính toán và xuất file WEB");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            try
            {
                _progressDialogController.SetProgress(1);
                viewModel.ExportHtmlMxFile();
                viewModel.AddUserAction("Xuất file web xong..." + viewModel.FamilyTree.GiaphaWebHtml);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi vẽ phả hệ: " + ex.Message, "Có Lỗi");
                log.Error("vẽ phả hệ");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();

        }

        private void Hyperlink_RequestNavigate_2(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(viewModel.FamilyTree.GiaphaId) > 0)
                {
                    Process.Start(new ProcessStartInfo("file://"+ viewModel.FamilyTree.GiaphaWebHtml));
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {

            }
            e.Handled = false;
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            // Check Update 
            AutoUpdater.Start("http://download.vietnamgiapha.com/files/autoupdate.xml");
        }

        private async void ExportToVectorMxFile(object sender, RoutedEventArgs e)
        {
            var _progressDialogController = await this.ShowProgressAsync("Đợi...", "Đang tính toán và xuất file Vector MX");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();
            try
            {
                _progressDialogController.SetProgress(1);
                viewModel.ExportDrawioFile();
                viewModel.AddUserAction("Xuất file mx xong..." + viewModel.FamilyTree.GiaphaDrawIo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi vẽ phả hệ: " + ex.Message, "Có Lỗi");
                log.Error("vẽ phả hệ");
                log.Error(ex);
            }
            //BtnDownloadGiaPha.IsEnabled = true;
            await _progressDialogController.CloseAsync();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

        }
        public ICommand DeletePersonFromFamilyClick { get; set; }
        private void DeletePersonFromFamilyClickFunc()
        {
            // XOA
            if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
            {
                PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                if (MessageBox.Show("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình này ?", "Xác Nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    if (obj._familyInfo.ListPerson.IndexOf(obj) > -1)
                    {
                        obj._familyInfo.ListPerson.Remove(obj);
                        obj._familyInfo.OnPropertyChanged("Name");
                        log.Info("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình");
                        viewModel.AddUserAction("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình " + obj._familyInfo.Name0);
                    }
                }
            }
        }
        //
        private void Button_Click_Add_Person(object sender, RoutedEventArgs e)
        {
            if (ListView_ListNguoiTrongGiaDinh.Items.Count>0)
            {
                PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.Items.GetItemAt(0);
                var person = new PersonInfo("Người mới", obj._familyInfo);
                int countIsmain = 0;
                foreach (var item in obj._familyInfo.ListPerson)
                {
                    if (item.IsMainPerson == 1)
                    {
                        countIsmain++;
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
                if (countIsmain == 0)
                {
                    obj._familyInfo.ListPerson[0].IsMainPerson = 1;
                    foreach (var item in obj._familyInfo.ListPerson)
                    {
                        if (item.IsMainPerson == 1)
                        {
                            countIsmain++;
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
                }
                obj._familyInfo.ListPerson.Add(person);
                log.Info("Thêm [" + obj.MANS_NAME_HUY + "] vào gia đình");
                viewModel.AddUserAction("Thêm [" + obj.MANS_NAME_HUY + "] vào gia đình " + obj._familyInfo.Name0);
            }
        }
        private void Button_Click_Delete_Person(object sender, RoutedEventArgs e)
        {
            if (ListView_ListNguoiTrongGiaDinh.SelectedItem != null)
            {
                PersonInfo obj = (PersonInfo)ListView_ListNguoiTrongGiaDinh.SelectedItem;
                if(obj._familyInfo.ListPerson.Count>1)
                {
                    if (MessageBox.Show("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình này ?", "Xác Nhận", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        if (obj._familyInfo.ListPerson.IndexOf(obj) > -1)
                        {
                            obj._familyInfo.ListPerson.Remove(obj);
                            obj._familyInfo.OnPropertyChanged("Name");
                            log.Info("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình");
                            viewModel.AddUserAction("Xóa [" + obj.MANS_NAME_HUY + "] ra khỏi gia đình " + obj._familyInfo.Name0);
                        }
                    }
                }
            }
        }
        
        //
    }
}
