using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Controls;
using ControllerCNC.Primitives;
using MillingRouter3D.Primitives;

namespace MillingRouter3D.GUI
{
    class MousePositionInfo : MillingWorkspaceItem
    {
        TextBlock _xInfo;

        TextBlock _yInfo;

        public MousePositionInfo()
            : base(new ReadableIdentifier("MouseInfo"))
        {
            PositionX = 1000;
            PositionY = 500;

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
            var xPosition = PositionX;
            var yPosition = PositionY;

            var workspace = Parent as MillingWorkspacePanel;
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
