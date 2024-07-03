using NAudio.Wave;

namespace CallRecording;

public class Recorder
{
    private bool isRecording;
    private readonly List<byte[]> recordedData = new();
    private WaveFileWriter waveFile;
    private WaveInEvent waveSource;

    public void StartRecording()
    {
        if (isRecording) return;

        var outputFileName = Utils.GenerateFilename();

        waveSource = new WaveInEvent
        {
            WaveFormat = new WaveFormat(44100, 1) // 44100 Hz, Mono
        };

        waveSource.DataAvailable += (sender, e) =>
        {
            var buffer = new byte[e.BytesRecorded];
            Array.Copy(e.Buffer, buffer, e.BytesRecorded);
            recordedData.Add(buffer);
        };

        waveSource.RecordingStopped += (sender, e) =>
        {
            SaveRecording(outputFileName);
            waveSource.Dispose();
            waveFile.Dispose();
        };

        waveFile = new WaveFileWriter(outputFileName, waveSource.WaveFormat);
        waveSource.StartRecording();
        isRecording = true;
        Console.WriteLine("开始录音...");
    }

    public void StopRecording()
    {
        if (!isRecording) return;

        waveSource.StopRecording();
        isRecording = false;
        Console.WriteLine("录音停止，文件已保存。");
    }

    private void SaveRecording(string fileName)
    {
        foreach (var data in recordedData) waveFile.Write(data, 0, data.Length);

        waveFile.Flush();
    }

    public bool IsRecording()
    {
        return isRecording;
    }
}