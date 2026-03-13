using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticOs.Models;
using Microsoft.Extensions.Logging;

namespace AgenticOs.Services;

/// <summary>
/// 任务调度器服务
/// 职责：维护调用栈（Call Stack），实现任务挂起（Push）与恢复（Pop），跨会话持久化
/// </summary>
public class TaskSchedulerService(ILogger<TaskSchedulerService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private const string CallStackFileName = "call-stack.json";
    private const string AgenticOsDirName = ".agentic-os";

    /// <summary>
    /// 将当前任务挂起并压入调用栈（Push）
    /// 返回挂起成功的确认消息和下一步指引
    /// </summary>
    public string SuspendAndPush(
        string projectRoot,
        string currentAssembly,
        string taskDescription,
        string suspendReason,
        string contextSummary,
        string resumeCondition,
        List<SubTask>? subTasks = null)
    {
        var stack = LoadCallStack(projectRoot);

        // 如果栈顶已有该程序集的任务，更新它；否则创建新帧
        var existingFrame = stack.Top;
        if (existingFrame != null &&
            existingFrame.AssemblyName.Equals(currentAssembly, StringComparison.OrdinalIgnoreCase) &&
            existingFrame.Status == TaskFrameStatus.Running)
        {
            existingFrame.Status = TaskFrameStatus.Suspended;
            existingFrame.SuspendReason = suspendReason;
            existingFrame.ResumeCondition = resumeCondition;
            existingFrame.ContextSummary = contextSummary;
            existingFrame.UpdatedAt = DateTime.UtcNow;
            if (subTasks != null) existingFrame.SubTasks = subTasks;
        }
        else
        {
            var frame = new TaskFrame
            {
                AssemblyName = currentAssembly,
                TaskDescription = taskDescription,
                SuspendReason = suspendReason,
                ResumeCondition = resumeCondition,
                ContextSummary = contextSummary,
                Status = TaskFrameStatus.Suspended,
                SubTasks = subTasks ?? []
            };
            stack.Frames.Add(frame);
        }

        SaveCallStack(projectRoot, stack);

        logger.LogInformation("任务已挂起: {Assembly} (栈深度: {Depth})", currentAssembly, stack.Frames.Count);

        return $"""
                [调用栈] 任务已挂起并入栈 ✓
                
                挂起程序集: {currentAssembly}
                挂起原因: {suspendReason}
                恢复条件: {resumeCondition}
                当前栈深度: {stack.Frames.Count}
                
                上下文已保存。请切换到目标程序集继续开发。
                完成后调用 complete_and_pop 恢复本任务。
                """;
    }

    /// <summary>
    /// 完成当前任务并弹出调用栈（Pop），恢复上一层任务的上下文
    /// </summary>
    public (string Message, TaskFrame? RestoredFrame) CompleteAndPop(string projectRoot, string completedAssembly)
    {
        var stack = LoadCallStack(projectRoot);

        if (stack.IsEmpty)
        {
            return ("调用栈为空，没有需要恢复的挂起任务。", null);
        }

        var top = stack.Top!;

        // 验证栈顶是否为期望完成的程序集
        if (!top.AssemblyName.Equals(completedAssembly, StringComparison.OrdinalIgnoreCase))
        {
            return ($"[警告] 栈顶任务是 '{top.AssemblyName}'，但你尝试完成的是 '{completedAssembly}'。\n" +
                    $"请先完成 '{top.AssemblyName}' 的任务，再调用 complete_and_pop。", null);
        }

        top.Status = TaskFrameStatus.Completed;
        top.UpdatedAt = DateTime.UtcNow;
        stack.Frames.RemoveAt(stack.Frames.Count - 1);
        SaveCallStack(projectRoot, stack);

        logger.LogInformation("任务已完成并出栈: {Assembly} (剩余栈深度: {Depth})", completedAssembly, stack.Frames.Count);

        if (stack.IsEmpty)
        {
            return ($"""
                     [调用栈] 程序集 '{completedAssembly}' 开发完成 ✓
                     
                     调用栈已清空，所有任务均已完成！
                     """, null);
        }

        var restored = stack.Top!;
        var message = $"""
                       [调用栈] 程序集 '{completedAssembly}' 开发完成 ✓
                       
                       ═══ 恢复上层任务 ═══
                       程序集: {restored.AssemblyName}
                       任务: {restored.TaskDescription}
                       挂起原因（已解决）: {restored.SuspendReason}
                       
                       上次进展摘要:
                       {restored.ContextSummary}
                       
                       待完成子任务:
                       {FormatSubTasks(restored.SubTasks)}
                       
                       剩余栈深度: {stack.Frames.Count}
                       """;

        restored.Status = TaskFrameStatus.Running;
        restored.UpdatedAt = DateTime.UtcNow;
        SaveCallStack(projectRoot, stack);

        return (message, restored);
    }

    /// <summary>
    /// 获取当前调用栈状态
    /// </summary>
    public CallStack GetCallStack(string projectRoot)
    {
        return LoadCallStack(projectRoot);
    }

    /// <summary>
    /// 更新子任务状态（标记完成/未完成）
    /// </summary>
    public string UpdateTaskStatus(string projectRoot, string assemblyName, int subTaskIndex, bool completed)
    {
        var stack = LoadCallStack(projectRoot);
        var frame = stack.Frames
            .FirstOrDefault(f => f.AssemblyName.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));

        if (frame == null)
            return $"调用栈中未找到程序集 '{assemblyName}' 的任务帧。";

        if (subTaskIndex < 0 || subTaskIndex >= frame.SubTasks.Count)
            return $"子任务索引 {subTaskIndex} 超出范围（共 {frame.SubTasks.Count} 个子任务）。";

        frame.SubTasks[subTaskIndex].Completed = completed;
        frame.UpdatedAt = DateTime.UtcNow;
        SaveCallStack(projectRoot, stack);

        var status = completed ? "✓ 已完成" : "○ 未完成";
        return $"子任务 [{subTaskIndex}] '{frame.SubTasks[subTaskIndex].Description}' 状态已更新为：{status}";
    }

    /// <summary>
    /// 格式化调用栈为人类可读的 Markdown
    /// </summary>
    public string FormatCallStackAsMarkdown(string projectRoot)
    {
        var stack = LoadCallStack(projectRoot);

        if (stack.IsEmpty)
            return "调用栈为空，当前无挂起任务。";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"## Call Stack — {stack.ProjectRoot}");
        sb.AppendLine($"- 更新时间: {stack.UpdatedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine();

        for (int i = stack.Frames.Count - 1; i >= 0; i--)
        {
            var frame = stack.Frames[i];
            var label = i == stack.Frames.Count - 1 ? "### 栈顶（当前执行）" : $"### 栈帧 #{stack.Frames.Count - 1 - i}（已挂起）";
            sb.AppendLine(label);
            sb.AppendLine($"- 程序集: {frame.AssemblyName}");
            sb.AppendLine($"- 任务: {frame.TaskDescription}");
            sb.AppendLine($"- 状态: {frame.Status}");
            if (!string.IsNullOrEmpty(frame.SuspendReason))
                sb.AppendLine($"- 挂起原因: {frame.SuspendReason}");
            if (!string.IsNullOrEmpty(frame.ResumeCondition))
                sb.AppendLine($"- 恢复条件: {frame.ResumeCondition}");
            if (!string.IsNullOrEmpty(frame.ContextSummary))
            {
                sb.AppendLine($"- 上次进展摘要:");
                sb.AppendLine($"  {frame.ContextSummary}");
            }
            if (frame.SubTasks.Count > 0)
            {
                sb.AppendLine("- 子任务:");
                foreach (var st in frame.SubTasks)
                {
                    var check = st.Completed ? "x" : " ";
                    var review = st.NeedsReview ? " [需 review]" : "";
                    sb.AppendLine($"  - [{check}] {st.Description}{review}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── 私有辅助方法 ──────────────────────────────────────────────

    private string GetCallStackPath(string projectRoot)
    {
        var dir = Path.Combine(projectRoot, AgenticOsDirName);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, CallStackFileName);
    }

    private CallStack LoadCallStack(string projectRoot)
    {
        var path = GetCallStackPath(projectRoot);
        if (!File.Exists(path))
            return new CallStack { ProjectRoot = projectRoot };

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CallStack>(json, JsonOptions)
                   ?? new CallStack { ProjectRoot = projectRoot };
        }
        catch (Exception ex)
        {
            logger.LogWarning("读取调用栈文件失败，将使用空栈: {Message}", ex.Message);
            return new CallStack { ProjectRoot = projectRoot };
        }
    }

    private void SaveCallStack(string projectRoot, CallStack stack)
    {
        stack.UpdatedAt = DateTime.UtcNow;
        var path = GetCallStackPath(projectRoot);
        var json = JsonSerializer.Serialize(stack, JsonOptions);
        File.WriteAllText(path, json);
        logger.LogDebug("调用栈已保存: {Path}", path);
    }

    private static string FormatSubTasks(List<SubTask> subTasks)
    {
        if (subTasks.Count == 0) return "（无子任务）";
        return string.Join("\n", subTasks.Select((st, i) =>
        {
            var check = st.Completed ? "x" : " ";
            var review = st.NeedsReview ? " [需 review]" : "";
            return $"  - [{check}] {st.Description}{review}";
        }));
    }
}
