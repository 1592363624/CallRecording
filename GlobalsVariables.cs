namespace CallRecording;

public class GlobalsVariables
{
    public static class gv
    {
        public static string script = @"@echo off
setlocal enabledelayedexpansion

REM 提权并隐藏窗口
NET SESSION >nul 2>&1
IF %ERRORLEVEL% NEQ 0 (
    echo Set UAC = CreateObject^(""Shell.Application""^) > ""%temp%\Elevate.vbs""
    echo UAC.ShellExecute ""%~f0"", ""%*"", """", ""runas"", 0 >> ""%temp%\Elevate.vbs""
    cscript //nologo ""%temp%\Elevate.vbs"" & del ""%temp%\Elevate.vbs""
    exit /b
)

REM ################ 核心修改部分 ################
REM 通过参数获取目标路径（优先级最高）
set ""TargetDir=%~1""

REM 如果未传参数，尝试从环境变量读取
if not defined TargetDir set ""TargetDir=%CALL_RECORDING_DIR%""

REM 如果仍未定义，使用默认安全路径
if not defined TargetDir (
    echo 错误：未指定安装路径，请通过参数或环境变量设置
    pause
    exit /b 1
)

REM 标准化路径格式（去除末尾反斜杠）
set ""TargetDir=%TargetDir:\=/%""
set ""TargetDir=%TargetDir:/=\%""
if ""%TargetDir:~-1%""==""\"" set ""TargetDir=%TargetDir:~0,-1%""
REM #############################################

REM 结束目标进程
tasklist /FI ""IMAGENAME eq CallRecording.exe"" 2>NUL | find /I ""CallRecording.exe"" >NUL && taskkill /F /IM ""CallRecording.exe""

REM 解压到动态路径
powershell -Command ""Expand-Archive -Path 'C:\Shell\Download\CallRecording.zip' -DestinationPath '%TargetDir%' -Force""

REM 清理ZIP文件
del /F /Q ""C:\Shell\Download\CallRecording.zip"" >nul 2>&1

REM 启动程序（使用动态路径）
start """" /D ""%TargetDir%"" CallRecording.exe

REM 自删除
start """" /B cmd /c ""timeout /t 1 /nobreak >nul & del /F /Q ""%~f0""""
exit
";
    }
}