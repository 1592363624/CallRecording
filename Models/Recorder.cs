using System.Collections.Concurrent;
using System.IO;
using MySharedProject.Model;
using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CallRecording.Models
{
    /// <summary>
    /// 录音核心类，负责采集音频、写入文件及后期混音
    /// </summary>
    public class Recorder
    {
        public enum AudioFormat
        {
            MP3,
            WAV
        }

        private struct AudioChunk
        {
            public byte[] Buffer;
            public int BytesRecorded;
            public bool IsSpeaker;
        }

        private readonly object _lockObject = new();
        private readonly Logms _logms;
        private bool _isRecording;
        private bool _isPaused = false;

        public WasapiLoopbackCapture _loopbackSource;
        private WasapiCapture _microphoneSource;

        private LameMP3FileWriter _mp3MicrophoneFile;
        private LameMP3FileWriter _mp3SpeakerFile;
        private WaveFileWriter _waveMicrophoneFile;
        private WaveFileWriter _waveSpeakerFile;

        private string _outputMicrophoneFileName;
        private string _outputMixedFileName;
        private string _outputSpeakerFileName;
        private AudioFormat _selectedFormat;

        // 异步写入组件
        private ConcurrentQueue<AudioChunk> _writeQueue;
        private Task _writeTask;
        private CancellationTokenSource _writeCts;
        private ManualResetEventSlim _writeLoopFinished;

        // 添加录音停止事件，用于通知 UI
        public event EventHandler RecordingStopped;

        public Recorder(Logms logms, AudioFormat selectedFormat)
        {
            _logms = logms;
            _selectedFormat = selectedFormat;
        }

        public void UpdateAudioFormat(AudioFormat newFormat)
        {
            _selectedFormat = newFormat;
        }

        public void StartRecording(string savePath, string softwareName)
        {
            lock (_lockObject)
            {
                if (_isRecording) return;

                // 初始化异步写入组件
                _writeQueue = new ConcurrentQueue<AudioChunk>();
                _writeCts = new CancellationTokenSource();
                _writeLoopFinished = new ManualResetEventSlim(false);
                _writeTask = Task.Run(() => WriteLoop(_writeCts.Token));

                string extension = _selectedFormat == AudioFormat.MP3 ? "mp3" : "wav";
                _outputSpeakerFileName = Utils.GenerateFilename(savePath, softwareName + "_speaker", extension);
                _outputMicrophoneFileName = Utils.GenerateFilename(savePath, softwareName + "_microphone", extension);
                _outputMixedFileName = Utils.GenerateFilename(savePath, softwareName + "_mixed", extension);

                try
                {
                    _loopbackSource = new WasapiLoopbackCapture { WaveFormat = new WaveFormat(48000, 2) };
                    _microphoneSource = new WasapiCapture { WaveFormat = new WaveFormat(48000, 2) };

                    _logms.LogMessage($"系统声音格式: {_loopbackSource.WaveFormat}", softwareName);
                    _logms.LogMessage($"麦克风声音格式: {_microphoneSource.WaveFormat}", softwareName);

                    if (_selectedFormat == AudioFormat.WAV)
                    {
                        _waveSpeakerFile = new WaveFileWriter(_outputSpeakerFileName, _loopbackSource.WaveFormat);
                        _waveMicrophoneFile =
                            new WaveFileWriter(_outputMicrophoneFileName, _microphoneSource.WaveFormat);
                    }
                    else if (_selectedFormat == AudioFormat.MP3)
                    {
                        _mp3SpeakerFile = new LameMP3FileWriter(_outputSpeakerFileName, _loopbackSource.WaveFormat,
                            LAMEPreset.STANDARD);
                        _mp3MicrophoneFile = new LameMP3FileWriter(_outputMicrophoneFileName,
                            _microphoneSource.WaveFormat, LAMEPreset.STANDARD);
                    }

                    // 修改为入队操作，避免阻塞音频线程
                    _loopbackSource.DataAvailable += (s, e) =>
                    {
                        if (_writeQueue != null && !_isPaused && e.BytesRecorded > 0)
                        {
                            var buffer = new byte[e.BytesRecorded];
                            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
                            _writeQueue.Enqueue(new AudioChunk
                                { Buffer = buffer, BytesRecorded = e.BytesRecorded, IsSpeaker = true });
                        }
                    };

                    _microphoneSource.DataAvailable += (s, e) =>
                    {
                        if (_writeQueue != null && !_isPaused && e.BytesRecorded > 0)
                        {
                            var buffer = new byte[e.BytesRecorded];
                            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
                            _writeQueue.Enqueue(new AudioChunk
                                { Buffer = buffer, BytesRecorded = e.BytesRecorded, IsSpeaker = false });
                        }
                    };

                    _loopbackSource.RecordingStopped += OnRecordingStopped;
                    //_microphoneSource.RecordingStopped += OnRecordingStopped;

                    _loopbackSource.StartRecording();
                    _microphoneSource.StartRecording();
                    _isRecording = true;
                    GlobalsVariables.是否正在录音 = true;
                    _isPaused = false; // 重置暂停状态

                    _logms.LogMessage("开始录音...", softwareName);
                }
                catch (Exception ex)
                {
                    _logms.LogMessage($"初始化录音源时发生异常: {ex.Message}", "录音器");
                    Cleanup();
                }
            }

            Utils.通话监控次数add();
        }

        private void WriteLoop(CancellationToken token)
        {
            try
            {
                // 持续写入直到取消且队列为空
                while (!token.IsCancellationRequested || (_writeQueue != null && !_writeQueue.IsEmpty))
                {
                    if (_writeQueue?.TryDequeue(out var chunk) == true)
                    {
                        try
                        {
                            if (_selectedFormat == AudioFormat.WAV)
                            {
                                if (chunk.IsSpeaker) _waveSpeakerFile?.Write(chunk.Buffer, 0, chunk.BytesRecorded);
                                else _waveMicrophoneFile?.Write(chunk.Buffer, 0, chunk.BytesRecorded);
                            }
                            else
                            {
                                if (chunk.IsSpeaker) _mp3SpeakerFile?.Write(chunk.Buffer, 0, chunk.BytesRecorded);
                                else _mp3MicrophoneFile?.Write(chunk.Buffer, 0, chunk.BytesRecorded);
                            }
                        }
                        catch (Exception)
                        {
                            // 忽略单次写入错误，避免中断整个录音
                        }
                    }
                    else
                    {
                        Thread.Sleep(5); // 避免空转占用 CPU
                    }
                }
            }
            finally
            {
                // 确保在循环结束后刷新缓冲区
                try
                {
                    _waveSpeakerFile?.Flush();
                    _waveMicrophoneFile?.Flush();
                    _mp3SpeakerFile?.Flush();
                    _mp3MicrophoneFile?.Flush();
                }
                catch
                {
                }

                _writeLoopFinished?.Set();
            }
        }

        public void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            lock (_lockObject)
            {
                try
                {
                    // 停止写入线程并等待完成
                    if (_writeCts != null)
                    {
                        _writeCts.Cancel();
                        // 最多等待 5 秒，防止死锁
                        _writeLoopFinished?.Wait(5000);
                    }

                    // 确保所有文件句柄已释放
                    Cleanup();

                    if (e.Exception != null)
                        _logms.LogMessage($"录音停止时发生异常: {e.Exception.Message}", "录音器");
                    else
                        MixAudio();
                }
                catch (Exception ex)
                {
                    _logms.LogMessage($"在处理录音停止事件时发生异常: {ex.Message}", "录音器");
                }
                finally
                {
                    _isRecording = false;
                    GlobalsVariables.是否正在录音 = false;
                    _isPaused = false;
                    // 通知外部录音已停止（例如更新 UI）
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void StopRecording()
        {
            lock (_lockObject)
            {
                if (!_isRecording) return;

                try
                {
                    _loopbackSource?.StopRecording();
                    _microphoneSource?.StopRecording();
                    _logms.LogMessage("录音停止，文件已保存。", "录音器");
                }
                catch (Exception ex)
                {
                    _logms.LogMessage($"停止录音时发生异常: {ex.Message}", "录音器");
                }

                _isRecording = false;
                GlobalsVariables.是否正在录音 = false;
                _isPaused = false; // 重置暂停状态
            }
        }

        // 添加暂停录音方法
        public void PauseRecording()
        {
            lock (_lockObject)
            {
                if (!_isRecording || _isPaused) return;

                _isPaused = true;
                GlobalsVariables.是否正在录音 = false;
                _logms.LogMessage("录音已暂停", "录音器");
            }
        }

        // 添加恢复录音方法
        public void ResumeRecording()
        {
            lock (_lockObject)
            {
                if (!_isRecording || !_isPaused) return;

                _isPaused = false;
                GlobalsVariables.是否正在录音 = true;
                _logms.LogMessage("录音已恢复,正在继续录音", "录音器");
            }
        }

        public bool IsRecording()
        {
            lock (_lockObject)
            {
                return _isRecording;
            }
        }

        // 添加检查是否暂停的方法
        public bool IsPaused()
        {
            lock (_lockObject)
            {
                return _isPaused;
            }
        }

        private void Cleanup()
        {
            _loopbackSource?.Dispose();
            _microphoneSource?.Dispose();

            // 先清理写入相关资源，确保文件句柄被释放
            _writeCts?.Dispose();
            _writeLoopFinished?.Dispose();

            _waveSpeakerFile?.Dispose();
            _waveMicrophoneFile?.Dispose();
            _mp3SpeakerFile?.Dispose();
            _mp3MicrophoneFile?.Dispose();

            _loopbackSource = null;
            _microphoneSource = null;
            _waveSpeakerFile = null;
            _waveMicrophoneFile = null;
            _mp3SpeakerFile = null;
            _mp3MicrophoneFile = null;

            _writeCts = null;
            _writeLoopFinished = null;
            _writeQueue = null;
        }

        private void MixAudio()
        {
            try
            {
                // 确保录音文件已释放
                // Cleanup(); // 已经在 OnRecordingStopped 调用

                string extension = _selectedFormat == AudioFormat.MP3 ? "mp3" : "wav";
                _outputMixedFileName = Path.ChangeExtension(_outputMixedFileName, extension);

                if (_selectedFormat == AudioFormat.WAV)
                {
                    // 使用 WaveFileReader 替代 AudioFileReader，确保正确解析 WAV 头信息
                    using (var readerSpeaker = new WaveFileReader(_outputSpeakerFileName))
                    using (var readerMicrophone = new WaveFileReader(_outputMicrophoneFileName))
                    {
                        _logms.LogMessage(
                            $"混音源格式(WAV): Speaker={readerSpeaker.WaveFormat}, Mic={readerMicrophone.WaveFormat}",
                            "录音器");

                        ISampleProvider speakerProvider = readerSpeaker.ToSampleProvider();
                        ISampleProvider microphoneProvider = readerMicrophone.ToSampleProvider();

                        // 1. 统一采样率 (以系统声音为准)
                        if (microphoneProvider.WaveFormat.SampleRate != speakerProvider.WaveFormat.SampleRate)
                        {
                            _logms.LogMessage(
                                $"重采样麦克风音频: {microphoneProvider.WaveFormat.SampleRate} -> {speakerProvider.WaveFormat.SampleRate}",
                                "录音器");
                            microphoneProvider = new WdlResamplingSampleProvider(microphoneProvider,
                                speakerProvider.WaveFormat.SampleRate);
                        }

                        // 2. 统一声道数 (通常为双声道)
                        if (microphoneProvider.WaveFormat.Channels != speakerProvider.WaveFormat.Channels)
                        {
                            if (speakerProvider.WaveFormat.Channels == 2 && microphoneProvider.WaveFormat.Channels == 1)
                            {
                                microphoneProvider = new MonoToStereoSampleProvider(microphoneProvider);
                            }
                            else if (speakerProvider.WaveFormat.Channels == 1 &&
                                     microphoneProvider.WaveFormat.Channels == 2)
                            {
                                microphoneProvider = new StereoToMonoSampleProvider(microphoneProvider);
                            }
                        }

                        // 3. 音量调整 (防止混音后爆音/削波)
                        // 与 MP3 混音逻辑保持一致，各 0.7 的音量
                        var speakerVol = new VolumeSampleProvider(speakerProvider) { Volume = 0.7f };
                        var micVol = new VolumeSampleProvider(microphoneProvider) { Volume = 0.7f };

                        // 4. 混音
                        var mixer = new MixingSampleProvider(new[] { speakerVol, micVol });

                        // 5. 保存为 16-bit PCM WAV (兼容性更好)
                        WaveFileWriter.CreateWaveFile16(_outputMixedFileName, mixer);
                    }
                }
                else if (_selectedFormat == AudioFormat.MP3)
                {
                    using (var readerSpeaker = new Mp3FileReader(_outputSpeakerFileName))
                    using (var readerMicrophone = new Mp3FileReader(_outputMicrophoneFileName))
                    {
                        var waveFormatSpeaker = readerSpeaker.WaveFormat;
                        var waveFormatMicrophone = readerMicrophone.WaveFormat;

                        if (waveFormatSpeaker.SampleRate != waveFormatMicrophone.SampleRate ||
                            waveFormatSpeaker.Channels != waveFormatMicrophone.Channels ||
                            waveFormatSpeaker.BitsPerSample != waveFormatMicrophone.BitsPerSample)
                        {
                            throw new InvalidOperationException("录制的两个音频文件格式不一致，无法混音");
                        }

                        using (var writer = new LameMP3FileWriter(_outputMixedFileName, waveFormatSpeaker,
                                   LAMEPreset.STANDARD))
                        {
                            var bufferSpeaker = new byte[waveFormatSpeaker.AverageBytesPerSecond];
                            var bufferMicrophone = new byte[waveFormatMicrophone.AverageBytesPerSecond];

                            int readSpeaker = 0, readMicrophone = 0;
                            bool speakerFinished = false;
                            bool microphoneFinished = false;

                            while (!speakerFinished || !microphoneFinished)
                            {
                                if (!speakerFinished)
                                {
                                    readSpeaker = readerSpeaker.Read(bufferSpeaker, 0, bufferSpeaker.Length);
                                    if (readSpeaker == 0) speakerFinished = true;
                                    else if (readSpeaker < bufferSpeaker.Length)
                                        Array.Clear(bufferSpeaker, readSpeaker, bufferSpeaker.Length - readSpeaker);
                                }
                                else
                                {
                                    readSpeaker = 0;
                                    Array.Clear(bufferSpeaker, 0, bufferSpeaker.Length);
                                }

                                if (!microphoneFinished)
                                {
                                    readMicrophone =
                                        readerMicrophone.Read(bufferMicrophone, 0, bufferMicrophone.Length);
                                    if (readMicrophone == 0) microphoneFinished = true;
                                    else if (readMicrophone < bufferMicrophone.Length)
                                        Array.Clear(bufferMicrophone, readMicrophone,
                                            bufferMicrophone.Length - readMicrophone);
                                }
                                else
                                {
                                    readMicrophone = 0;
                                    Array.Clear(bufferMicrophone, 0, bufferMicrophone.Length);
                                }

                                if (speakerFinished && microphoneFinished) break;

                                // 只要有一方还有数据，就处理整个缓冲区（不足的已补零）
                                int samplesToMix = bufferSpeaker.Length;

                                // 优先处理 IEEE Float 格式
                                if (waveFormatSpeaker.BitsPerSample == 32 &&
                                    waveFormatSpeaker.Encoding == WaveFormatEncoding.IeeeFloat)
                                {
                                    // 处理 32-bit Float 格式
                                    for (int i = 0; i < samplesToMix; i += 4)
                                    {
                                        if (i + 3 >= samplesToMix) break;

                                        float sampleSpeaker = BitConverter.ToSingle(bufferSpeaker, i);
                                        float sampleMicrophone = BitConverter.ToSingle(bufferMicrophone, i);

                                        float mixed = (sampleSpeaker * 0.7f) + (sampleMicrophone * 0.7f);
                                        mixed = Math.Max(-1.0f, Math.Min(1.0f, mixed));

                                        byte[] mixedBytes = BitConverter.GetBytes(mixed);
                                        Array.Copy(mixedBytes, 0, bufferSpeaker, i, 4);
                                    }
                                }
                                else if (waveFormatSpeaker.BitsPerSample == 32)
                                {
                                    for (int i = 0; i < samplesToMix; i += 4)
                                    {
                                        if (i + 3 >= samplesToMix) break;

                                        // 32位整数转float
                                        float sampleSpeaker = BitConverter.ToInt32(bufferSpeaker, i) / 2147483648f;
                                        float sampleMicrophone =
                                            BitConverter.ToInt32(bufferMicrophone, i) / 2147483648f;

                                        // 应用加权混合
                                        float mixed = (sampleSpeaker * 0.7f) + (sampleMicrophone * 0.7f);

                                        // 限制在[-1.0, 1.0]范围内
                                        mixed = Math.Max(-1.0f, Math.Min(1.0f, mixed));

                                        // 转回32位整数
                                        int mixedSample = (int)(mixed * 2147483648f);
                                        byte[] mixedBytes = BitConverter.GetBytes(mixedSample);
                                        Array.Copy(mixedBytes, 0, bufferSpeaker, i, 4);
                                    }
                                }
                                else if (waveFormatSpeaker.BitsPerSample == 16)
                                {
                                    for (int i = 0; i < samplesToMix; i += 2)
                                    {
                                        if (i + 1 >= samplesToMix) break;

                                        // 将16位整数转换为float进行更精确的混音
                                        float sampleSpeaker = BitConverter.ToInt16(bufferSpeaker, i) / 32768f;
                                        float sampleMicrophone = BitConverter.ToInt16(bufferMicrophone, i) / 32768f;

                                        // 应用加权混合
                                        float mixed = (sampleSpeaker * 0.7f) + (sampleMicrophone * 0.7f);

                                        // 限制在[-1.0, 1.0]范围内
                                        mixed = Math.Max(-1.0f, Math.Min(1.0f, mixed));

                                        // 转回16位整数
                                        short mixedSample = (short)(mixed * 32768f);
                                        byte[] mixedBytes = BitConverter.GetBytes(mixedSample);
                                        bufferSpeaker[i] = mixedBytes[0];
                                        bufferSpeaker[i + 1] = mixedBytes[1];
                                    }
                                }
                                else
                                {
                                    throw new NotSupportedException(
                                        $"不支持的位深度或编码: {waveFormatSpeaker.BitsPerSample} bits, {waveFormatSpeaker.Encoding}");
                                }

                                writer.Write(bufferSpeaker, 0, samplesToMix);
                            }
                        }
                    }
                }

                _logms.LogMessage($"混音已完成，文件保存到: {_outputMixedFileName}", "录音器");

                // 根据配置决定是否删除独立录音文件
                bool.TryParse(ConfigurationHelper.GetSetting("保留独立录音文件"), out bool isKeepOriginalFiles);
                if (!isKeepOriginalFiles)
                {
                    // 尝试安全删除源文件
                    DeleteFileSafe(_outputSpeakerFileName);
                    DeleteFileSafe(_outputMicrophoneFileName);
                }
                else
                {
                    _logms.LogMessage($"已保留独立录音文件: {Path.GetFileName(_outputSpeakerFileName)}, {Path.GetFileName(_outputMicrophoneFileName)}", "录音器");
                }
            }
            catch (Exception ex)
            {
                _logms.LogMessage($"混音过程中发生异常: {ex.Message}", "录音器");
            }
        }

        private void DeleteFileSafe(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            // 简单的重试机制，最多尝试3次
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    File.Delete(filePath);
                    return; // 删除成功，退出
                }
                catch (IOException)
                {
                    // 文件可能被占用，等待后重试
                    Thread.Sleep(200);
                }
                catch (Exception ex)
                {
                    _logms.LogMessage($"删除文件 {Path.GetFileName(filePath)} 失败: {ex.Message}", "录音器");
                    return;
                }
            }

            _logms.LogMessage($"删除文件 {Path.GetFileName(filePath)} 失败: 文件可能被占用", "录音器");
        }

        private string ConvertMp3ToWavIfNecessary(string inputFile)
        {
            if (inputFile.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                string wavFile = Path.ChangeExtension(inputFile, ".wav");
                using (var reader = new Mp3FileReader(inputFile))
                using (var writer = new WaveFileWriter(wavFile, reader.WaveFormat))
                {
                    reader.CopyTo(writer);
                }

                return wavFile;
            }

            return inputFile;
        }
    }
}