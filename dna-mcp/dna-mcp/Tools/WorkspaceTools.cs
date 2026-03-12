using System.ComponentModel;
using DnaMcp.Services;
using ModelContextProtocol.Server;

namespace DnaMcp.Tools;

/// <summary>
/// 工作区执行器 MCP 工具集
/// </summary>
[McpServerToolType]
public class WorkspaceTools(WorkspaceService workspace, ProjectConfig config)
{
    [McpServerTool, Description(
        "使用 Roslyn 从 C# 程序集目录中精确提取所有 public 成员签名，生成标准 csharp 签名块。100% 准确，不会漏掉继承成员或复杂泛型。" +
        "提取结果可直接写入 architecture.md 的 ## Public API 段。" +
        "参数 assemblyPath：程序集目录的绝对路径。" +
        "参数 writeToArchitecture：true 则自动写入 architecture.md，false 则只返回提取结果。")]
    public async Task<string> extract_public_api(
        [Description("程序集目录的绝对路径")] string assemblyPath,
        [Description("true 则自动写入 architecture.md 的 ## Public API 段，false 则只返回提取结果")] bool writeToArchitecture = false)
    {
        var apiContent = await workspace.ExtractPublicApiAsync(assemblyPath);

        if (apiContent.StartsWith("错误：") || apiContent.StartsWith("未找到"))
            return apiContent;

        if (writeToArchitecture)
        {
            var writeResult = await workspace.WritePublicApiToArchitectureAsync(assemblyPath, apiContent);
            return $"{writeResult}\n\n提取内容预览：\n{apiContent}";
        }

        return apiContent;
    }

    [McpServerTool, Description(
        "结构化写入 pitfall 教训条目到程序集的 pitfalls.md，并自动同步更新全局 pitfall-index.md。确保格式规范、标签一致。" +
        "参数 assemblyPath：程序集目录的绝对路径。" +
        "参数 tag：标签，例如：#NullRef、#BoundaryViolation、#ApiMisuse。" +
        "参数 rootCause：根因描述（一句话说清楚为什么会出现这个问题）。" +
        "参数 fixMethod：修复方式（具体怎么修的）。" +
        "参数 scope：影响范围（例如：仅当前程序集、跨程序集、所有调用方）。" +
        "参数 summary：可选，摘要标题（留空则自动从 rootCause 截取）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT，用于更新 pitfall-index.md）。")]
    public async Task<string> write_pitfall(
        [Description("程序集目录的绝对路径")] string assemblyPath,
        [Description("标签，例如：#NullRef、#BoundaryViolation、#ApiMisuse")] string tag,
        [Description("根因描述：一句话说清楚为什么会出现这个问题")] string rootCause,
        [Description("修复方式：具体怎么修的")] string fixMethod,
        [Description("影响范围：例如「仅当前程序集」、「跨程序集」、「所有调用方」")] string scope,
        [Description("可选：摘要标题（留空则自动从 rootCause 截取第一句）")] string? summary = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 DNA_MCP_PROJECT_ROOT）")] string? projectRoot = null)
    {
        return await workspace.WritePitfallAsync(assemblyPath, config.Resolve(projectRoot), tag, rootCause, fixMethod, scope, summary);
    }

    [McpServerTool, Description(
        "追加变更记录到程序集的 changelog.md，自动处理 [BREAKING] 和 [跨程序集] 标注。" +
        "参数 assemblyPath：程序集目录的绝对路径。" +
        "参数 changeType：变更类型，例如：feat、fix、refactor、breaking。" +
        "参数 description：变更描述。" +
        "参数 isBreaking：是否为 Breaking Change（会自动添加 [BREAKING] 标注）。" +
        "参数 relatedAssemblies：可选，关联的其他程序集名称（跨程序集变更时填写，逗号分隔）。")]
    public async Task<string> write_changelog(
        [Description("程序集目录的绝对路径")] string assemblyPath,
        [Description("变更类型：feat、fix、refactor、perf、docs、breaking 等")] string changeType,
        [Description("变更描述")] string description,
        [Description("是否为 Breaking Change（自动添加 [BREAKING] 标注）")] bool isBreaking = false,
        [Description("可选：关联的其他程序集名称，逗号分隔（跨程序集变更时填写）")] string? relatedAssemblies = null)
    {
        return await workspace.WriteChangelogAsync(assemblyPath, changeType, description, isBreaking, relatedAssemblies);
    }

    [McpServerTool, Description(
        "一次性读取程序集的完整 .dna/ 上下文，按加载协议顺序（architecture → pitfalls → dependencies → wip）组装返回。" +
        "参数 assemblyPath：程序集目录的绝对路径。")]
    public async Task<string> read_dna(
        [Description("程序集目录的绝对路径")] string assemblyPath)
    {
        return await workspace.ReadDnaAsync(assemblyPath);
    }
}
