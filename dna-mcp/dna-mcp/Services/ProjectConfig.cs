namespace DnaMcp.Services;

/// <summary>
/// DNA-MCP 全局配置，从环境变量读取默认项目根目录
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// 默认项目根目录。
    /// 优先读取环境变量 DNA_MCP_PROJECT_ROOT，未设置时为空字符串（工具调用时必须手动传入）。
    /// </summary>
    public string DefaultProjectRoot { get; }

    public ProjectConfig()
    {
        DefaultProjectRoot = Environment.GetEnvironmentVariable("DNA_MCP_PROJECT_ROOT") ?? string.Empty;
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
            "未指定 projectRoot，且未配置环境变量 DNA_MCP_PROJECT_ROOT。\n" +
            "请在工具调用时传入 projectRoot 参数，或在 .cursor/mcp.json 的 env 中设置 DNA_MCP_PROJECT_ROOT。");
    }
}
