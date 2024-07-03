using FlaUI.UIA3;

namespace CallRecording;

public class WeChatCallDetector
{
    public static bool IsWeChatCallActive()
    {
        using (var app = new UIA3Automation())
        {
            var windows = app.GetDesktop().FindAllChildren();
            foreach (var window in windows)
                if (window.ClassName == "AudioWnd")
                    return true;
        }

        return false;
    }
}