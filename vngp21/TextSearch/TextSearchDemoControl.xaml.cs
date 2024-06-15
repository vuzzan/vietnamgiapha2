using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace vietnamgiapha.TextSearch
{
    /// <summary>
    /// Interaction logic for TextSearchDemoControl.xaml
    /// </summary>
    public partial class TextSearchDemoControl : UserControl
    {
        readonly FamilyTreeViewModel _familyTree;
        public TextSearchDemoControl()
        {
            InitializeComponent();
        }

        void searchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                _familyTree.SearchCommand.Execute(null);
        }
    }
}
