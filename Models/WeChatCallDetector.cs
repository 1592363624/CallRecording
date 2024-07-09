using System.Windows;
using CallRecording.ViewModels;
using CallRecording.Views;
using FlaUI.UIA3;

namespace CallRecording.Models;

public static class WeChatCallDetector
{
    public static bool IsWeChatCallActive()
    {
        try
        {
            return Application.Current.Dispatcher.Invoke(() =>
            {
                using (var app = new UIA3Automation())
                {
                    var windows = app.GetDesktop().FindAllChildren();
                    foreach (var window in windows)
                        if (window.ClassName == "AudioWnd")
                            return true;
                }

                return false;
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var logger = (Application.Current.MainWindow as MainWindow)?.DataContext as MainViewModel;
                logger?.Logger.LogMessage($"微信通话检测时发生异常: {ex.Message}");
            }));
            return false;
        }
    }
}