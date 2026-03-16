using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticOs.Models;
using AgenticOs.Services;
using ModelContextProtocol.Server;

namespace AgenticOs.Tools;

/// <summary>
/// DNA 拓扑管理器 MCP 工具集
/// </summary>
[McpServerToolType]
public class DnaManagerTools(DnaManagerService dnaManager, ProjectConfig config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description(
        "扫描项目目录，发现所有含 .dna/ 目录的模块，返回完整的 DAG 拓扑图（JSON 格式）。" +
        "模块可以是代码、美术资产、策划文档、音视频等任何类型的工作内容单元。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string get_topology(
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var result = dnaManager.ScanTopology(config.Resolve(projectRoot));

        var summary = new
        {
            moduleCount = result.Modules.Count,
            edgeCount = result.Edges.Count,
            scannedAt = result.ScannedAt,
            modules = result.Modules.Select(a => new
            {
                name = a.Name,
                path = a.Path,
                boundary = a.Boundary.ToString().ToLower(),
                dependencies = a.Dependencies,
                maintainer = a.Maintainer,
                hasDna = a.HasDna
            }),
            edges = result.Edges.Select(e => new { from = e.From, to = e.To })
        };

        return JsonSerializer.Serialize(summary, JsonOptions);
    }

    [McpServerTool, Description(
        "对给定模块列表进行拓扑排序，返回正确的执行顺序（被依赖方优先）。" +
        "自动检测循环依赖并报错。" +
        "参数 moduleNames：涉及的模块名称列表（逗号分隔）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string get_execution_plan(
        [Description("涉及的模块名称列表，逗号分隔，例如：'Core,Art/Characters,Design/Combat'")] string moduleNames,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var root = config.Resolve(projectRoot);
        var names = moduleNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var plan = dnaManager.GetExecutionPlan(names, root);

        if (plan.HasCycle)
        {
            return $"[错误] 检测到循环依赖！\n\n{plan.CycleDescription}\n\n" +
                   $"已完成排序的模块（部分）：{string.Join(" → ", plan.OrderedModules)}";
        }

        var result = new StringBuilder();
        result.AppendLine("## 执行计划（拓扑排序结果）");
        result.AppendLine();
        result.AppendLine("按以下顺序依次执行（被依赖方优先）：");
        result.AppendLine();

        for (int i = 0; i < plan.OrderedModules.Count; i++)
        {
            var module = plan.OrderedModules[i];
            result.AppendLine($"{i + 1}. **{module}**");
        }

        result.AppendLine();
        result.AppendLine($"执行顺序：{string.Join(" → ", plan.OrderedModules)}");
        result.AppendLine();
        result.AppendLine("每个模块独立执行工作流程，完成后调用 complete_and_pop 恢复上层任务。");

        return result.ToString();
    }

    [McpServerTool, Description(
        "按视界分级（Context Window Access Level）物理过滤，返回 AI 被允许访问的模块上下文内容。" +
        "hard 边界只返回 ## Contract 段，彻底阻断内部内容访问。" +
        "参数 targetModule：目标模块名称。" +
        "参数 currentModule：当前正在操作的模块名称（留空表示查询模式，返回完整上下文）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string get_module_context(
        [Description("目标模块名称，例如：'Core' 或 'Art/Characters'")] string targetModule,
        [Description("当前正在操作的模块名称（留空表示查询模式）")] string? currentModule = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var current = string.IsNullOrWhiteSpace(currentModule) ? null : currentModule;
        var context = dnaManager.GetModuleContext(targetModule, current, config.Resolve(projectRoot));

        if (context.IsBlocked)
        {
            return context.BlockMessage ?? $"[拦截] 无权访问模块 '{targetModule}'。";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# 模块上下文 — {targetModule}");
        sb.AppendLine($"- **访问级别**: {context.Level}");
        sb.AppendLine($"- **边界模式**: {context.Boundary.ToString().ToLower()}");
        sb.AppendLine();

        if (context.Level == ContextLevel.HardDependency)
        {
            sb.AppendLine("> [物理过滤] 硬边界模块：仅返回 ## Contract 段，内部内容已屏蔽。");
            sb.AppendLine();
        }

        if (context.IdentityContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## identity.md");
            sb.AppendLine(context.IdentityContent);
            sb.AppendLine();
        }

        if (context.LessonsContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## lessons.md");
            sb.AppendLine(context.LessonsContent);
            sb.AppendLine();
        }

        if (context.LinksContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## links.md");
            sb.AppendLine(context.LinksContent);
            sb.AppendLine();
        }

        if (context.ActiveContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## active.md（进行中任务）");
            sb.AppendLine(context.ActiveContent);
            sb.AppendLine();
        }

        if (context.ContentFilePaths.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## 可访问的内容文件（共 {context.ContentFilePaths.Count} 个）");
            foreach (var path in context.ContentFilePaths)
                sb.AppendLine($"- {path}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description(
        "校验模块 A 对模块 B 的依赖是否在 links.md 白名单内，以及指定的接口/职责是否在对方的 ## Contract 段中。" +
        "越界直接返回标准阻断消息。" +
        "参数 callerModule：调用方模块名称。" +
        "参数 calleeModule：被调用方模块名称。" +
        "参数 apiName：可选，要校验的具体接口/职责名（留空只校验依赖声明）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string validate_dependency(
        [Description("调用方模块名称")] string callerModule,
        [Description("被调用方模块名称")] string calleeModule,
        [Description("可选：要校验的具体接口/职责名（留空只校验依赖声明是否存在）")] string? apiName = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var result = dnaManager.ValidateDependency(callerModule, calleeModule, apiName, config.Resolve(projectRoot));

        if (!result.IsValid && result.BlockMessage != null)
            return result.BlockMessage;

        return result.IsValid
            ? $"✓ {result.Message}"
            : $"✗ {result.Message}";
    }

    [McpServerTool, Description(
        "将新模块目录注册到 DNA 拓扑图（创建 .dna/ 目录）。" +
        "模块可以是代码、美术、策划、文档等任何类型的工作内容单元。" +
        "参数 modulePath：模块目录路径（绝对路径或相对于 projectRoot 的路径）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string register_module(
        [Description("模块目录路径（绝对路径或相对于 projectRoot 的路径）")] string modulePath,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        return dnaManager.RegisterModule(modulePath, config.Resolve(projectRoot));
    }
}
