using System.Collections.ObjectModel;

namespace CallRecording.Models
{
    public class Logms
    {
        private readonly ObservableCollection<string> _logs;

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
            _logs.Add(timestampedMessage);
        }
    }
}