using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CallRecording.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CallRecording.ViewModels
{
    public partial class AudioPlayerViewModel : ObservableObject
    {
        private IWavePlayer wavePlayer;
        private AudioFileReader audioFile;
        private string currentFileName;
        private bool isPlaying;
        private double position;
        private double duration;
        private string timeDisplay;
        private string playButtonText;
        private readonly Logger logger;

        public ICommand PlayPauseCommand { get; }
        public ICommand RewindCommand { get; }
        public ICommand ForwardCommand { get; }

        public string CurrentFileName
        {
            get => currentFileName;
            set => SetProperty(ref currentFileName, value);
        }

        public bool IsPlaying
        {
            get => isPlaying;
            set
            {
                if (SetProperty(ref isPlaying, value))
                {
                    PlayButtonText = value ? "⏸" : "▶";
                }
            }
        }

        public double Position
        {
            get => position;
            set
            {
                if (SetProperty(ref position, value) && audioFile != null)
                {
                    audioFile.Position = (long)(audioFile.Length * (value / 100.0));
                    UpdateTimeDisplay();
                }
            }
        }

        public double Duration
        {
            get => duration;
            set => SetProperty(ref duration, value);
        }

        public string TimeDisplay
        {
            get => timeDisplay;
            set => SetProperty(ref timeDisplay, value);
        }

        public string PlayButtonText
        {
            get => playButtonText;
            set => SetProperty(ref playButtonText, value);
        }

        public AudioPlayerViewModel(Logger logger)
        {
            this.logger = logger;
            PlayButtonText = "▶";
            Duration = 100; // 设置滑块最大值

            PlayPauseCommand = new RelayCommand(ExecutePlayPause);
            RewindCommand = new RelayCommand(ExecuteRewind);
            ForwardCommand = new RelayCommand(ExecuteForward);

            // 启动定时器更新进度
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200) // 降低UI更新频率，减少性能开销
            };
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        public void LoadFile(string filePath)
        {
            try
            {
                CleanupAudio();

                // 创建音频文件读取器
                audioFile = new AudioFileReader(filePath);

                // 创建一个音频音量采样提供器，可以控制音量
                var volumeSampleProvider = new VolumeSampleProvider(audioFile.ToSampleProvider())
                {
                    Volume = 1.0f // 设置初始音量
                };

                // 创建波形播放器，设置较大的缓冲区
                wavePlayer = new WaveOutEvent
                {
                    DesiredLatency = 100, // 设置较低的延迟，但不要太低
                    NumberOfBuffers = 3 // 缓冲区数量
                };

                // 初始化播放器，使用采样提供器
                wavePlayer.Init(volumeSampleProvider);

                // 设置播放器停止事件处理
                wavePlayer.PlaybackStopped += (s, e) =>
                {
                    // 处理播放结束或停止事件
                    Application.Current.Dispatcher.BeginInvoke(new Action(() => { IsPlaying = false; }));
                };

                CurrentFileName = Path.GetFileName(filePath);
                UpdateTimeDisplay();
                IsPlaying = false;

                logger.LogMessage($"已加载音频文件: {Path.GetFileName(filePath)}", "音频预览");
            }
            catch (Exception ex)
            {
                logger.LogMessage($"加载音频文件时出错: {ex.Message}", "音频预览");
            }
        }

        private void ExecutePlayPause()
        {
            if (wavePlayer == null) return;

            if (IsPlaying)
            {
                wavePlayer.Pause();
            }
            else
            {
                wavePlayer.Play();
            }

            IsPlaying = !IsPlaying;
        }

        private void ExecuteRewind()
        {
            if (audioFile == null) return;

            var newPosition = audioFile.Position - audioFile.WaveFormat.AverageBytesPerSecond * 5;
            audioFile.Position = Math.Max(0, newPosition);
            UpdateTimeDisplay();
        }

        private void ExecuteForward()
        {
            if (audioFile == null) return;

            var newPosition = audioFile.Position + audioFile.WaveFormat.AverageBytesPerSecond * 5;
            audioFile.Position = Math.Min(audioFile.Length, newPosition);
            UpdateTimeDisplay();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (audioFile != null && wavePlayer != null && wavePlayer.PlaybackState == PlaybackState.Playing)
            {
                Position = (audioFile.Position * 100.0) / audioFile.Length;
                UpdateTimeDisplay();
            }
        }

        private void UpdateTimeDisplay()
        {
            if (audioFile == null) return;

            var currentTime =
                TimeSpan.FromSeconds(audioFile.Position / (double)audioFile.WaveFormat.AverageBytesPerSecond);
            var totalTime = TimeSpan.FromSeconds(audioFile.Length / (double)audioFile.WaveFormat.AverageBytesPerSecond);
            TimeDisplay = $"{currentTime:mm\\:ss} / {totalTime:mm\\:ss}";
        }

        public void CleanupAudio()
        {
            try
            {
                if (IsPlaying)
                {
                    wavePlayer?.Stop();
                    IsPlaying = false;
                }

                if (wavePlayer != null)
                {
                    wavePlayer.Dispose();
                    wavePlayer = null;
                }

                if (audioFile != null)
                {
                    audioFile.Dispose();
                    audioFile = null;
                }

                // 强制进行垃圾回收，释放音频相关资源
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                logger.LogMessage($"清理音频资源时出错: {ex.Message}", "音频预览");
            }
        }
    }
}