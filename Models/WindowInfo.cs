using System.Runtime.InteropServices;
using System.Text;

public class WindowInfo
{
    // 定义样式常量
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;

    // 窗口样式标志 (部分示例)
    private const uint WS_CAPTION = 0x00C00000;
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_THICKFRAME = 0x00040000;

    // 扩展样式标志 (部分示例)
    private const uint WS_EX_LEFT = 0x00000000;
    private const uint WS_EX_WINDOWEDGE = 0x00000100;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    // 获取窗口样式并解析
    public static string GetWindowStyles(IntPtr hWnd)
    {
        int styleValue = GetWindowLong(hWnd, GWL_STYLE);
        StringBuilder styles = new StringBuilder();

        // 按位检查样式
        if ((styleValue & WS_CAPTION) != 0) styles.AppendLine("WS_CAPTION");
        if ((styleValue & WS_POPUP) != 0) styles.AppendLine("WS_POPUP");
        if ((styleValue & WS_VISIBLE) != 0) styles.AppendLine("WS_VISIBLE");
        if ((styleValue & WS_THICKFRAME) != 0) styles.AppendLine("WS_THICKFRAME");
        return styles.ToString();
    }

    // 获取扩展样式并解析
    public static string GetExtendedStyles(IntPtr hWnd)
    {
        int exStyleValue = GetWindowLong(hWnd, GWL_EXSTYLE);
        StringBuilder exStyles = new StringBuilder();

        // 按位检查扩展样式
        if ((exStyleValue & WS_EX_LEFT) != 0) exStyles.AppendLine("WS_EX_LEFT");
        if ((exStyleValue & WS_EX_WINDOWEDGE) != 0) exStyles.AppendLine("WS_EX_WINDOWEDGE");
        return exStyles.ToString();
    }

    // 获取窗口类名
    public static string GetWindowClassName(IntPtr hWnd)
    {
        StringBuilder className = new StringBuilder(256);
        GetClassName(hWnd, className, className.Capacity);
        return className.ToString();
    }
}