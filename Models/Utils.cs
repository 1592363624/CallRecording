using System;
using System.IO;

namespace CallRecording.Models
{
    public static class Utils
    {
        public static string GetFormattedTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        public static string GenerateFilename(string savePath, string softwareName)
        {
            return Path.Combine(savePath, $"{GetFormattedTime()}_{softwareName}_通话录音.wav");
        }
    }
}