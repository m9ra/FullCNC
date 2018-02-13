using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;

using ControllerCNC.Primitives;
using ControllerCNC.Machine;

namespace ControllerCNC.GUI
{
    internal class MousePositionInfo : WorkspaceItem
    {
        TextBlock _xInfo;

        TextBlock _yInfo;

        public MousePositionInfo()
            : base(new ReadableIdentifier("MouseInfo"))
        {
            PositionC1 = 1000;
            PositionC2 = 500;

            _xInfo = createInfo("X: 153.2");
            _yInfo = createInfo("Y: 23.8");

            initialize();
        }

        protected override object createContent()
        {
            var panel = new StackPanel();
            panel.Margin = new Thickness(20);

            panel.Children.Add(_xInfo);
            panel.Children.Add(_yInfo);

            return panel;
        }

        internal void UpdateInfo()
        {
            var xPosition = PositionC1 * Configuration.MilimetersPerStep;
            var yPosition = PositionC2 * Configuration.MilimetersPerStep;

            var infoTextX = string.Format("X: {0:0.0}mm", xPosition);
            var infoTextY = string.Format("Y: {0:0.0}mm", yPosition);

            _xInfo.Text = infoTextX;
            _yInfo.Text = infoTextY;
        }

        public void Show()
        {
            this.Visibility = Visibility.Visible;
        }

        public void Hide()
        {
            this.Visibility = Visibility.Hidden;
        }

        private TextBlock createInfo(string text)
        {
            var info = new TextBlock();
            info.Text = text;
            info.FontSize = 20;
            return info;
        }
    }
}
