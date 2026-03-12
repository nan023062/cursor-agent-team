using DnaMcp.Cli;
using DnaMcp.Services;
using DnaMcp.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── CLI 模式：dna-mcp cli <subcommand> [args...] ──────────────────────────────
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

// ── MCP Server 模式（默认）：通过 stdio 与 Cursor 通信 ────────────────────────
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options =>
{
    // MCP stdio 传输要求所有日志走 stderr，避免污染 stdout 的 JSON-RPC 通信
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// 注册全局配置（从环境变量 DNA_MCP_PROJECT_ROOT 读取默认项目根目录）
builder.Services.AddSingleton<ProjectConfig>();

// 注册三层核心服务
builder.Services.AddSingleton<DnaManagerService>();
builder.Services.AddSingleton<TaskSchedulerService>();
builder.Services.AddSingleton<WorkspaceService>();

// 注册 MCP 服务器（stdio 传输，扫描当前程序集中所有 [McpServerToolType]）
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
return 0;
