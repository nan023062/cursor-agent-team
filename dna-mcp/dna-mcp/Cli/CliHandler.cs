using System.Text.Json;
using System.Text.Json.Serialization;
using DnaMcp.Models;
using DnaMcp.Services;

namespace DnaMcp.Cli;

/// <summary>
/// CLI 模式入口：解析子命令，调用对应 Service，将结果输出到 stdout
///
/// 用法：
///   dna-mcp cli [子命令] [参数...]
///
/// 子命令列表：
///   topology  [projectRoot]              — 打印程序集拓扑图
///   stack     [projectRoot]              — 查看当前调用栈
///   context   [assemblyName] [projectRoot] — 查看程序集 DNA 上下文摘要
///   plan      [assembly1,assembly2,...] [projectRoot] — 生成执行计划
///   validate  [caller] [callee] [projectRoot] — 校验依赖关系
///   dna       [assemblyPath]             — 读取程序集完整 DNA
///   help                                 — 显示帮助
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
        // args[0] 已经是 "cli"，从 args[1] 开始是子命令
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
        // dna-mcp cli topology [projectRoot]
        var root = args.Length > 2 ? args[2] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"DNA 拓扑图 — {projectRoot}");

        var result = dnaManager.ScanTopology(projectRoot);

        if (result.Assemblies.Count == 0)
        {
            WriteWarning("未发现任何含 .dna/ 目录的程序集。请先执行 @coder init 初始化程序集。");
            return 0;
        }

        Console.WriteLine($"  共 {result.Assemblies.Count} 个程序集，{result.Edges.Count} 条依赖边");
        Console.WriteLine();

        // 按边界类型分组显示
        var groups = result.Assemblies.GroupBy(a => a.Boundary).OrderBy(g => g.Key);
        foreach (var group in groups)
        {
            var icon = group.Key switch
            {
                BoundaryMode.Shared => "◈",
                BoundaryMode.Soft => "◇",
                _ => "◆"
            };
            WriteSection($"{icon} {group.Key} 边界（{group.Count()} 个）");
            foreach (var assembly in group.OrderBy(a => a.Name))
            {
                var deps = assembly.Dependencies.Count > 0
                    ? $"  → [{string.Join(", ", assembly.Dependencies)}]"
                    : "";
                var maintainer = assembly.Maintainer != null ? $"  @{assembly.Maintainer}" : "";
                Console.WriteLine($"  {assembly.Name}{maintainer}{deps}");
            }
            Console.WriteLine();
        }

        // 显示依赖关系图（简化版）
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
        // dna-mcp cli stack [projectRoot]
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

            Console.WriteLine($"    程序集:  {frame.AssemblyName}");
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
        // dna-mcp cli context [assemblyName] [projectRoot]
        if (args.Length < 3)
        {
            WriteError("用法: dna-mcp cli context [程序集名称] [projectRoot?]");
            return 1;
        }

        var assemblyName = args[2];
        var root = args.Length > 3 ? args[3] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"程序集上下文摘要 — {assemblyName}");

        var topology = dnaManager.ScanTopology(projectRoot);
        var assembly = topology.Assemblies
            .FirstOrDefault(a => a.Name.Equals(assemblyName, StringComparison.OrdinalIgnoreCase));

        if (assembly == null)
        {
            WriteError($"程序集 '{assemblyName}' 未在拓扑图中找到。");
            Console.WriteLine();
            Console.WriteLine("已注册的程序集：");
            foreach (var a in topology.Assemblies.OrderBy(x => x.Name))
                Console.WriteLine($"  - {a.Name}");
            return 1;
        }

        var boundaryIcon = assembly.Boundary switch
        {
            BoundaryMode.Shared => "◈ shared",
            BoundaryMode.Soft => "◇ soft",
            _ => "◆ hard"
        };

        Console.WriteLine($"  路径:     {assembly.Path}");
        Console.WriteLine($"  边界:     {boundaryIcon}");
        if (assembly.Maintainer != null)
            Console.WriteLine($"  维护者:   @{assembly.Maintainer}");
        if (assembly.Dependencies.Count > 0)
            Console.WriteLine($"  依赖:     {string.Join(", ", assembly.Dependencies)}");

        Console.WriteLine();

        // 显示 .dna/ 文件摘要
        WriteSection(".dna/ 文件状态");
        PrintDnaFileStatus("architecture.md", assembly.ArchitecturePath);
        PrintDnaFileStatus("pitfalls.md", assembly.PitfallsPath);
        PrintDnaFileStatus("dependencies.md", assembly.DependenciesPath);
        PrintDnaFileStatus("changelog.md", assembly.ChangelogPath);
        PrintDnaFileStatus("wip.md", assembly.WipPath);

        return 0;
    }

    private int RunPlan(string[] args)
    {
        // dna-mcp cli plan [assembly1,assembly2,...] [projectRoot]
        if (args.Length < 3)
        {
            WriteError("用法: dna-mcp cli plan [程序集1,程序集2,...] [projectRoot?]");
            return 1;
        }

        var assemblyNames = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var root = args.Length > 3 ? args[3] : null;
        var projectRoot = config.Resolve(root);

        WriteHeader($"执行计划 — {string.Join(", ", assemblyNames)}");

        var plan = dnaManager.GetExecutionPlan(assemblyNames, projectRoot);

        if (plan.HasCycle)
        {
            WriteError("检测到循环依赖！");
            Console.WriteLine(plan.CycleDescription);
            return 1;
        }

        Console.WriteLine("  按以下顺序依次开发（被依赖方优先）：");
        Console.WriteLine();
        for (int i = 0; i < plan.OrderedAssemblies.Count; i++)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"  {i + 1}. ");
            Console.ResetColor();
            Console.WriteLine(plan.OrderedAssemblies[i]);
        }
        Console.WriteLine();
        Console.WriteLine($"  执行顺序：{string.Join(" → ", plan.OrderedAssemblies)}");

        return 0;
    }

    private int RunValidate(string[] args)
    {
        // dna-mcp cli validate [caller] [callee] [projectRoot]
        if (args.Length < 4)
        {
            WriteError("用法: dna-mcp cli validate [调用方] [被调用方] [projectRoot?]");
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
        // dna-mcp cli dna [assemblyPath]
        if (args.Length < 3)
        {
            WriteError("用法: dna-mcp cli dna [程序集目录路径]");
            return 1;
        }

        var assemblyPath = args[2];
        WriteHeader($"DNA 上下文 — {Path.GetFileName(assemblyPath)}");

        var content = await workspace.ReadDnaAsync(assemblyPath);
        Console.WriteLine(content);
        return 0;
    }

    private int RunHelp()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  DNA-MCP CLI — Agentic OS 状态查询工具");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  用法: dna-mcp cli <子命令> [参数...]");
        Console.WriteLine();

        WriteSection("子命令");
        var commands = new[]
        {
            ("topology [root]",              "扫描并显示程序集 DAG 拓扑图"),
            ("stack    [root]",              "查看当前调用栈（挂起任务状态）"),
            ("context  <assembly> [root]",   "查看指定程序集的 DNA 上下文摘要"),
            ("plan     <a1,a2,...> [root]",  "生成跨程序集执行计划（拓扑排序）"),
            ("validate <caller> <callee> [root]", "校验程序集依赖关系"),
            ("dna      <assemblyPath>",      "读取程序集完整 .dna/ 内容"),
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
        Console.WriteLine("  DNA_MCP_PROJECT_ROOT    默认项目根目录（省略 [root] 参数时使用）");
        Console.WriteLine();

        WriteSection("示例");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  dna-mcp cli topology");
        Console.WriteLine("  dna-mcp cli stack");
        Console.WriteLine("  dna-mcp cli context Core");
        Console.WriteLine("  dna-mcp cli plan Core,Services/Audio,Services/Scene");
        Console.WriteLine("  dna-mcp cli validate Services/Audio Core");
        Console.WriteLine("  dna-mcp cli dna C:\\MyProject\\src\\Core");
        Console.ResetColor();
        Console.WriteLine();

        return 0;
    }

    private int RunUnknown(string subCommand)
    {
        WriteError($"未知子命令: '{subCommand}'");
        Console.WriteLine("运行 'dna-mcp cli help' 查看可用命令。");
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
