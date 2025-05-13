using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CallRecording.Views
{
    public class MarkerWindow : Window
    {
        private readonly Ellipse marker;

        public MarkerWindow()
        {
            // 设置窗口属性
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;

            // 覆盖整个屏幕
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
            Left = 0;
            Top = 0;

            // 创建标记
            var canvas = new Canvas();
            marker = new Ellipse
            {
                Width = 20,
                Height = 20,
                Stroke = Brushes.Orange,
                StrokeThickness = 2
            };
            canvas.Children.Add(marker);
            Content = canvas;
        }

        public void UpdatePosition(double x, double y)
        {
            // 更新标记位置
            Canvas.SetLeft(marker, x - 10);
            Canvas.SetTop(marker, y - 10);
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}