using MahApps.Metro.Controls;

namespace vietnamgiapha
{
    /// <summary>Hộp thoại hướng dẫn — modeless, có thể mở song song với cửa sổ chính.</summary>
    public partial class HelpDialog : MetroWindow
    {
        public HelpDialog()
        {
            InitializeComponent();
        }

        private void CloseBtn_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }
    }
}
