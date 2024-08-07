﻿using System.IO;

namespace CallRecording.Models;

public static class Utils
{
    public static string GetFormattedTime()
    {
        return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }

    public static string GetDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
    }

    public static string GenerateFilename(string savePath)
    {
        var filename = $"{GetFormattedTime()}_微信通话录音.wav";
        return Path.Combine(savePath, filename);
    }
}