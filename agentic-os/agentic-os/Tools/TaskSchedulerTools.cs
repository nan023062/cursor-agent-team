using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticOs.Models;
using AgenticOs.Services;
using ModelContextProtocol.Server;

namespace AgenticOs.Tools;

/// <summary>
/// 任务调度器 MCP 工具集
/// </summary>
[McpServerToolType]
public class TaskSchedulerTools(TaskSchedulerService scheduler, ProjectConfig config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool, Description(
        "将当前模块的工作任务挂起并压入调用栈（Push），保存完整上下文状态，供后续恢复。" +
        "典型场景：操作模块 A 时发现需要模块 B 先完善，挂起 A，转去处理 B。" +
        "参数 currentModule：当前正在操作的模块名称。" +
        "参数 taskDescription：任务需求描述。" +
        "参数 suspendReason：挂起原因（例如：等待 Core 模块提供 IEventBus 接口）。" +
        "参数 contextSummary：当前进展摘要（已完成的步骤、关键设计决策、下次恢复时需要知道的信息）。" +
        "参数 resumeCondition：恢复条件（例如：Core/.dna/architecture.md 职责声明包含 IEventBus）。" +
        "参数 subTasks：可选，JSON 格式的子任务列表，格式：[{\"description\":\"...\",\"completed\":false}]" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string suspend_and_push(
        [Description("当前正在操作的模块名称")] string currentModule,
        [Description("任务需求描述")] string taskDescription,
        [Description("挂起原因，例如：等待 Core 模块提供 IEventBus 接口")] string suspendReason,
        [Description("当前进展摘要：已完成的步骤、关键设计决策、下次恢复时需要知道的信息")] string contextSummary,
        [Description("恢复条件，例如：Core/.dna/architecture.md 职责声明包含 IEventBus")] string resumeCondition,
        [Description("可选：JSON 格式的子任务列表，格式：[{\"description\":\"...\",\"completed\":false,\"needsReview\":false}]")] string? subTasksJson = null,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        List<SubTask>? subTasks = null;
        if (!string.IsNullOrWhiteSpace(subTasksJson))
        {
            try
            {
                subTasks = JsonSerializer.Deserialize<List<SubTask>>(subTasksJson, JsonOptions);
            }
            catch
            {
                return $"错误：subTasksJson 格式不正确，请使用 JSON 数组格式：[{{\"description\":\"...\",\"completed\":false}}]";
            }
        }

        return scheduler.SuspendAndPush(
            config.Resolve(projectRoot), currentModule, taskDescription,
            suspendReason, contextSummary, resumeCondition, subTasks);
    }

    [McpServerTool, Description(
        "标记当前模块任务完成并弹出调用栈（Pop），自动恢复上一层挂起任务的完整上下文。" +
        "参数 completedModule：刚完成的模块名称（必须与栈顶一致）。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string complete_and_pop(
        [Description("刚完成的模块名称（必须与当前栈顶模块一致）")] string completedModule,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        var (message, _) = scheduler.CompleteAndPop(config.Resolve(projectRoot), completedModule);
        return message;
    }

    [McpServerTool, Description(
        "查看当前调用栈的完整状态，包括所有挂起任务的上下文摘要和子任务进度。以结构化 Markdown 格式返回。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string get_call_stack(
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        return scheduler.FormatCallStackAsMarkdown(config.Resolve(projectRoot));
    }

    [McpServerTool, Description(
        "更新调用栈中指定模块任务的子任务完成状态（标记完成或未完成）。" +
        "参数 moduleName：目标模块名称。" +
        "参数 subTaskIndex：子任务索引（从 0 开始）。" +
        "参数 completed：true 表示完成，false 表示未完成。" +
        "参数 projectRoot：项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）。")]
    public string update_task_status(
        [Description("目标模块名称")] string moduleName,
        [Description("子任务索引（从 0 开始）")] int subTaskIndex,
        [Description("true 表示标记为完成，false 表示标记为未完成")] bool completed,
        [Description("项目根目录的绝对路径（留空则使用环境变量 AGENTIC_OS_PROJECT_ROOT）")] string? projectRoot = null)
    {
        return scheduler.UpdateTaskStatus(config.Resolve(projectRoot), moduleName, subTaskIndex, completed);
    }
}
