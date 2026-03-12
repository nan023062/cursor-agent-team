using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DnaMcp.Models;
using DnaMcp.Services;
using ModelContextProtocol.Server;

namespace DnaMcp.Tools;

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
        "扫描项目目录，发现所有含 .dna/ 目录的程序集，返回完整的 DAG 拓扑图（JSON 格式）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）。")]
    public string get_topology(
        [Description("项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var result = dnaManager.ScanTopology(config.Resolve(projectRoot));

        var summary = new
        {
            assemblyCount = result.Assemblies.Count,
            edgeCount = result.Edges.Count,
            scannedAt = result.ScannedAt,
            assemblies = result.Assemblies.Select(a => new
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
        "对给定程序集列表进行拓扑排序，返回正确的执行顺序（被依赖方优先）。" +
        "自动检测循环依赖并报错。" +
        "参数 assemblyNames：涉及的程序集名称列表（逗号分隔）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）。")]
    public string get_execution_plan(
        [Description("涉及的程序集名称列表，逗号分隔，例如：'vena.core,vena.module.combat,vena.ui'")] string assemblyNames,
        [Description("项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var root = config.Resolve(projectRoot);
        var names = assemblyNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var plan = dnaManager.GetExecutionPlan(names, root);

        if (plan.HasCycle)
        {
            return $"[错误] 检测到循环依赖！\n\n{plan.CycleDescription}\n\n" +
                   $"已完成排序的程序集（部分）：{string.Join(" → ", plan.OrderedAssemblies)}";
        }

        var result = new StringBuilder();
        result.AppendLine("## 执行计划（拓扑排序结果）");
        result.AppendLine();
        result.AppendLine("按以下顺序依次开发（被依赖方优先）：");
        result.AppendLine();

        for (int i = 0; i < plan.OrderedAssemblies.Count; i++)
        {
            var assembly = plan.OrderedAssemblies[i];
            result.AppendLine($"{i + 1}. **{assembly}**");
        }

        result.AppendLine();
        result.AppendLine($"执行顺序：{string.Join(" → ", plan.OrderedAssemblies)}");
        result.AppendLine();
        result.AppendLine("每个程序集独立执行 Phase 2 主流程，完成后调用 complete_and_pop 恢复上层任务。");

        return result.ToString();
    }

    [McpServerTool, Description(
        "按视界分级（Context Window Access Level）物理过滤，返回 AI 被允许访问的程序集上下文内容。" +
        "hard 边界只返回 Public API 段，彻底阻断源码访问。" +
        "参数 targetAssembly：目标程序集名称。" +
        "参数 currentAssembly：当前正在开发的程序集名称（留空表示查询模式，返回完整上下文）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）。")]
    public string get_assembly_context(
        [Description("目标程序集名称，例如：'Core' 或 'Services/Audio'")] string targetAssembly,
        [Description("当前正在开发的程序集名称（留空表示查询模式）")] string? currentAssembly = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var current = string.IsNullOrWhiteSpace(currentAssembly) ? null : currentAssembly;
        var context = dnaManager.GetAssemblyContext(targetAssembly, current, config.Resolve(projectRoot));

        if (context.IsBlocked)
        {
            return context.BlockMessage ?? $"[拦截] 无权访问程序集 '{targetAssembly}'。";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# 程序集上下文 — {targetAssembly}");
        sb.AppendLine($"- **访问级别**: {context.Level}");
        sb.AppendLine($"- **边界模式**: {context.Boundary.ToString().ToLower()}");
        sb.AppendLine();

        if (context.Level == ContextLevel.HardDependency)
        {
            sb.AppendLine("> [物理过滤] 硬边界程序集：仅返回 Public API 签名，源码已屏蔽。");
            sb.AppendLine();
        }

        if (context.ArchitectureContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## architecture.md");
            sb.AppendLine(context.ArchitectureContent);
            sb.AppendLine();
        }

        if (context.PitfallsContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## pitfalls.md");
            sb.AppendLine(context.PitfallsContent);
            sb.AppendLine();
        }

        if (context.DependenciesContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## dependencies.md");
            sb.AppendLine(context.DependenciesContent);
            sb.AppendLine();
        }

        if (context.WipContent != null)
        {
            sb.AppendLine("---");
            sb.AppendLine("## wip.md（进行中任务）");
            sb.AppendLine(context.WipContent);
            sb.AppendLine();
        }

        if (context.SourceFilePaths.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine($"## 可访问的源文件（共 {context.SourceFilePaths.Count} 个）");
            foreach (var path in context.SourceFilePaths)
                sb.AppendLine($"- {path}");
        }

        return sb.ToString();
    }

    [McpServerTool, Description(
        "校验程序集 A 调用程序集 B 的依赖是否在 dependencies.md 白名单内，以及指定 API 是否在 Public API 中。" +
        "越界直接返回标准阻断消息。" +
        "参数 callerAssembly：调用方程序集名称。" +
        "参数 calleeAssembly：被调用方程序集名称。" +
        "参数 apiName：可选，要校验的具体 API 方法名（留空只校验依赖声明）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）。")]
    public string validate_dependency(
        [Description("调用方程序集名称")] string callerAssembly,
        [Description("被调用方程序集名称")] string calleeAssembly,
        [Description("可选：要校验的具体 API 方法名（留空只校验依赖声明是否存在）")] string? apiName = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var result = dnaManager.ValidateDependency(callerAssembly, calleeAssembly, apiName, config.Resolve(projectRoot));

        if (!result.IsValid && result.BlockMessage != null)
            return result.BlockMessage;

        return result.IsValid
            ? $"✓ {result.Message}"
            : $"✗ {result.Message}";
    }

    [McpServerTool, Description(
        "将新程序集目录注册到 DNA 拓扑图（创建 .dna/ 目录）。" +
        "参数 assemblyPath：程序集目录路径（绝对路径或相对于 projectRoot 的路径）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）。")]
    public string register_assembly(
        [Description("程序集目录路径（绝对路径或相对于 projectRoot 的路径）")] string assemblyPath,
        [Description("项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）")] string? projectRoot = null)
    {
        return dnaManager.RegisterAssembly(assemblyPath, config.Resolve(projectRoot));
    }
}
