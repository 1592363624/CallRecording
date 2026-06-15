using System.IO;
using CallRecording.Models;
using MySharedProject.Model;
using static CallRecording.Models.Recorder;

namespace CallRecording.Services;

public class RecordingService : IDisposable
{
    private readonly Logms _logms;
    private Recorder _recorder;
    private string _recordingSavePath;
    private AudioFormat _selectedFormat;
    private bool _disposed = false;

    public event EventHandler RecordingStarted;
    public event EventHandler RecordingStopped;
    public event EventHandler RecordingPaused;
    public event EventHandler RecordingResumed;

    public RecordingService(Logms logms)
    {
        _logms = logms;
        InitializeRecorder();
        LoadSettings();
    }

    private void InitializeRecorder()
    {
        _recorder = new Recorder(_logms, _selectedFormat);
        _recorder.RecordingStopped += OnRecorderStopped;
    }

    private void LoadSettings()
    {
        _recordingSavePath = AppDomain.CurrentDomain.BaseDirectory + "Recordings";
        if (!Directory.Exists(_recordingSavePath))
        {
            Directory.CreateDirectory(_recordingSavePath);
            ConfigurationHelper.SetSetting("OutputDirectory", _recordingSavePath);
        }

        _recordingSavePath = ConfigurationHelper.GetSetting("OutputDirectory");
        string? root = Path.GetPathRoot(_recordingSavePath);
        if (string.IsNullOrEmpty(root))
        {
            ConfigurationHelper.SetSetting("OutputDirectory", AppDomain.CurrentDomain.BaseDirectory + "Recordings");
            _recordingSavePath = AppDomain.CurrentDomain.BaseDirectory + "Recordings";
        }

        string formatStr = ConfigurationHelper.GetSetting("音频格式");
        _selectedFormat = formatStr == "MP3" ? AudioFormat.MP3 : AudioFormat.WAV;
    }

    public string RecordingSavePath
    {
        get => _recordingSavePath;
        set
        {
            if (_recordingSavePath != value)
            {
                _recordingSavePath = value;
                ConfigurationHelper.SetSetting("OutputDirectory", value);
            }
        }
    }

    public AudioFormat SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (_selectedFormat != value)
            {
                if (IsRecording())
                {
                    throw new InvalidOperationException("Cannot change format while recording");
                }

                _selectedFormat = value;
                _recorder.UpdateAudioFormat(value);
                ConfigurationHelper.SetSetting("音频格式", value.ToString());
            }
        }
    }

    public bool IsKeepOriginalFiles
    {
        get
        {
            bool.TryParse(ConfigurationHelper.GetSetting("保留独立录音文件"), out bool result);
            return result;
        }
        set { ConfigurationHelper.SetSetting("保留独立录音文件", value.ToString()); }
    }

    public void StartRecording(string softwareName = "通话")
    {
        if (!IsRecording())
        {
            _recorder.StartRecording(_recordingSavePath, softwareName);
            RecordingStarted?.Invoke(this, EventArgs.Empty);
        }
    }

    public void StopRecording()
    {
        if (IsRecording())
        {
            _recorder.StopRecording();
        }
    }

    public void PauseRecording()
    {
        if (IsRecording() && !IsPaused())
        {
            _recorder.PauseRecording();
            RecordingPaused?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ResumeRecording()
    {
        if (IsRecording() && IsPaused())
        {
            _recorder.ResumeRecording();
            RecordingResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ToggleRecording()
    {
        if (IsRecording())
        {
            if (IsPaused())
            {
                ResumeRecording();
            }
            else
            {
                PauseRecording();
            }
        }
        else
        {
            StartRecording();
        }
    }

    public bool IsRecording()
    {
        return _recorder.IsRecording();
    }

    public bool IsPaused()
    {
        return _recorder.IsPaused();
    }

    private void OnRecorderStopped(object sender, EventArgs e)
    {
        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            if (_recorder != null)
            {
                _recorder.RecordingStopped -= OnRecorderStopped;
                if (_recorder.IsRecording())
                {
                    _recorder.StopRecording();
                }

                _recorder = null;
            }
        }

        _disposed = true;
    }

    ~RecordingService()
    {
        Dispose(false);
    }
}