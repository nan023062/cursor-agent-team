namespace DnaMcp.Models;

/// <summary>
/// 调用栈帧状态
/// </summary>
public enum TaskFrameStatus
{
    /// <summary>执行中</summary>
    Running,
    /// <summary>已挂起，等待依赖就绪</summary>
    Suspended,
    /// <summary>已完成</summary>
    Completed
}

/// <summary>
/// 调用栈中的单个任务帧，代表一个被挂起的程序集开发任务
/// </summary>
public class TaskFrame
{
    /// <summary>唯一标识符</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>目标程序集名称</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>任务需求描述</summary>
    public string TaskDescription { get; set; } = string.Empty;

    /// <summary>挂起原因（缺少哪个接口，依赖哪个程序集）</summary>
    public string? SuspendReason { get; set; }

    /// <summary>恢复条件（例如：等待 xxx 程序集的 PublicAPI 包含 yyy 方法）</summary>
    public string? ResumeCondition { get; set; }

    /// <summary>
    /// 上下文摘要：挂起时 AI 当前的关键状态，供恢复时重建注意力。
    /// 例如：已完成的子任务、当前正在实现的方法名、关键设计决策等。
    /// </summary>
    public string ContextSummary { get; set; } = string.Empty;

    /// <summary>子任务列表</summary>
    public List<SubTask> SubTasks { get; set; } = [];

    /// <summary>帧状态</summary>
    public TaskFrameStatus Status { get; set; } = TaskFrameStatus.Running;

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 子任务条目
/// </summary>
public class SubTask
{
    /// <summary>子任务描述</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>是否已完成</summary>
    public bool Completed { get; set; }

    /// <summary>是否需要 review</summary>
    public bool NeedsReview { get; set; }
}

/// <summary>
/// 调用栈持久化数据结构
/// </summary>
public class CallStack
{
    /// <summary>栈帧列表，最后一个为栈顶（当前执行）</summary>
    public List<TaskFrame> Frames { get; set; } = [];

    /// <summary>关联的项目根目录</summary>
    public string ProjectRoot { get; set; } = string.Empty;

    /// <summary>最后更新时间</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>栈顶帧（当前正在执行的任务），栈为空时返回 null</summary>
    public TaskFrame? Top => Frames.Count > 0 ? Frames[^1] : null;

    /// <summary>栈是否为空</summary>
    public bool IsEmpty => Frames.Count == 0;
}
