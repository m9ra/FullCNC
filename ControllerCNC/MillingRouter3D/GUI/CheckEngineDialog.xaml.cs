using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MillingRouter3D.GUI
{
    /// <summary>
    /// Interaction logic for CheckEngineDialog.xaml
    /// </summary>
    public partial class CheckEngineDialog : Window
    {
        private readonly Action _action;

        private CheckEngineDialog(Action action)
        {
            _action = action;
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
            InitializeComponent();
        }

        internal static void WaitForConfirmation(Action action)
        {
            var dialog = new CheckEngineDialog(action);
            dialog.ShowDialog();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Dispatcher.BeginInvoke(_action);
        }
    }
}
