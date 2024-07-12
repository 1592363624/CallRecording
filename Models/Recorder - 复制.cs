//using System;
//using NAudio.CoreAudioApi;
//using NAudio.Wave;
//保留单独和混音文件版本
//namespace CallRecording.Models
//{
//    public class Recorder
//    {
//        private readonly object _lockObject = new();
//        private readonly Logger _logger;
//        private bool _isRecording;
//        private string _outputSpeakerFileName;
//        private string _outputMicrophoneFileName;
//        private string _outputMixedFileName;
//        private WaveFileWriter _waveSpeakerFile;
//        private WaveFileWriter _waveMicrophoneFile;
//        private WasapiLoopbackCapture _loopbackSource;
//        private WasapiCapture _microphoneSource;

//        public Recorder(Logger logger)
//        {
//            _logger = logger;
//        }

//        public void StartRecording(string savePath, string softwareName)
//        {
//            lock (_lockObject)
//            {
//                if (_isRecording) return;

//                _outputSpeakerFileName = Utils.GenerateFilename(savePath, softwareName + "_speaker");
//                _outputMicrophoneFileName = Utils.GenerateFilename(savePath, softwareName + "_microphone");
//                _outputMixedFileName = Utils.GenerateFilename(savePath, softwareName + "_mixed");
//                try
//                {
//                    _loopbackSource = new WasapiLoopbackCapture { WaveFormat = new WaveFormat(44100, 2) };
//                    _microphoneSource = new WasapiCapture { WaveFormat = new WaveFormat(44100, 2) };

//                    _loopbackSource.DataAvailable += (s, e) => _waveSpeakerFile.Write(e.Buffer, 0, e.BytesRecorded);
//                    _microphoneSource.DataAvailable += (s, e) => _waveMicrophoneFile.Write(e.Buffer, 0, e.BytesRecorded);

//                    _loopbackSource.RecordingStopped += OnRecordingStopped;
//                    _microphoneSource.RecordingStopped += OnRecordingStopped;

//                    _waveSpeakerFile = new WaveFileWriter(_outputSpeakerFileName, _loopbackSource.WaveFormat);
//                    _waveMicrophoneFile = new WaveFileWriter(_outputMicrophoneFileName, _microphoneSource.WaveFormat);

//                    _loopbackSource.StartRecording();
//                    _microphoneSource.StartRecording();
//                    _isRecording = true;

//                    _logger.LogMessage("开始录音...", softwareName);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogMessage($"初始化录音源时发生异常: {ex.Message}", "录音器");
//                    Cleanup();
//                }
//            }
//        }

//        private void OnRecordingStopped(object sender, StoppedEventArgs e)
//        {
//            lock (_lockObject)
//            {
//                try
//                {
//                    Cleanup();

//                    if (e.Exception != null)
//                        _logger.LogMessage($"录音停止时发生异常: {e.Exception.Message}", "录音器");
//                    else
//                        _logger.LogMessage($"录音已保存到: {_outputSpeakerFileName} 和 {_outputMicrophoneFileName}", "录音器");

//                    // 开始混音处理
//                    MixAudio();
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogMessage($"在处理录音停止事件时发生异常: {ex.Message}", "录音器");
//                }
//            }
//        }

//        public void StopRecording()
//        {
//            lock (_lockObject)
//            {
//                if (!_isRecording) return;

//                try
//                {
//                    _loopbackSource?.StopRecording();
//                    _microphoneSource?.StopRecording();
//                    _logger.LogMessage("录音停止，文件已保存。", "录音器");
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogMessage($"停止录音时发生异常: {ex.Message}", "录音器");
//                }

//                _isRecording = false;
//            }
//        }

//        public bool IsRecording()
//        {
//            lock (_lockObject)
//            {
//                return _isRecording;
//            }
//        }

//        private void Cleanup()
//        {
//            _loopbackSource?.Dispose();
//            _microphoneSource?.Dispose();
//            _waveSpeakerFile?.Dispose();
//            _waveMicrophoneFile?.Dispose();

//            _loopbackSource = null;
//            _microphoneSource = null;
//            _waveSpeakerFile = null;
//            _waveMicrophoneFile = null;
//        }

//        private void MixAudio()
//        {
//            using var readerSpeaker = new AudioFileReader(_outputSpeakerFileName);
//            using var readerMicrophone = new AudioFileReader(_outputMicrophoneFileName);
//            var waveFormat = readerSpeaker.WaveFormat;
//            if (!waveFormat.Equals(readerMicrophone.WaveFormat))
//            {
//                throw new InvalidOperationException("录制的两个音频文件格式不一致，无法混音");
//            }

//            using var waveFileWriter = new WaveFileWriter(_outputMixedFileName, waveFormat);

//            var buffer1 = new float[readerSpeaker.WaveFormat.SampleRate * readerSpeaker.WaveFormat.Channels];
//            var buffer2 = new float[readerMicrophone.WaveFormat.SampleRate * readerMicrophone.WaveFormat.Channels];

//            int readSpeaker, readMicrophone;
//            while ((readSpeaker = readerSpeaker.Read(buffer1, 0, buffer1.Length)) > 0 &&
//                   (readMicrophone = readerMicrophone.Read(buffer2, 0, buffer2.Length)) > 0)
//            {
//                for (int i = 0; i < readSpeaker; i++)
//                {
//                    buffer1[i] += buffer2[i];
//                }
//                waveFileWriter.WriteSamples(buffer1, 0, readSpeaker);
//            }

//            _logger.LogMessage($"混音已完成，文件保存到: {_outputMixedFileName}", "录音器");
//        }
//    }
//}
