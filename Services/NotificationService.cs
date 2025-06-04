using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Toolkit.Uwp.Notifications;
using Application = System.Windows.Application;
// 添加声音支持
// 添加文件操作支持
using Clipboard = System.Windows.Clipboard;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Path = System.Windows.Shapes.Path;

namespace CallRecording.Services;

public static class NotificationService
{
    public static class WpfScreen
    {
        public static Screen GetScreenFrom(Window window)
        {
            return Screen.FromHandle(new WindowInteropHelper(window).Handle);
        }
    }

    public static void ShowNotification(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch (Exception ex)
        {
            ShowFallbackNotification(title, message);
        }
    }

    private static void ShowFallbackNotification(string title, string message)
    {
        try
        {
            if (Application.Current == null)
            {
                Debug.WriteLine("Application.Current 为空，无法显示通知");
                return;
            }

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                // 创建主内容容器
                var contentGrid = new Grid();

                // 添加背景
                var background = new Border
                {
                    Background = new LinearGradientBrush(
                        Color.FromRgb(45, 45, 60),
                        Color.FromRgb(30, 30, 45),
                        90),
                    CornerRadius = new CornerRadius(10),
                    BorderThickness = new Thickness(1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(70, 70, 90))
                };
                contentGrid.Children.Add(background);

                // 添加光泽效果
                var gloss = new Border
                {
                    Background = new LinearGradientBrush(
                        Color.FromArgb(50, 255, 255, 255),
                        Colors.Transparent,
                        90),
                    CornerRadius = new CornerRadius(10, 10, 0, 0),
                    Height = 20,
                    VerticalAlignment = VerticalAlignment.Top
                };
                contentGrid.Children.Add(gloss);

                // 创建内容面板
                var contentStack = new StackPanel
                {
                    Margin = new Thickness(20, 15, 20, 20)
                };

                // 标题栏（包含标题和关闭按钮）
                var titleBar = new Grid
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // 标题
                var titleBlock = new TextBlock
                {
                    Text = title,
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.Bold,
                    FontSize = 16,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(titleBlock, 0);
                titleBar.Children.Add(titleBlock);
                contentStack.Children.Add(titleBar);

                // 消息内容
                var messageBlock = new TextBlock
                {
                    Text = message,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 240)),
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 5, 0, 0)
                };
                contentStack.Children.Add(messageBlock);

                contentGrid.Children.Add(contentStack);

                // 添加上下文菜单
                var contextMenu = new ContextMenu
                {
                    Background = new SolidColorBrush(Color.FromRgb(50, 50, 65)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(80, 80, 100)),
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 240)),
                    FontSize = 14
                };

                var copyMenuItem = new MenuItem
                {
                    Header = "复制消息",
                    Icon = new Path
                    {
                        Data = Geometry.Parse(
                            "M8,4 L12,4 L12,8 L16,8 L16,16 L8,16 Z M4,8 L8,8 L8,20 L16,20 L16,24 L4,24 Z"),
                        Fill = Brushes.LightGray,
                        Stretch = Stretch.Uniform,
                        Width = 16,
                        Height = 16
                    }
                };

                copyMenuItem.Click += (s, e) =>
                {
                    try
                    {
                        Clipboard.SetText($"{title}: {message}");
                    }
                    catch
                    {
                        /* 忽略复制错误 */
                    }
                };

                contextMenu.Items.Add(copyMenuItem);
                contentGrid.ContextMenu = contextMenu;

                // 创建窗口
                var toastWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    Topmost = true,
                    ShowInTaskbar = false,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    Content = contentGrid,
                    Opacity = 0, // 初始透明用于动画
                    Effect = new DropShadowEffect
                    {
                        Color = Color.FromArgb(200, 0, 0, 0),
                        Opacity = 0.7,
                        BlurRadius = 20,
                        ShadowDepth = 3
                    }
                };

                // 设置窗口所有者
                try
                {
                    var activeWindow = Application.Current.Windows
                        .OfType<Window>()
                        .FirstOrDefault(w => w.IsActive && w != toastWindow);

                    if (activeWindow != null)
                    {
                        toastWindow.Owner = activeWindow;
                    }
                    else if (Application.Current.MainWindow != null &&
                             Application.Current.MainWindow != toastWindow &&
                             Application.Current.MainWindow.IsLoaded)
                    {
                        toastWindow.Owner = Application.Current.MainWindow;
                    }
                }
                catch (Exception ownerEx)
                {
                    Debug.WriteLine($"设置所有者失败: {ownerEx.Message}");
                }

                // 点击任意位置关闭（仅限左键）
                toastWindow.MouseDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                        CloseWithAnimation(toastWindow);
                };

                // 悬停效果 - 添加轻微放大效果
                contentGrid.MouseEnter += (s, e) =>
                {
                    var scale = new ScaleTransform(1, 1);
                    contentGrid.RenderTransform = scale;
                    contentGrid.RenderTransformOrigin = new Point(0.5, 0.5);

                    var anim = new DoubleAnimation(1.02, TimeSpan.FromMilliseconds(20));
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                };

                contentGrid.MouseLeave += (s, e) =>
                {
                    var scale = contentGrid.RenderTransform as ScaleTransform;
                    if (scale == null) return;

                    var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(20));
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
                };

                // 窗口加载事件
                toastWindow.Loaded += (s, e) =>
                {
                    // 播放Win11通知声音
                    PlayNotificationSound();

                    // 显示动画
                    var fadeIn = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    toastWindow.BeginAnimation(Window.OpacityProperty, fadeIn);

                    // 定位窗口
                    contentGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    contentGrid.Arrange(new Rect(contentGrid.DesiredSize));

                    double width = contentGrid.ActualWidth;
                    double height = contentGrid.ActualHeight;

                    if (width <= 0 || height <= 0)
                    {
                        width = 300;
                        height = 100;
                    }

                    var screen = WpfScreen.GetScreenFrom(toastWindow);
                    var workingArea = screen.WorkingArea;

                    // 底部居中显示
                    double left = workingArea.Left + (workingArea.Width - width) / 2;
                    double top = workingArea.Bottom - height - 30;
                    toastWindow.Left = left;
                    toastWindow.Top = top;
                };

                // 自动关闭定时器
                var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
                closeTimer.Tick += (s, e) =>
                {
                    closeTimer.Stop();
                    CloseWithAnimation(toastWindow);
                };

                toastWindow.Show();
                closeTimer.Start();
            }));
        }
        catch (Exception fallbackEx)
        {
            Debug.WriteLine($"自定义通知失败: {fallbackEx}");
            Console.WriteLine($"通知: {title} - {message}");
        }
    }

    private static void CloseWithAnimation(Window window)
    {
        var fadeOut = new DoubleAnimation
        {
            From = 1,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(250),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fadeOut.Completed += (s, e) => window.Close();
        window.BeginAnimation(Window.OpacityProperty, fadeOut);
    }

    private static void PlayNotificationSound()
    {
        try
        {
            // 尝试播放Windows默认通知声音
            var soundPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Media", "Windows Notify Calendar.wav");

            if (File.Exists(soundPath))
            {
                var player = new SoundPlayer(soundPath);
                player.Play();
            }
            else
            {
                // 回退到系统声音
                SystemSounds.Beep.Play();
            }
        }
        catch
        {
            // 忽略声音播放错误
        }
    }
}