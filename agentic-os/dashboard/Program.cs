using System.Collections.Generic;
using System.Linq;
using AgenticOs.Models;
using AgenticOs.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// 内容根固定为程序集所在目录，确保从 kernel 启动时也能找到 wwwroot
var contentRoot = AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = contentRoot,
    Args = args
});

builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.UseUrls("http://127.0.0.1:5050");

builder.Services.AddSingleton<ProjectConfig>();
builder.Services.AddSingleton<DnaManagerService>();
builder.Services.AddSingleton<TaskSchedulerService>();

var app = builder.Build();

var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(wwwroot),
    RequestPath = ""
});

// 根路径显式返回 index.html，避免依赖默认文件解析
app.MapGet("/", () =>
{
    var indexPath = Path.Combine(wwwroot, "index.html");
    if (!File.Exists(indexPath))
        return Results.NotFound("wwwroot/index.html not found");
    return Results.File(indexPath, "text/html; charset=utf-8");
});

app.MapGet("/api/stack", (ProjectConfig config, TaskSchedulerService scheduler) =>
{
    var root = config.DefaultProjectRoot;
    if (string.IsNullOrEmpty(root))
        return Results.Json(new { error = "未配置 AGENTIC_OS_PROJECT_ROOT" }, statusCode: 400);
    var stack = scheduler.GetCallStack(root);
    var frames = new List<object>();
    if (!stack.IsEmpty)
        for (int i = stack.Frames.Count - 1; i >= 0; i--)
        {
            var f = stack.Frames[i];
            var completed = f.SubTasks.Count(static s => s.Completed);
            frames.Add(new
            {
                f.ModuleName,
                f.TaskDescription,
                StatusText = f.Status.ToString(),
                f.SuspendReason,
                f.ResumeCondition,
                SubTasksSummary = f.SubTasks.Count > 0 ? $"{completed}/{f.SubTasks.Count} 子任务完成" : "",
                IsTop = i == stack.Frames.Count - 1,
                Header = i == stack.Frames.Count - 1 ? "▶ 栈顶（当前执行）" : "○ 已挂起"
            });
        }
    return Results.Json(new { projectRoot = root, frames, updatedAt = stack.UpdatedAt });
});

app.MapGet("/api/topology", (ProjectConfig config, DnaManagerService dnaManager) =>
{
    var root = config.DefaultProjectRoot;
    if (string.IsNullOrEmpty(root))
        return Results.Json(new { error = "未配置 AGENTIC_OS_PROJECT_ROOT" }, statusCode: 400);
    var topology = dnaManager.ScanTopology(root);
    var nodes = topology.Modules.Select(m => new
    {
        m.Name,
        Boundary = m.Boundary.ToString(),
        Dependencies = m.Dependencies.Count > 0 ? string.Join(", ", m.Dependencies) : "—",
        m.Maintainer
    }).ToList();
    return Results.Json(new
    {
        projectRoot = topology.ProjectRoot,
        modules = nodes,
        edges = topology.Edges,
        summary = $"共 {topology.Modules.Count} 个模块，{topology.Edges.Count} 条依赖边 · {topology.ScannedAt:yyyy-MM-dd HH:mm}",
        scannedAt = topology.ScannedAt
    });
});

await app.RunAsync();
