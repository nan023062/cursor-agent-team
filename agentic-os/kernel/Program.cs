using System.Diagnostics;
using AgenticOs.Cli;
using AgenticOs.Services;
using AgenticOs.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var arg0 = args.Length > 0 ? args[0] : null;
static bool IsArg(string? a, params string[] values) =>
    a != null && Array.Exists(values, v => string.Equals(a, v, StringComparison.OrdinalIgnoreCase));

// ── UI 模式：agentic-os ui / agentic-os --ui（启动独立进程）────────────────────
if (IsArg(arg0, "ui", "--ui"))
{
    var dashboardDir = Path.Combine(AppContext.BaseDirectory, "dashboard");
    var isWindows = OperatingSystem.IsWindows();
    var hostExe = Path.Combine(dashboardDir, isWindows ? "dashboard.exe" : "dashboard");
    var hostDll = Path.Combine(dashboardDir, "dashboard.dll");

    string? fileName;
    string? arguments;
    if (File.Exists(hostExe))
    {
        fileName = hostExe;
        arguments = null;
    }
    else if (File.Exists(hostDll))
    {
        fileName = "dotnet";
        arguments = $"\"{hostDll}\"";
    }
    else
    {
        fileName = null;
        arguments = null;
    }

    if (fileName != null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments ?? "",
            UseShellExecute = false
        };
        using var p = Process.Start(psi);
        const string dashboardUrl = "http://localhost:5050";
        await Task.Delay(1500);
        try
        {
            Process.Start(new ProcessStartInfo { FileName = dashboardUrl, UseShellExecute = true });
        }
        catch { /* 忽略打开浏览器失败 */ }
        p?.WaitForExit();
        return p?.ExitCode ?? 0;
    }
    Console.Error.WriteLine("未找到 dashboard（仪表盘）。请先编译整个解决方案：dotnet build agentic-os/agentic-os.sln");
    return 1;
}

// ── CLI 模式：agentic-os cli <subcommand> [args...] ────────────────────────────
if (IsArg(arg0, "cli"))
{
    using var loggerFactory = LoggerFactory.Create(b =>
        b.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Warning)
         .SetMinimumLevel(LogLevel.Warning));

    var config = new ProjectConfig();
    var dnaManager = new DnaManagerService(loggerFactory.CreateLogger<DnaManagerService>());
    var scheduler = new TaskSchedulerService(loggerFactory.CreateLogger<TaskSchedulerService>());
    var workspace = new WorkspaceService(loggerFactory.CreateLogger<WorkspaceService>());
    var handler = new CliHandler(dnaManager, scheduler, workspace, config);
    return await handler.RunAsync(args);
}

// ── MCP Server 模式（默认）：通过 stdio 与 AI IDE 通信 ────────────────────────
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<ProjectConfig>();

builder.Services.AddSingleton<DnaManagerService>();
builder.Services.AddSingleton<TaskSchedulerService>();
builder.Services.AddSingleton<WorkspaceService>();

// 注册 MCP 服务器（stdio 传输，扫描当前 .NET assembly 中所有 [McpServerToolType]）
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
return 0;
