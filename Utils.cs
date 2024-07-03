using System.IO;

namespace CallRecording;

public class Utils
{
    public static string GetFormattedTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    public static string GetDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    public static string GenerateFilename()
    {
        return Path.Combine(GetDesktopPath(), $"{GetFormattedTime()}_通话录音文件.wav");
    }
}