using System.Collections.ObjectModel;

namespace CallRecording.Models;

public class Logger
{
    private readonly ObservableCollection<string> _logs;

    public Logger(ObservableCollection<string> logs)
    {
        _logs = logs;
    }

    public void LogMessage(string message)
    {
        _logs.Add(message);
    }
}