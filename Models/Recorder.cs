using System;
using System.IO;
using System.Threading;
using System.Windows;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CallRecording.Models
{
    public class Recorder
    {
        private readonly object _lockObject = new();
        private readonly Logger _logger;
        private bool _isRecording;
        private string _outputSpeakerFileName;
        private string _outputMicrophoneFileName;
        private string _outputMixedFileName;
        private WaveFileWriter _waveSpeakerFile;
        private WaveFileWriter _waveMicrophoneFile;
        private WasapiLoopbackCapture _loopbackSource;
        private WasapiCapture _microphoneSource;
        private bool _isMixing = false; // 添加一个标志位
        public Recorder(Logger logger)
        {
            _logger = logger;
        }

        public void StartRecording(string savePath, string softwareName)
        {
            lock (_lockObject)
            {
                if (_isRecording) return;

                _outputSpeakerFileName = Utils.GenerateFilename(savePath, softwareName + "_speaker");
                _outputMicrophoneFileName = Utils.GenerateFilename(savePath, softwareName + "_microphone");
                _outputMixedFileName = Utils.GenerateFilename(savePath, softwareName + "_mixed");
                try
                {
                    _loopbackSource = new WasapiLoopbackCapture { WaveFormat = new WaveFormat(44100, 2) };
                    _microphoneSource = new WasapiCapture { WaveFormat = new WaveFormat(44100, 2) };

                    _waveSpeakerFile = new WaveFileWriter(_outputSpeakerFileName, _loopbackSource.WaveFormat);
                    _waveMicrophoneFile = new WaveFileWriter(_outputMicrophoneFileName, _microphoneSource.WaveFormat);

                    _loopbackSource.DataAvailable += (s, e) => _waveSpeakerFile.Write(e.Buffer, 0, e.BytesRecorded);
                    _microphoneSource.DataAvailable += (s, e) => _waveMicrophoneFile.Write(e.Buffer, 0, e.BytesRecorded);

                    _loopbackSource.RecordingStopped += OnRecordingStopped;
                    //_microphoneSource.RecordingStopped += OnRecordingStopped;

                    _loopbackSource.StartRecording();
                    _microphoneSource.StartRecording();
                    _isRecording = true;

                    _logger.LogMessage("开始录音...", softwareName);
                }
                catch (Exception ex)
                {
                    _logger.LogMessage($"初始化录音源时发生异常: {ex.Message}", "录音器");
                    Cleanup();
                }
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            lock (_lockObject)
            {
                try
                {
                    Cleanup();

                    if (e.Exception != null)
                        _logger.LogMessage($"录音停止时发生异常: {e.Exception.Message}", "录音器");
                    else
                        //_logger.LogMessage($"录音已保存到: {_outputSpeakerFileName} 和 {_outputMicrophoneFileName}", "录音器");

                        // 确保混音操作只进行一次
                        //if (!_isMixing)
                        //{
                        //    _isMixing = true;
                        // 开始混音处理
                        MixAudio();
                    //}
                }
                catch (Exception ex)
                {
                    _logger.LogMessage($"在处理录音停止事件时发生异常: {ex.Message}", "录音器");
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
                    _logger.LogMessage("录音停止，文件已保存。", "录音器");
                }
                catch (Exception ex)
                {
                    _logger.LogMessage($"停止录音时发生异常: {ex.Message}", "录音器");
                }

                _isRecording = false;
            }
        }

        public bool IsRecording()
        {
            lock (_lockObject)
            {
                return _isRecording;
            }
        }

        private void Cleanup()
        {
            _loopbackSource?.Dispose();
            _microphoneSource?.Dispose();
            _waveSpeakerFile?.Dispose();
            _waveMicrophoneFile?.Dispose();

            _loopbackSource = null;
            _microphoneSource = null;
            _waveSpeakerFile = null;
            _waveMicrophoneFile = null;
        }

        private void MixAudio()
        {
            try
            {
                // 确保录音文件已释放
                Cleanup();

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
                        var buffer1 = new float[readerSpeaker.WaveFormat.SampleRate * readerSpeaker.WaveFormat.Channels];
                        var buffer2 = new float[readerMicrophone.WaveFormat.SampleRate * readerMicrophone.WaveFormat.Channels];

                        int readSpeaker, readMicrophone;
                        while ((readSpeaker = readerSpeaker.Read(buffer1, 0, buffer1.Length)) > 0 &&
                               (readMicrophone = readerMicrophone.Read(buffer2, 0, buffer2.Length)) > 0)
                        {
                            for (int i = 0; i < readSpeaker; i++)
                            {
                                buffer1[i] += buffer2[i];
                            }
                            waveFileWriter.WriteSamples(buffer1, 0, readSpeaker);
                        }
                    }

                    _logger.LogMessage($"混音已完成，文件保存到: {_outputMixedFileName}", "录音器");
                }

                // 等待文件流完全释放
                Thread.Sleep(1000);

                // 删除单独录音的文件
                DeleteFile(_outputSpeakerFileName);
                DeleteFile(_outputMicrophoneFileName);
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"混音过程中发生异常: {ex.Message}", "录音器");
            }
            //finally
            //{
            //    _isMixing = false; // 重置混音标志
            //}

        }

        private void DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    //_logger.LogMessage($"文件已删除: {filePath}", "录音器");
                }
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"删除文件时发生异常: {ex.Message}", "录音器");
            }
        }
    }
}
