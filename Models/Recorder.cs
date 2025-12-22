using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Lame;
using NAudio.Wave;

namespace CallRecording.Models
{
    public class Recorder
    {
        public enum AudioFormat
        {
            MP3,
            WAV
        }

        private readonly object _lockObject = new();
        private readonly Logms _logms;
        private bool _isMixing = false;
        private bool _isRecording;
        private bool _isPaused = false; // 添加暂停状态标志
        public WasapiLoopbackCapture _loopbackSource;
        private WasapiCapture _microphoneSource;
        private LameMP3FileWriter _mp3MicrophoneFile;
        private LameMP3FileWriter _mp3SpeakerFile;
        private string _outputMicrophoneFileName;
        private string _outputMixedFileName;
        private string _outputSpeakerFileName;
        private AudioFormat _selectedFormat;
        private WaveFileWriter _waveMicrophoneFile;
        private WaveFileWriter _waveSpeakerFile;

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

                string extension = _selectedFormat == AudioFormat.MP3 ? "mp3" : "wav";
                _outputSpeakerFileName = Utils.GenerateFilename(savePath, softwareName + "_speaker", extension);
                _outputMicrophoneFileName = Utils.GenerateFilename(savePath, softwareName + "_microphone", extension);
                _outputMixedFileName = Utils.GenerateFilename(savePath, softwareName + "_mixed", extension);

                try
                {
                    _loopbackSource = new WasapiLoopbackCapture { WaveFormat = new WaveFormat(48000, 2) };
                    _microphoneSource = new WasapiCapture { WaveFormat = new WaveFormat(48000, 2) };

                    if (_selectedFormat == AudioFormat.WAV)
                    {
                        _waveSpeakerFile = new WaveFileWriter(_outputSpeakerFileName, _loopbackSource.WaveFormat);
                        _waveMicrophoneFile =
                            new WaveFileWriter(_outputMicrophoneFileName, _microphoneSource.WaveFormat);

                        _loopbackSource.DataAvailable += (s, e) =>
                        {
                            if (!_isPaused) // 添加暂停检查
                                _waveSpeakerFile.Write(e.Buffer, 0, e.BytesRecorded);
                        };
                        _microphoneSource.DataAvailable +=
                            (s, e) =>
                            {
                                if (!_isPaused) // 添加暂停检查
                                    _waveMicrophoneFile.Write(e.Buffer, 0, e.BytesRecorded);
                            };
                    }
                    else if (_selectedFormat == AudioFormat.MP3)
                    {
                        _mp3SpeakerFile = new LameMP3FileWriter(_outputSpeakerFileName, _loopbackSource.WaveFormat,
                            LAMEPreset.STANDARD);
                        _mp3MicrophoneFile = new LameMP3FileWriter(_outputMicrophoneFileName,
                            _microphoneSource.WaveFormat, LAMEPreset.STANDARD);

                        _loopbackSource.DataAvailable += (s, e) =>
                        {
                            if (!_isPaused) // 添加暂停检查
                                _mp3SpeakerFile.Write(e.Buffer, 0, e.BytesRecorded);
                        };
                        _microphoneSource.DataAvailable +=
                            (s, e) =>
                            {
                                if (!_isPaused) // 添加暂停检查
                                    _mp3MicrophoneFile.Write(e.Buffer, 0, e.BytesRecorded);
                            };
                    }

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

        public void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            lock (_lockObject)
            {
                try
                {
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
        }

        private void MixAudio()
        {
            try
            {
                // 确保录音文件已释放
                Cleanup();

                string extension = _selectedFormat == AudioFormat.MP3 ? "mp3" : "wav";
                _outputMixedFileName = Path.ChangeExtension(_outputMixedFileName, extension);

                if (_selectedFormat == AudioFormat.WAV)
                {
                    using (var readerSpeaker = new AudioFileReader(_outputSpeakerFileName))
                    using (var readerMicrophone = new AudioFileReader(_outputMicrophoneFileName))
                    {
                        var waveFormat = readerSpeaker.WaveFormat;
                        if (!waveFormat.Equals(readerMicrophone.WaveFormat))
                        {
                            throw new InvalidOperationException("录制的两个音频文件格式不一致，无法混音");
                        }

                        using (var waveFileWriter = new WaveFileWriter(_outputMixedFileName, waveFormat))
                        {
                            var buffer1 = new float[readerSpeaker.WaveFormat.SampleRate *
                                                    readerSpeaker.WaveFormat.Channels];
                            var buffer2 = new float[readerMicrophone.WaveFormat.SampleRate *
                                                    readerMicrophone.WaveFormat.Channels];

                            int readSpeaker, readMicrophone;
                            while ((readSpeaker = readerSpeaker.Read(buffer1, 0, buffer1.Length)) > 0)
                            {
                                readMicrophone = readerMicrophone.Read(buffer2, 0, buffer2.Length);

                                // 确保读取的样本数相同
                                int samplesToMix = Math.Min(readSpeaker, readMicrophone);
                                for (int i = 0; i < samplesToMix; i++)
                                {
                                    buffer1[i] = (buffer1[i] + buffer2[i]) / 2; // 防止溢出，音量混合
                                }

                                waveFileWriter.WriteSamples(buffer1, 0, samplesToMix);
                            }
                        }
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

                            int readSpeaker, readMicrophone;
                            while ((readSpeaker = readerSpeaker.Read(bufferSpeaker, 0, bufferSpeaker.Length)) > 0)
                            {
                                readMicrophone = readerMicrophone.Read(bufferMicrophone, 0, bufferMicrophone.Length);

                                // 确保读取的样本数相同
                                int samplesToMix = Math.Min(readSpeaker, readMicrophone);

                                // 使用改进的混音算法
                                if (waveFormatSpeaker.BitsPerSample == 16)
                                {
                                    for (int i = 0; i < samplesToMix; i += 2)
                                    {
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
                                        Array.Copy(mixedBytes, 0, bufferSpeaker, i, 2);
                                    }
                                }
                                else if (waveFormatSpeaker.BitsPerSample == 32)
                                {
                                    for (int i = 0; i < samplesToMix; i += 4)
                                    {
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
                                else
                                {
                                    throw new NotSupportedException("不支持的位深度");
                                }

                                writer.Write(bufferSpeaker, 0, samplesToMix);
                            }
                        }
                    }
                }

                _logms.LogMessage($"混音已完成，文件保存到: {_outputMixedFileName}", "录音器");

                // 尝试安全删除源文件
                DeleteFileSafe(_outputSpeakerFileName);
                DeleteFileSafe(_outputMicrophoneFileName);
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