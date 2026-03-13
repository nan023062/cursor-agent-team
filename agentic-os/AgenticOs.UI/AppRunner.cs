using System;
using Avalonia;

namespace AgenticOs.UI;

/// <summary>
/// 供主程序 agentic-os 在 --ui 模式下调用的入口
/// </summary>
public static class AppRunner
{
    [STAThread]
    public static int Run(string[] args)
    {
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
