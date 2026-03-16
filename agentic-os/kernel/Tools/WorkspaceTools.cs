using System.ComponentModel;
using AgenticOs.Services;
using ModelContextProtocol.Server;

namespace AgenticOs.Tools;

/// <summary>
/// 工作区执行器 MCP 工具集
/// </summary>
[McpServerToolType]
public class WorkspaceTools(WorkspaceService workspace, ProjectConfig config)
{
    [McpServerTool, Description(
        "【C# 代码模块专用】使用 Roslyn 从模块目录中精确提取所有 public 成员签名，生成标准 csharp 签名块。" +
        "100% 准确，不会漏掉继承成员或复杂泛型。仅适用于 C# 代码模块。" +
        "提取结果可直接写入 identity.md 的 ## Contract 段。" +
        "参数 modulePath：模块目录的绝对路径。" +
        "参数 writeToIdentity：true 则自动写入 identity.md 的 ## Contract 段，false 则只返回提取结果。")]
    public async Task<string> extract_public_api(
        [Description("模块目录的绝对路径")] string modulePath,
        [Description("true 则自动写入 identity.md 的 ## Contract 段，false 则只返回提取结果")] bool writeToIdentity = false)
    {
        var apiContent = await workspace.ExtractPublicApiAsync(modulePath);

        if (apiContent.StartsWith("错误：") || apiContent.StartsWith("未找到"))
            return apiContent;

        if (writeToIdentity)
        {
            var writeResult = await workspace.WriteContractToIdentityAsync(modulePath, apiContent);
            return $"{writeResult}\n\n提取内容预览：\n{apiContent}";
        }

        return apiContent;
    }

    [McpServerTool, Description(
        "结构化写入教训条目到模块的 lessons.md，并自动同步更新全局 pitfall-index.md。确保格式规范、标签一致。" +
        "参数 modulePath：模块目录的绝对路径。" +
        "参数 tag：标签，例如：#NullRef、#BoundaryViolation、#ApiMisuse。" +
        "参数 rootCause：根因描述（一句话说清楚为什么会出现这个问题）。" +
        "参数 fixMethod：修复方式（具体怎么修的）。" +
        "参数 scope：影响范围（例如：仅当前模块、跨模块、所有依赖方）。" +
        "参数 summary：可选，摘要标题（留空则自动从 rootCause 截取）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT，用于更新 pitfall-index.md）。")]
    public async Task<string> write_lesson(
        [Description("模块目录的绝对路径")] string modulePath,
        [Description("标签，例如：#NullRef、#BoundaryViolation、#ApiMisuse")] string tag,
        [Description("根因描述：一句话说清楚为什么会出现这个问题")] string rootCause,
        [Description("修复方式：具体怎么修的")] string fixMethod,
        [Description("影响范围：例如「仅当前模块」、「跨模块」、「所有依赖方」")] string scope,
        [Description("可选：摘要标题（留空则自动从 rootCause 截取第一句）")] string? summary = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        return await workspace.WritePitfallAsync(modulePath, config.Resolve(projectRoot), tag, rootCause, fixMethod, scope, summary);
    }

    [McpServerTool, Description(
        "追加变更记录到模块的 history.md，自动处理 [BREAKING] 和 [跨模块] 标注。" +
        "参数 modulePath：模块目录的绝对路径。" +
        "参数 changeType：变更类型，例如：feat、fix、refactor、breaking。" +
        "参数 description：变更描述。" +
        "参数 isBreaking：是否为 Breaking Change（会自动添加 [BREAKING] 标注）。" +
        "参数 relatedModules：可选，关联的其他模块名称（跨模块变更时填写，逗号分隔）。")]
    public async Task<string> write_history(
        [Description("模块目录的绝对路径")] string modulePath,
        [Description("变更类型：feat、fix、refactor、perf、docs、breaking 等")] string changeType,
        [Description("变更描述")] string description,
        [Description("是否为 Breaking Change（自动添加 [BREAKING] 标注）")] bool isBreaking = false,
        [Description("可选：关联的其他模块名称，逗号分隔（跨模块变更时填写）")] string? relatedModules = null)
    {
        return await workspace.WriteChangelogAsync(modulePath, changeType, description, isBreaking, relatedModules);
    }

    [McpServerTool, Description(
        "一次性读取模块的完整 .dna/ 上下文，按加载协议顺序（identity → lessons → links → active）组装返回。" +
        "参数 modulePath：模块目录的绝对路径。")]
    public async Task<string> read_dna(
        [Description("模块目录的绝对路径")] string modulePath)
    {
        return await workspace.ReadDnaAsync(modulePath);
    }
}
