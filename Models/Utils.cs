using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace CallRecording.Models
{
    public static class Utils
    {
        private const string AppSettingsFileName = "appsettings.json";

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
            int.TryParse(ConfigurationHelper.GetSetting("启动软件次数"), out int startupCount);
            startupCount++;
            ConfigurationHelper.SetSetting("启动软件次数", startupCount.ToString());
            return startupCount;
        }

        /// <summary>
        /// 返回监控通话次数
        /// </summary>
        /// <returns></returns>
        public static int 通话监控次数add()
        {
            int.TryParse(ConfigurationHelper.GetSetting("监控通话次数"), out int recCount);
            recCount++;
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
                return (0, 0, 0); // 返回默认值
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
                    folderSize += GetFolderSize(subDir.FullName); // 递归调用
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
                    totalFileSize += file.Length; // 累加符合条件文件的大小
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


        /// <summary>
        /// 检查并确保 appsettings.json 存在
        /// </summary>
        public static void EnsureAppSettingsFile()
        {
            // 获取当前应用程序执行目录
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // 确定 appsettings.json 路径
            string appSettingsPath = Path.Combine(appDirectory, AppSettingsFileName);
            // 判断文件是否存在
            if (File.Exists(appSettingsPath))
            {
                Debug.WriteLine("appsettings.json 文件已存在");
            }
            else
            {
                Debug.WriteLine("appsettings.json 文件不存在，开始释放");
                // 文件不存在，从嵌入资源释放
                ReleaseEmbeddedAppSettingsFile(appSettingsPath);
            }
        }

        /// <summary>
        /// 从嵌入式资源释放 appsettings.json 到指定路径
        /// </summary>
        /// <param name="outputPath">输出的路径</param>
        private static void ReleaseEmbeddedAppSettingsFile(string outputPath)
        {
            // 获取当前程序集
            var assembly = Assembly.GetExecutingAssembly();

            // 嵌入资源的默认命名空间 + 文件名
            string resourceName = "CallRecording.appsettings.json"; // 根据实际命名更改
            using (Stream resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                {
                    throw new FileNotFoundException("嵌入式资源 appsettings.json 未找到");
                }

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            Debug.WriteLine("appsettings.json 文件已释放到 " + outputPath);
        }

        public static void InitAppsettings()
        {
            if (ConfigurationHelper.GetSetting("OutputDirectory") == null)
            {
                ConfigurationHelper.SetSetting("OutputDirectory", "微信通话录音文件/");
            }

            if (ConfigurationHelper.GetSetting("Device_info") == null)
            {
                ConfigurationHelper.SetSetting("Device_info", "null");
            }

            if (ConfigurationHelper.GetSetting("Device_code") == null)
            {
                ConfigurationHelper.SetSetting("Device_code", "null");
            }

            if (ConfigurationHelper.GetSetting("ComputerUserName") == null)
            {
                ConfigurationHelper.SetSetting("ComputerUserName", "null");
            }

            if (ConfigurationHelper.GetSetting("User") == null)
            {
                ConfigurationHelper.SetSetting("User", "null");
            }

            if (ConfigurationHelper.GetSetting("Is_Rge") == null)
            {
                ConfigurationHelper.SetSetting("Is_Rge", "N");
            }

            if (ConfigurationHelper.GetSetting("是否开机自启") == null)
            {
                ConfigurationHelper.SetSetting("是否开机自启", "True");
            }

            if (ConfigurationHelper.GetSetting("是否隐身模式启动") == null)
            {
                ConfigurationHelper.SetSetting("是否隐身模式启动", "False");
            }

            if (ConfigurationHelper.GetSetting("音频采样率") == null)
            {
                ConfigurationHelper.SetSetting("音频采样率", "48000");
            }

            if (ConfigurationHelper.GetSetting("声道数") == null)
            {
                ConfigurationHelper.SetSetting("声道数", "2");
            }

            if (ConfigurationHelper.GetSetting("音频格式") == null)
            {
                ConfigurationHelper.SetSetting("音频格式", "MP3");
            }

            if (ConfigurationHelper.GetSetting("启动软件次数") == null)
            {
                ConfigurationHelper.SetSetting("启动软件次数", "0");
            }

            if (ConfigurationHelper.GetSetting("监控通话次数") == null)
            {
                ConfigurationHelper.SetSetting("监控通话次数", "0");
            }

            if (ConfigurationHelper.GetSetting("监控窗口类名") == null)
            {
                ConfigurationHelper.SetSetting("监控窗口类名", "AudioWnd|测试");
            }

            if (ConfigurationHelper.GetSetting("监控窗口进程名") == null)
            {
                ConfigurationHelper.SetSetting("监控窗口进程名", "WeChat|测试");
            }
        }
    }
}