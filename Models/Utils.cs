using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using MySharedProject;
using MySharedProject.Model;

namespace CallRecording.Models
{
    public static class Utils
    {
        private const string AppSettingsFileName = "appsettings.json";

        public static string GetFormattedTime()
        {
            return DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        public static string DecodeBase64String(string base64EncodedData)
        {
            // 将Base64编码的字符串转换为字节流
            byte[] base64EncodedBytes = Convert.FromBase64String(base64EncodedData);

            // 将字节流解码为字符串
            string decodedString = Encoding.UTF8.GetString(base64EncodedBytes);

            return decodedString;
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
            // string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            // 确定 appsettings.json 路径
            // string appSettingsPath = Path.Combine(appDirectory, AppSettingsFileName);   //软件根目录
            string appSettingsPath = DataSource.Configurationfilepath; //"C:\\Shell\\CallRecording\\appsettings.json"
            // 判断配置文件是否存在
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

            // 确保下载缓存目录存在
            string directoryPath = Path.GetDirectoryName("C:\\Shell\\Download");
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
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

                // 确保目录存在
                string directoryPath = Path.GetDirectoryName(outputPath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                {
                    resourceStream.CopyTo(fileStream);
                }
            }

            Debug.WriteLine("appsettings.json 文件已释放到 " + outputPath);
        }


        /// <summary>
        /// 解压缩zip文件
        /// </summary>
        /// <param name="zipFilePath"></param>
        /// <param name="destinationFolder"></param>
        public static void UnzipFile(string zipFilePath, string destinationFolder)
        {
            try
            {
                // 确保目标目录存在，如果不存在则创建
                if (!Directory.Exists(destinationFolder))
                {
                    Directory.CreateDirectory(destinationFolder);
                }

                // 使用 ZipFile 解压文件
                ZipFile.ExtractToDirectory(zipFilePath, destinationFolder);
                Console.WriteLine($"文件解压缩成功，解压至 {destinationFolder}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"解压缩过程中发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 用于释放更新文件的脚本
        /// </summary>
        public static void UnzipBat()
        {
            // 获取当前应用程序路径（自动处理路径格式）
            // string appPath = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');    //Shadow Copy 或 Single-File Publish 机制可能会导致路径错误
            string appPath = Environment.CurrentDirectory;

            // 定义.bat文件路径
            string batFilePath = Path.Combine(appPath, "temp_script.bat");

            try
            {
                // string batContent = GlobalsVariables.gv.script;
                // 原始批处理模板
                string batContent = @"@echo off
setlocal enabledelayedexpansion

REM ################ 核心逻辑 ################
REM 强制设置目标路径为当前脚本所在目录
set ""TargetDir=%~dp0""
set ""TargetDir=%TargetDir:\=/%""
set ""TargetDir=%TargetDir:/=\%""
if ""%TargetDir:~-1%""=="""" set ""TargetDir=%TargetDir:~0,-1%""
REM ##########################################


REM 结束进程
tasklist /FI ""IMAGENAME eq CallRecording.exe"" 2>NUL | find /I ""CallRecording.exe"" >NUL && taskkill /F /IM ""CallRecording.exe""

REM 解压固定路径的ZIP
powershell -Command ""Expand-Archive -Path 'C:\Shell\Download\CallRecording.zip' -DestinationPath '%TargetDir%' -Force""

REM 清理固定路径的ZIP
del /F /Q ""C:\Shell\Download\CallRecording.zip"" >nul 2>&1

REM 启动程序
start """" /D ""%TargetDir%"" CallRecording.exe

REM 自删除
start """" /B cmd /c ""timeout /t 1 /nobreak >nul & del /F /Q ""%~f0""""
exit
";

                // 仅替换目标路径为当前程序目录
                batContent = batContent.Replace("set \"TargetDir=%~dp0\"", $"set \"TargetDir={appPath}\"");

                File.WriteAllText(batFilePath, batContent);
                // 静默运行批处理
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = batFilePath,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    process.Start();
                    process.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"操作失败: {ex}");
            }
            finally
            {
                if (File.Exists(batFilePath))
                {
                    try
                    {
                        File.Delete(batFilePath);
                    }
                    catch
                    {
                        /* 忽略清理失败 */
                    }
                }
            }
        }

        /// <summary>
        /// 初始化appsettings.json设置官方认定的默认值
        /// </summary>
        public static void InitAppsettings()
        {
            if (ConfigurationHelper.GetSetting("OutputDirectory") == "NULL")
            {
                ConfigurationHelper.SetSetting("OutputDirectory", "微信通话录音文件/");
            }

            if (ConfigurationHelper.GetSetting("Device_info") == "NULL")
            {
                ConfigurationHelper.SetSetting("Device_info", "NULL");
            }

            if (ConfigurationHelper.GetSetting("Device_code") == "NULL")
            {
                ConfigurationHelper.SetSetting("Device_code", "NULL");
            }

            if (ConfigurationHelper.GetSetting("ComputerUserName") == "NULL")
            {
                ConfigurationHelper.SetSetting("ComputerUserName", "NULL");
            }

            if (ConfigurationHelper.GetSetting("User") == "NULL")
            {
                ConfigurationHelper.SetSetting("User", "NULL");
            }

            if (ConfigurationHelper.GetSetting("Is_Rge") == "NULL")
            {
                ConfigurationHelper.SetSetting("Is_Rge", "N");
            }

            if (ConfigurationHelper.GetSetting("是否开机自启") == "NULL")
            {
                ConfigurationHelper.SetSetting("是否开机自启", "False");
            }

            if (ConfigurationHelper.GetSetting("是否隐身模式启动") == "NULL")
            {
                ConfigurationHelper.SetSetting("是否隐身模式启动", "False");
            }

            if (ConfigurationHelper.GetSetting("音频采样率") == "NULL")
            {
                ConfigurationHelper.SetSetting("音频采样率", "48000");
            }

            if (ConfigurationHelper.GetSetting("声道数") == "NULL")
            {
                ConfigurationHelper.SetSetting("声道数", "2");
            }

            if (ConfigurationHelper.GetSetting("音频格式") == "NULL")
            {
                ConfigurationHelper.SetSetting("音频格式", "MP3");
            }

            if (ConfigurationHelper.GetSetting("启动软件次数") == "NULL")
            {
                ConfigurationHelper.SetSetting("启动软件次数", "0");
            }

            if (ConfigurationHelper.GetSetting("监控通话次数") == "NULL")
            {
                ConfigurationHelper.SetSetting("监控通话次数", "0");
            }

            if (ConfigurationHelper.GetSetting("监控窗口类名") == "NULL")
            {
                ConfigurationHelper.SetSetting("监控窗口类名", "AudioWnd|要监控的窗口类名");
            }

            if (ConfigurationHelper.GetSetting("监控窗口进程名") == "NULL")
            {
                ConfigurationHelper.SetSetting("监控窗口进程名", "WeChat|要监控的窗口进程名");
            }

            if (ConfigurationHelper.GetSetting("上次执行检测更新时间") == "NULL")
            {
                ConfigurationHelper.SetSetting("上次执行检测更新时间", "2025-3-5 15:38:14");
            }

            if (ConfigurationHelper.GetSetting("录音快捷键") == "NULL")
            {
                ConfigurationHelper.SetSetting("录音快捷键", "F9");
            }
        }
    }
}