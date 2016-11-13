using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

namespace ControllerCNC.GUI
{
    public class ListBoxOverride : ListBox
    {
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}
