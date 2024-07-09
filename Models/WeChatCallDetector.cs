using System.Windows;
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
            return false;
        }
    }
}