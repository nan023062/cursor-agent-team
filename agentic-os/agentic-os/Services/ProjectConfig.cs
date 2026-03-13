namespace AgenticOs.Services;

/// <summary>
/// Agentic OS 全局配置，从环境变量读取默认项目根目录
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// 默认项目根目录。
    /// 优先读取环境变量 AGENTIC_OS_PROJECT_ROOT，其次 DNA_MCP_PROJECT_ROOT（兼容旧配置），未设置时为空字符串。
    /// </summary>
    public string DefaultProjectRoot { get; }

    public ProjectConfig()
    {
        DefaultProjectRoot = Environment.GetEnvironmentVariable("AGENTIC_OS_PROJECT_ROOT")
            ?? Environment.GetEnvironmentVariable("DNA_MCP_PROJECT_ROOT")
            ?? string.Empty;
    }

    /// <summary>
    /// 解析实际使用的 projectRoot：优先使用调用方传入的值，否则回退到环境变量配置
    /// </summary>
    public string Resolve(string? provided)
    {
        if (!string.IsNullOrWhiteSpace(provided))
            return provided;

        if (!string.IsNullOrWhiteSpace(DefaultProjectRoot))
            return DefaultProjectRoot;

        throw new InvalidOperationException(
            "未指定 projectRoot，且未配置环境变量 AGENTIC_OS_PROJECT_ROOT（或 DNA_MCP_PROJECT_ROOT）。\n" +
            "请在工具调用时传入 projectRoot 参数，或在 .cursor/mcp.json 的 env 中设置 AGENTIC_OS_PROJECT_ROOT。");
    }
}
