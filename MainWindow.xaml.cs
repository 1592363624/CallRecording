using System.Windows;

namespace CallRecording;

/// <summary>
///     Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Strating();
    }

    public static void Strating()
    {
        Console.WriteLine("开始监控语音通话...");

        var recorder = new Recorder();

        while (true)
        {
            if (WeChatCallDetector.IsWeChatCallActive())
            {
                if (!recorder.IsRecording())
                {
                    Console.WriteLine("检测到微信通话，开始录音...");
                    recorder.StartRecording();
                }
            }
            else
            {
                if (recorder.IsRecording())
                {
                    Console.WriteLine("通话结束，停止录音并保存文件...");
                    recorder.StopRecording();
                }
            }

            Thread.Sleep(1000); // 每1秒检查一次通话状态
        }
    }
}