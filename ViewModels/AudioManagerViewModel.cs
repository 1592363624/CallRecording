using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using CallRecording.Models;
using CallRecording.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MySharedProject.Model;

namespace CallRecording.ViewModels
{
    public class AudioFileInfo
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string FileSize { get; set; }
        public string RecordTime { get; set; }
        public string Format { get; set; }
        public DateTime CreationTime { get; set; }
    }

    public partial class AudioManagerViewModel : ObservableObject
    {
        private readonly Logger logger;
        private string recordingsFolder;
        [ObservableProperty] private ObservableCollection<AudioFileInfo> audioFiles;
        [ObservableProperty] private string searchText;
        [ObservableProperty] private string selectedFormat;
        [ObservableProperty] private DateTime? startDate;
        [ObservableProperty] private DateTime? endDate;
        [ObservableProperty] private string statusText;
        [ObservableProperty] private string totalFilesText;

        public List<string> AudioFormats { get; } = new List<string> { "全部", "MP3", "WAV" };

        public IRelayCommand SearchCommand { get; }
        public IRelayCommand<AudioFileInfo> PlayCommand { get; }
        public IRelayCommand<AudioFileInfo> ExportCommand { get; }
        public IRelayCommand<AudioFileInfo> DeleteCommand { get; }
        public IRelayCommand<AudioFileInfo> RenameCommand { get; }

        public AudioManagerViewModel(Logger logger)
        {
            this.logger = logger;

            // 设置默认值
            SelectedFormat = "全部";
            StartDate = DateTime.Now.AddMonths(-1);
            EndDate = DateTime.Now;
            AudioFiles = new ObservableCollection<AudioFileInfo>();

            // 设置录音文件夹路径
            recordingsFolder = ConfigurationHelper.GetSetting("OutputDirectory");

            // 确保文件夹存在
            if (!Directory.Exists(recordingsFolder))
            {
                Directory.CreateDirectory(recordingsFolder);
            }

            // 初始化命令
            SearchCommand = new RelayCommand(ExecuteSearch);
            PlayCommand = new RelayCommand<AudioFileInfo>(ExecutePlay);
            ExportCommand = new RelayCommand<AudioFileInfo>(ExecuteExport);
            DeleteCommand = new RelayCommand<AudioFileInfo>(ExecuteDelete);
            RenameCommand = new RelayCommand<AudioFileInfo>(ExecuteRename);

            // 加载文件
            LoadAudioFiles();
        }

        public void LoadAudioFiles()
        {
            try
            {
                StatusText = "正在加载文件...";
                AudioFiles.Clear();

                if (!Directory.Exists(recordingsFolder))
                {
                    StatusText = "录音文件夹不存在";
                    return;
                }

                var files = Directory.GetFiles(recordingsFolder, "*.*")
                    .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    var audioInfo = new AudioFileInfo
                    {
                        FileName = fileInfo.Name,
                        FilePath = file,
                        FileSize = FormatFileSize(fileInfo.Length),
                        Format = fileInfo.Extension.TrimStart('.').ToUpper(),
                        CreationTime = fileInfo.CreationTime,
                        RecordTime = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    AudioFiles.Add(audioInfo);
                }

                TotalFilesText = $"共 {AudioFiles.Count} 个文件";
                StatusText = "文件加载完成";
            }
            catch (Exception ex)
            {
                logger.LogMessage($"加载音频文件时出错: {ex.Message}", "文件管理");
                StatusText = "加载文件时出错";
            }
        }

        private void ExecuteSearch()
        {
            try
            {
                StatusText = "正在搜索...";

                var filteredFiles = Directory.GetFiles(recordingsFolder, "*.*")
                    .Where(f => f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                    .Select(f => new FileInfo(f))
                    .ToList();

                // 应用格式过滤
                if (SelectedFormat != "全部")
                {
                    filteredFiles = filteredFiles
                        .Where(f => f.Extension.Equals($".{SelectedFormat}", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                // 应用日期过滤
                if (StartDate.HasValue)
                {
                    filteredFiles = filteredFiles
                        .Where(f => f.CreationTime.Date >= StartDate.Value.Date)
                        .ToList();
                }

                if (EndDate.HasValue)
                {
                    filteredFiles = filteredFiles
                        .Where(f => f.CreationTime.Date <= EndDate.Value.Date)
                        .ToList();
                }

                // 应用搜索文本过滤
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    filteredFiles = filteredFiles
                        .Where(f => f.Name.IndexOf(SearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                }

                // 更新UI
                AudioFiles.Clear();
                foreach (var file in filteredFiles)
                {
                    var audioInfo = new AudioFileInfo
                    {
                        FileName = file.Name,
                        FilePath = file.FullName,
                        FileSize = FormatFileSize(file.Length),
                        Format = file.Extension.TrimStart('.').ToUpper(),
                        CreationTime = file.CreationTime,
                        RecordTime = file.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    AudioFiles.Add(audioInfo);
                }

                TotalFilesText = $"共 {AudioFiles.Count} 个文件";
                StatusText = "搜索完成";
            }
            catch (Exception ex)
            {
                logger.LogMessage($"搜索音频文件时出错: {ex.Message}", "文件管理");
                StatusText = "搜索文件时出错";
            }
        }

        private void ExecutePlay(AudioFileInfo audioFile)
        {
            if (audioFile == null) return;

            try
            {
                var playerWindow = new AudioPlayerWindow();
                var playerViewModel = new AudioPlayerViewModel(logger);
                playerWindow.DataContext = playerViewModel;
                playerViewModel.LoadFile(audioFile.FilePath);
                playerWindow.Closed += (s, e) => playerViewModel.CleanupAudio();
                playerWindow.Show();

                StatusText = $"正在播放: {audioFile.FileName}";
            }
            catch (Exception ex)
            {
                logger.LogMessage($"播放音频文件时出错: {ex.Message}", "文件管理");
                StatusText = "播放文件时出错";
            }
        }

        private void ExecuteExport(AudioFileInfo audioFile)
        {
            if (audioFile == null) return;

            try
            {
                var saveDialog = new SaveFileDialog
                {
                    FileName = audioFile.FileName,
                    Filter = audioFile.Format == "MP3" ? "MP3 文件 (*.mp3)|*.mp3" : "WAV 文件 (*.wav)|*.wav",
                    Title = "导出录音文件"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    File.Copy(audioFile.FilePath, saveDialog.FileName, true);
                    StatusText = $"文件已导出至: {saveDialog.FileName}";
                    logger.LogMessage($"文件已导出至: {saveDialog.FileName}", "文件管理");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"导出音频文件时出错: {ex.Message}", "文件管理");
                StatusText = "导出文件时出错";
            }
        }

        private void ExecuteDelete(AudioFileInfo audioFile)
        {
            if (audioFile == null) return;

            try
            {
                var result = MessageBox.Show(
                    $"确定要删除文件 {audioFile.FileName} 吗？此操作不可撤销。",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    File.Delete(audioFile.FilePath);
                    AudioFiles.Remove(audioFile);
                    TotalFilesText = $"共 {AudioFiles.Count} 个文件";
                    StatusText = $"已删除: {audioFile.FileName}";
                    logger.LogMessage($"已删除文件: {audioFile.FileName}", "文件管理");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"删除音频文件时出错: {ex.Message}", "文件管理");
                StatusText = "删除文件时出错";
            }
        }

        private void ExecuteRename(AudioFileInfo audioFile)
        {
            if (audioFile == null) return;

            try
            {
                // 创建输入对话框
                var inputDialog = new Window
                {
                    Title = "重命名文件",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                var label = new Label { Content = "请输入新文件名:" };
                Grid.SetRow(label, 0);

                var textBox = new TextBox
                {
                    Margin = new Thickness(5),
                    Text = Path.GetFileNameWithoutExtension(audioFile.FileName)
                };
                Grid.SetRow(textBox, 1);

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var okButton = new Button
                {
                    Content = "确定",
                    Width = 75,
                    Margin = new Thickness(0, 0, 5, 0),
                    IsDefault = true
                };
                okButton.Click += (s, e) => { inputDialog.DialogResult = true; };

                var cancelButton = new Button
                {
                    Content = "取消",
                    Width = 75,
                    IsCancel = true
                };

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                Grid.SetRow(buttonPanel, 2);

                grid.Children.Add(label);
                grid.Children.Add(textBox);
                grid.Children.Add(buttonPanel);

                inputDialog.Content = grid;

                // 显示对话框并获取结果
                if (inputDialog.ShowDialog() == true)
                {
                    string newFileName = textBox.Text.Trim();

                    // 验证文件名
                    if (string.IsNullOrEmpty(newFileName))
                    {
                        MessageBox.Show("文件名不能为空", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 检查是否包含非法字符
                    char[] invalidChars = Path.GetInvalidFileNameChars();
                    if (newFileName.IndexOfAny(invalidChars) >= 0)
                    {
                        MessageBox.Show("文件名包含非法字符", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    string extension = Path.GetExtension(audioFile.FileName);
                    string newFullFileName = $"{newFileName}{extension}";
                    string newFilePath = Path.Combine(Path.GetDirectoryName(audioFile.FilePath), newFullFileName);

                    // 检查文件是否已存在
                    if (File.Exists(newFilePath) && !string.Equals(audioFile.FilePath, newFilePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show("同名文件已存在", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // 重命名文件
                    File.Move(audioFile.FilePath, newFilePath);

                    // 更新AudioFileInfo对象
                    audioFile.FileName = newFullFileName;
                    audioFile.FilePath = newFilePath;

                    // 刷新列表（触发UI更新）
                    var index = AudioFiles.IndexOf(audioFile);
                    if (index >= 0)
                    {
                        AudioFiles[index] = audioFile;
                    }

                    StatusText = $"文件已重命名为: {newFullFileName}";
                    logger.LogMessage($"文件已重命名为: {newFullFileName}", "文件管理");
                }
            }
            catch (Exception ex)
            {
                logger.LogMessage($"重命名音频文件时出错: {ex.Message}", "文件管理");
                StatusText = "重命名文件时出错";
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            double number = bytes;

            while (number > 1024 && counter < suffixes.Length - 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:0.##} {suffixes[counter]}";
        }
    }
}