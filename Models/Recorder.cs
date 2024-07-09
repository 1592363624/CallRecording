using System.IO;
using NAudio.Wave;

namespace CallRecording.Models;

public class Recorder
{
    private readonly object _lockObject = new();
    private readonly Logger _logger;
    private bool _isRecording;
    private string _outputFileName;
    private WaveFileWriter _waveFile;
    private WaveInEvent _waveSource;

    public Recorder(Logger logger)
    {
        _logger = logger;
    }

    public void StartRecording(string savePath)
    {
        lock (_lockObject)
        {
            if (_isRecording) return;

            _outputFileName = Path.Combine(savePath, Utils.GenerateFilename());
            _waveSource = new WaveInEvent { WaveFormat = new WaveFormat(44100, 1) }; // 44100 Hz, Mono
            _waveSource.DataAvailable += OnDataAvailable;
            _waveSource.RecordingStopped += OnRecordingStopped;
            _waveFile = new WaveFileWriter(_outputFileName, _waveSource.WaveFormat);
            _waveSource.StartRecording();
            _isRecording = true;
            _logger.LogMessage("开始录音...");
        }
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        lock (_lockObject)
        {
            if (_waveFile != null)
            {
                _waveFile.Write(e.Buffer, 0, e.BytesRecorded);
                _waveFile.Flush();
            }
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs e)
    {
        lock (_lockObject)
        {
            try
            {
                _waveSource.Dispose();
                _waveFile?.Dispose();
                _waveSource = null;
                _waveFile = null;

                if (e.Exception != null)
                    _logger.LogMessage($"录音停止时发生异常: {e.Exception.Message}");
                else
                    _logger.LogMessage($"录音已保存到: {_outputFileName}");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"在处理录音停止事件时发生异常: {ex.Message}");
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
                _waveSource.StopRecording();
                _logger.LogMessage("录音停止，文件已保存。");
            }
            catch (Exception ex)
            {
                _logger.LogMessage($"停止录音时发生异常: {ex.Message}");
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
}