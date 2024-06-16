using MahApps.Metro.Controls;
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
            string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            log.Info("Application started...version " + ver);
            AutoUpdater.Start("https://vietnamgiapha.com/download/autoupdate.xml");
            InitEvents();
            this.Title = this.Title + " - " + ver;
            this.viewModel = new MainWindowViewModel(DialogCoordinator.Instance, this);
            this.DataContext = this.viewModel;
            //
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
                    viewModel.FamilyTree.GP.FileName = defaultSaveFolder +"\\"+viewModel.FamilyTree.GP.GiaphaName.Replace(" ", "_") + "_" + ".json";
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
            var _progressDialogController = await this.ShowProgressAsync("Đợi upload lên web...", "Đang upload từ web vietnamgiapha.com");
            _progressDialogController.SetProgress(0);
            _progressDialogController.SetIndeterminate();

            try
            {
                //BtnDownloadGiaPha.IsEnabled = false;
                string gp = await Database.UploadWeb(
                    viewModel.FamilyTree.Username.Trim().ToLower(), 
                    viewModel.FamilyTree.Password.Trim(),
                    viewModel.FamilyTree.ToJson()
                    );
                _progressDialogController.SetProgress(1);
                if (gp != null)
                {
                    log.Info("BtnUploadGiaPha_Click: upload từ web : " + gp);
                    MessageBox.Show(gp, "Upload");
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
    }
}
