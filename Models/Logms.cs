using System.Collections.ObjectModel;
using NLog;

namespace CallRecording.Models
{
    public class Logms
    {
        private readonly ObservableCollection<string> _logs;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Logms()
        {
            _logs = new ObservableCollection<string>();
        }

        public Logms(ObservableCollection<string> logs)
        {
            _logs = logs;
        }

        public void LogMessage(string message, string softwareName)
        {
            string timestampedMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - [{softwareName}] {message}";
            
            // 确保在 UI 线程上更新集合（如果需要）
            // 注意：如果 Logms 被多个线程调用，ObservableCollection 不是线程安全的。
            // 这里假设调用者或绑定机制处理了线程安全，或者仅在 UI 线程调用。
            // 为了安全起见，通常建议使用 BindingOperations.EnableCollectionSynchronization
            // 但这里先保持原样，只添加 NLog
            try 
            {
                // 如果在非 UI 线程，这可能会抛出异常，取决于 WPF 版本和配置
                // 这里简单地捕获异常以防万一，但不阻止日志记录
               System.Windows.Application.Current?.Dispatcher?.Invoke(() => _logs.Add(timestampedMessage));
            }
            catch
            {
                 // 忽略 UI 更新错误，确保日志被记录
            }

            // 使用 NLog 记录到文件
            Logger.Info($"[{softwareName}] {message}");
        }
    }
}