using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticOs.Models;
using AgenticOs.Services;

namespace AgenticOs.Cli;

/// <summary>
/// CLI 模式入口：解析子命令，调用对应 Service，将结果输出到 stdout
///
/// 用法：
///   agentic-os cli [子命令] [参数...]
///
/// 子命令列表：
///   topology  [projectRoot]                    — 打印模块拓扑图
///   stack     [projectRoot]                    — 查看当前调用栈
///   context   [moduleName] [projectRoot]       — 查看模块 DNA 上下文摘要
///   plan      [module1,module2,...] [projectRoot] — 生成执行计划
///   validate  [caller] [callee] [projectRoot]  — 校验依赖关系
///   dna       [modulePath]                     — 读取模块完整 DNA
///   help                                       — 显示帮助
/// </summary>
public class CliHandler(
    DnaManagerService dnaManager,
    TaskSchedulerService scheduler,
    WorkspaceService workspace,
    ProjectConfig config)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<int> RunAsync(string[] args)
    {
        var subCommand = args.Length > 1 ? args[1].ToLower() : "help";

        try
        {
            return subCommand switch
            {
                "topology" or "topo" => RunTopology(args),
                "stack" or "callstack" => RunStack(args),
                "context" or "ctx" => RunContext(args),
                "plan" => RunPlan(args),
                "validate" or "val" => RunValidate(args),
                "dna" => await RunDna(args),
                "help" or "--help" or "-h" => RunHelp(),
                _ => RunUnknown(subCommand)
            };
        }
        catch (InvalidOperationException ex)
        {
            WriteError(ex.Message);
            return 1;
        }
    }

    // ── 子命令实现 ────────────────────────────────────────────────

    private int RunTopology(string[] args)
    {
        var root = args.Length > 2 ? args[2] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"DNA 拓扑图 — {projectRoot}");

        var result = dnaManager.ScanTopology(projectRoot);

        if (result.Modules.Count == 0)
        {
            WriteWarning("未发现任何含 .dna/ 目录的模块。请先初始化模块（创建 .dna/ 目录）。");
            return 0;
        }

        Console.WriteLine($"  共 {result.Modules.Count} 个模块，{result.Edges.Count} 条依赖边");
        Console.WriteLine();

        var groups = result.Modules.GroupBy(a => a.Boundary).OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            var icon = group.Key switch
            {
                BoundaryMode.Shared => "◈",
                BoundaryMode.Soft => "◇",
                _ => "◆"
            };
            WriteSection($"{icon} {group.Key} 边界（{group.Count()} 个）");
            foreach (var module in group.OrderBy(a => a.Name))
            {
                var deps = module.Dependencies.Count > 0
                    ? $"  → [{string.Join(", ", module.Dependencies)}]"
                    : "";
                var maintainer = module.Maintainer != null ? $"  @{module.Maintainer}" : "";
                Console.WriteLine($"  {module.Name}{maintainer}{deps}");
            }
            Console.WriteLine();
        }

        if (result.Edges.Count > 0)
        {
            WriteSection("依赖关系");
            foreach (var edge in result.Edges.OrderBy(e => e.From))
            {
                Console.WriteLine($"  {edge.From}  →  {edge.To}");
            }
        }

        return 0;
    }

    private int RunStack(string[] args)
    {
        var root = args.Length > 2 ? args[2] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"调用栈状态 — {projectRoot}");

        var stack = scheduler.GetCallStack(projectRoot);

        if (stack.IsEmpty)
        {
            WriteSuccess("调用栈为空，当前无挂起任务。");
            return 0;
        }

        Console.WriteLine($"  栈深度: {stack.Frames.Count}  |  更新时间: {stack.UpdatedAt:yyyy-MM-dd HH:mm} UTC");
        Console.WriteLine();

        for (int i = stack.Frames.Count - 1; i >= 0; i--)
        {
            var frame = stack.Frames[i];
            var isTop = i == stack.Frames.Count - 1;
            var label = isTop ? "▶ 栈顶（当前执行）" : $"  栈帧 #{stack.Frames.Count - 1 - i}（已挂起）";
            var statusColor = frame.Status == TaskFrameStatus.Running ? ConsoleColor.Green : ConsoleColor.Yellow;

            Console.ForegroundColor = isTop ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
            Console.WriteLine($"  {label}");
            Console.ResetColor();

            Console.WriteLine($"    模块:    {frame.ModuleName}");
            Console.WriteLine($"    任务:    {frame.TaskDescription}");

            Console.ForegroundColor = statusColor;
            Console.WriteLine($"    状态:    {frame.Status}");
            Console.ResetColor();

            if (!string.IsNullOrEmpty(frame.SuspendReason))
                Console.WriteLine($"    挂起原因: {frame.SuspendReason}");
            if (!string.IsNullOrEmpty(frame.ResumeCondition))
                Console.WriteLine($"    恢复条件: {frame.ResumeCondition}");

            if (frame.SubTasks.Count > 0)
            {
                var done = frame.SubTasks.Count(s => s.Completed);
                Console.WriteLine($"    子任务:  {done}/{frame.SubTasks.Count} 已完成");
                foreach (var st in frame.SubTasks)
                {
                    var check = st.Completed ? "✓" : "○";
                    Console.ForegroundColor = st.Completed ? ConsoleColor.Green : ConsoleColor.DarkGray;
                    Console.WriteLine($"      {check} {st.Description}");
                    Console.ResetColor();
                }
            }

            if (!string.IsNullOrEmpty(frame.ContextSummary))
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"    上次进展: {TruncateText(frame.ContextSummary, 80)}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        return 0;
    }

    private int RunContext(string[] args)
    {
        if (args.Length < 3)
        {
            WriteError("用法: agentic-os cli context [模块名称] [projectRoot?]");
            return 1;
        }

        var moduleName = args[2];
        var root = args.Length > 3 ? args[3] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"模块上下文摘要 — {moduleName}");

        var topology = dnaManager.ScanTopology(projectRoot);
        var module = topology.Modules
            .FirstOrDefault(a => a.Name.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

        if (module == null)
        {
            WriteError($"模块 '{moduleName}' 未在拓扑图中找到。");
            Console.WriteLine();
            Console.WriteLine("已注册的模块：");
            foreach (var a in topology.Modules.OrderBy(x => x.Name))
                Console.WriteLine($"  - {a.Name}");
            return 1;
        }

        var boundaryIcon = module.Boundary switch
        {
            BoundaryMode.Shared => "◈ shared",
            BoundaryMode.Soft => "◇ soft",
            _ => "◆ hard"
        };

        Console.WriteLine($"  路径:     {module.Path}");
        Console.WriteLine($"  边界:     {boundaryIcon}");
        if (module.Maintainer != null)
            Console.WriteLine($"  维护者:   @{module.Maintainer}");
        if (module.Dependencies.Count > 0)
            Console.WriteLine($"  依赖:     {string.Join(", ", module.Dependencies)}");

        Console.WriteLine();

        WriteSection(".dna/ 文件状态");
        PrintDnaFileStatus("architecture.md", module.ArchitecturePath);
        PrintDnaFileStatus("pitfalls.md", module.PitfallsPath);
        PrintDnaFileStatus("dependencies.md", module.DependenciesPath);
        PrintDnaFileStatus("changelog.md", module.ChangelogPath);
        PrintDnaFileStatus("wip.md", module.WipPath);

        return 0;
    }

    private int RunPlan(string[] args)
    {
        if (args.Length < 3)
        {
            WriteError("用法: agentic-os cli plan [模块1,模块2,...] [projectRoot?]");
            return 1;
        }

        var moduleNames = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var root = args.Length > 3 ? args[3] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"执行计划 — {string.Join(", ", moduleNames)}");

        var plan = dnaManager.GetExecutionPlan(moduleNames, projectRoot);

        if (plan.HasCycle)
        {
            WriteError("检测到循环依赖！");
            Console.WriteLine(plan.CycleDescription);
            return 1;
        }

        Console.WriteLine("  按以下顺序依次执行（被依赖方优先）：");
        Console.WriteLine();
        for (int i = 0; i < plan.OrderedModules.Count; i++)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {i + 1}. ");
            Console.ResetColor();
            Console.WriteLine(plan.OrderedModules[i]);
        }
        Console.WriteLine();
        Console.WriteLine($"  执行顺序：{string.Join(" → ", plan.OrderedModules)}");

        return 0;
    }

    private int RunValidate(string[] args)
    {
        if (args.Length < 4)
        {
            WriteError("用法: agentic-os cli validate [调用方] [被调用方] [projectRoot?]");
            return 1;
        }

        var caller = args[2];
        var callee = args[3];
        var root = args.Length > 4 ? args[4] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"依赖校验 — {caller} → {callee}");

        var result = dnaManager.ValidateDependency(caller, callee, null, projectRoot);

        if (result.IsValid)
        {
            WriteSuccess(result.Message);
        }
        else
        {
            WriteError(result.Message);
            if (result.BlockMessage != null)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(result.BlockMessage);
                Console.ResetColor();
            }
            return 1;
        }

        return 0;
    }

    private async Task<int> RunDna(string[] args)
    {
        if (args.Length < 3)
        {
            WriteError("用法: agentic-os cli dna [模块目录路径]");
            return 1;
        }

        var modulePath = args[2];
        WriteHeader($"DNA 上下文 — {Path.GetFileName(modulePath)}");

        var content = await workspace.ReadDnaAsync(modulePath);
        Console.WriteLine(content);
        return 0;
    }

    private int RunHelp()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  Agentic OS CLI — 工作区状态查询工具");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  用法: agentic-os cli <子命令> [参数...]");
        Console.WriteLine();

        WriteSection("子命令");
        var commands = new[]
        {
            ("topology [root]",              "扫描并显示模块 DAG 拓扑图"),
            ("stack    [root]",              "查看当前调用栈（挂起任务状态）"),
            ("context  <module> [root]",     "查看指定模块的 DNA 上下文摘要"),
            ("plan     <m1,m2,...> [root]",  "生成跨模块执行计划（拓扑排序）"),
            ("validate <caller> <callee> [root]", "校验模块依赖关系"),
            ("dna      <modulePath>",        "读取模块完整 .dna/ 内容"),
            ("help",                         "显示此帮助"),
        };

        foreach (var (cmd, desc) in commands)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  {cmd,-38}");
            Console.ResetColor();
            Console.WriteLine(desc);
        }

        Console.WriteLine();
        WriteSection("环境变量");
        Console.WriteLine("  AGENTIC_OS_PROJECT_ROOT    默认项目根目录（省略 [root] 参数时使用）");
        Console.WriteLine();

        WriteSection("示例");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  agentic-os cli topology");
        Console.WriteLine("  agentic-os cli stack");
        Console.WriteLine("  agentic-os cli context Core");
        Console.WriteLine("  agentic-os cli plan Core,Art/Characters,Design/Combat");
        Console.WriteLine("  agentic-os cli validate Art/Characters Core");
        Console.WriteLine("  agentic-os cli dna C:\\MyProject\\src\\Core");
        Console.ResetColor();
        Console.WriteLine();

        return 0;
    }

    private int RunUnknown(string subCommand)
    {
        WriteError($"未知子命令: '{subCommand}'");
        Console.WriteLine("运行 'agentic-os cli help' 查看可用命令。");
        return 1;
    }

    // ── 输出辅助方法 ──────────────────────────────────────────────

    private static void WriteHeader(string title)
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"  ╔═ {title}");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void WriteSection(string title)
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  ── {title} ──");
        Console.ResetColor();
    }

    private static void WriteSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {message}");
        Console.ResetColor();
    }

    private static void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  ⚠ {message}");
        Console.ResetColor();
    }

    private static void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {message}");
        Console.ResetColor();
    }

    private static void PrintDnaFileStatus(string fileName, string filePath)
    {
        if (File.Exists(filePath))
        {
            var info = new FileInfo(filePath);
            var lines = File.ReadLines(filePath).Count();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  ✓ {fileName,-20}");
            Console.ResetColor();
            Console.WriteLine($"  {lines} 行  ({info.LastWriteTime:yyyy-MM-dd HH:mm})");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  ○ {fileName,-20}  （不存在）");
            Console.ResetColor();
        }
    }

    private static string TruncateText(string text, int maxLength)
    {
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "...";
    }
}
