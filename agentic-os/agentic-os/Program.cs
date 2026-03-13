using System.Diagnostics;
using AgenticOs.Cli;
using AgenticOs.Services;
using AgenticOs.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── UI 模式：agentic-os ui / agentic-os --ui（启动独立进程）────────────────────
if (args.Length > 0 && (args[0].Equals("ui", StringComparison.OrdinalIgnoreCase) || args[0].Equals("--ui", StringComparison.OrdinalIgnoreCase)))
{
    var baseDir = AppContext.BaseDirectory;
    var uiExe = Path.Combine(baseDir, "agentic-os-ui.dll");
    if (!File.Exists(uiExe))
        uiExe = Path.Combine(baseDir, "AgenticOs.UI.dll");
    if (!File.Exists(uiExe))
        uiExe = Path.GetFullPath(Path.Combine(baseDir, "../../../AgenticOs.UI/bin/Debug/net8.0/AgenticOs.UI.dll"));
    if (!File.Exists(uiExe))
        uiExe = Path.GetFullPath(Path.Combine(baseDir, "../../../AgenticOs.UI/bin/Release/net8.0/AgenticOs.UI.dll"));

    if (File.Exists(uiExe))
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"\"{uiExe}\"",
            UseShellExecute = false
        };
        using var p = System.Diagnostics.Process.Start(psi);
        p?.WaitForExit();
        return p?.ExitCode ?? 0;
    }
    Console.Error.WriteLine("未找到 AgenticOs.UI。请先编译整个解决方案：dotnet build agentic-os.sln");
    return 1;
}

// ── CLI 模式：agentic-os cli <subcommand> [args...] ────────────────────────────
if (args.Length > 0 && args[0].Equals("cli", StringComparison.OrdinalIgnoreCase))
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
