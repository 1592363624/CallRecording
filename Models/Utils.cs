using System;
using System.IO;
using CallRecording.ViewModels;

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

        public static (long 总大小, long 已用空间, long 可用空间) GetDiskInfoInMB(string path)
        {
            DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(path));

            if (driveInfo.IsReady)
            {
                long totalSizeMB = driveInfo.TotalSize;
                long availableFreeSpaceMB = driveInfo.AvailableFreeSpace;
                long usedSpaceMB = totalSizeMB - availableFreeSpaceMB;


                return (totalSizeMB, availableFreeSpaceMB, usedSpaceMB);
            }
            else
            {
                return (0, 0, 0);  // 返回默认值
            }
        }

        public static long GetFolderSize(string folderPath)
        {
            long folderSize = 0;

            // 获取文件夹中的所有文件
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(folderPath);

                // 累计当前文件夹的文件大小
                foreach (FileInfo file in dirInfo.GetFiles())
                {
                    folderSize += file.Length;
                }

                // 递归获取子文件夹中的文件大小
                foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
                {
                    folderSize += GetFolderSize(subDir.FullName);  // 递归调用
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取文件夹大小时出错: {ex.Message}");
            }

            return folderSize;
        }

        public static long GetRecSize(string folderPath)
        {
            long totalFileSize = 0;

            // 获取指定目录下的所有文件，并过滤文件名包含“通话录音”的文件
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(folderPath);

                // 遍历当前目录中符合条件的文件
                foreach (FileInfo file in dirInfo.GetFiles("*通话录音*"))
                {
                    totalFileSize += file.Length;  // 累加符合条件文件的大小
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取文件大小时出错: {ex.Message}");
            }

            return totalFileSize;
        }



        public static string FormatSize(long sizeInBytes)
        {
            // 将字节大小转换为合适的单位（KB, MB, GB等）
            double size = sizeInBytes;
            string[] sizeUnits = { "Bytes", "KB", "MB", "GB", "TB" };
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < sizeUnits.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:F2} {sizeUnits[unitIndex]}";
        }


    }
}