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

        public static string GenerateFilename(string savePath, string softwareName, string extension)
        {
            return Path.Combine(savePath, $"{GetFormattedTime()}_{softwareName}_通话录音.{extension}");
        }
        /// <summary>
        /// 返回软件启动次数
        /// </summary>
        /// <returns></returns>
        public static int 软件启动次数add()
        {
            int.TryParse(ConfigurationHelper.GetSetting("启动软件次数"), out int startupCount); startupCount++;
            ConfigurationHelper.SetSetting("启动软件次数", startupCount.ToString());
            return startupCount;
        }
        /// <summary>
        /// 返回监控通话次数
        /// </summary>
        /// <returns></returns>
        public static int 通话监控次数add()
        {
            int.TryParse(ConfigurationHelper.GetSetting("监控通话次数"), out int recCount); recCount++;
            ConfigurationHelper.SetSetting("监控通话次数", recCount.ToString());
            return recCount;
        }
    }
}