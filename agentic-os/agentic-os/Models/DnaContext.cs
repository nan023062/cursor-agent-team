namespace AgenticOs.Models;

/// <summary>
/// 视界访问级别，对应 AGENT.md 中的 Context Window Access Level
/// </summary>
public enum ContextLevel
{
    /// <summary>当前程序集：.dna/ 全部 + 全部源码路径</summary>
    Current,
    /// <summary>共享层/软边界：.dna/ 全部 + Public API 源码路径</summary>
    SharedOrSoft,
    /// <summary>硬边界依赖：仅 architecture.md 的 ## Public API 段</summary>
    HardDependency,
    /// <summary>非依赖模块：无权访问，物理隔离</summary>
    Unlinked
}

/// <summary>
/// 程序集 DNA 上下文，由 get_assembly_context 工具按视界分级过滤后返回
/// </summary>
public class DnaContext
{
    /// <summary>程序集名称</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>访问级别</summary>
    public ContextLevel Level { get; set; }

    /// <summary>边界模式</summary>
    public BoundaryMode Boundary { get; set; }

    /// <summary>
    /// architecture.md 内容。
    /// - Current/SharedOrSoft：完整内容
    /// - HardDependency：仅 ## Public API 段
    /// - Unlinked：null（拦截）
    /// </summary>
    public string? ArchitectureContent { get; set; }

    /// <summary>
    /// pitfalls.md 内容。
    /// - Current/SharedOrSoft：完整内容
    /// - HardDependency/Unlinked：null
    /// </summary>
    public string? PitfallsContent { get; set; }

    /// <summary>
    /// dependencies.md 内容。
    /// - Current/SharedOrSoft：完整内容
    /// - HardDependency/Unlinked：null
    /// </summary>
    public string? DependenciesContent { get; set; }

    /// <summary>
    /// wip.md 内容（仅 Current 级别返回）
    /// </summary>
    public string? WipContent { get; set; }

    /// <summary>
    /// 源码文件路径列表（物理过滤后）。
    /// - Current：全部 .cs 文件路径
    /// - SharedOrSoft：Public API 相关源文件路径（需要 AI 进一步加载）
    /// - HardDependency/Unlinked：空列表
    /// </summary>
    public List<string> SourceFilePaths { get; set; } = [];

    /// <summary>越界拦截消息（当 Level = Unlinked 时填充）</summary>
    public string? BlockMessage { get; set; }

    /// <summary>是否被拦截</summary>
    public bool IsBlocked => Level == ContextLevel.Unlinked;
}

/// <summary>
/// 拓扑图查询结果
/// </summary>
public class TopologyResult
{
    /// <summary>所有已注册的程序集节点</summary>
    public List<AssemblyNode> Assemblies { get; set; } = [];

    /// <summary>依赖边列表（from → to 表示 from 依赖 to）</summary>
    public List<DependencyEdge> Edges { get; set; } = [];

    /// <summary>项目根目录</summary>
    public string ProjectRoot { get; set; } = string.Empty;

    /// <summary>扫描时间</summary>
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 依赖边
/// </summary>
public class DependencyEdge
{
    /// <summary>依赖方（上层）</summary>
    public string From { get; set; } = string.Empty;

    /// <summary>被依赖方（下层）</summary>
    public string To { get; set; } = string.Empty;
}

/// <summary>
/// 执行计划结果（拓扑排序后的执行顺序）
/// </summary>
public class ExecutionPlan
{
    /// <summary>按执行顺序排列的程序集名称列表（被依赖方优先）</summary>
    public List<string> OrderedAssemblies { get; set; } = [];

    /// <summary>是否检测到循环依赖</summary>
    public bool HasCycle { get; set; }

    /// <summary>循环依赖描述（HasCycle 为 true 时填充）</summary>
    public string? CycleDescription { get; set; }

    /// <summary>各程序集的变更类型说明</summary>
    public Dictionary<string, string> ChangeTypes { get; set; } = [];
}

/// <summary>
/// 依赖校验结果
/// </summary>
public class DependencyValidationResult
{
    /// <summary>是否合法</summary>
    public bool IsValid { get; set; }

    /// <summary>校验消息</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>被拦截时的标准阻断消息（格式与 AGENT.md 一致）</summary>
    public string? BlockMessage { get; set; }
}
