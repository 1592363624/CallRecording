using Microsoft.Toolkit.Uwp.Notifications;

namespace CallRecording.Services;

public static class NotificationService
{
    // 显示通知
    public static void ShowNotification(string title, string message)
    {
        new ToastContentBuilder()
            .AddText(title)
            .AddText(message)
            .Show();
    }
}