namespace CallRecording;

public static class GlobalsVariables
{
    private static volatile bool _是否有新版本 = false;
    private static volatile bool _是否正在录音 = false;

    public static bool 是否有新版本
    {
        get => _是否有新版本;
        set => _是否有新版本 = value;
    }

    public static bool 是否正在录音
    {
        get => _是否正在录音;
        set => _是否正在录音 = value;
    }
}