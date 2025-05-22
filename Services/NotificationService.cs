using System.Diagnostics;
using System.Windows;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CallRecording.Services;

public static class NotificationService
{
    // 显示通知
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
            // 回退到Windows原生通知
            MessageBox.Show($"{title}\n{message}", "通知",
                MessageBoxButton.OK, MessageBoxImage.Information);

            // 记录错误
            Debug.WriteLine($"通知发送失败: {ex}");
        }
    }
}