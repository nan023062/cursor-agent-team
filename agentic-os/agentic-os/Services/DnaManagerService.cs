using System.Text.RegularExpressions;
using AgenticOs.Models;
using Microsoft.Extensions.Logging;

namespace AgenticOs.Services;

/// <summary>
/// DNA 记忆与拓扑管理器
/// 职责：扫描项目程序集、维护 DAG 拓扑图、执行视界分级物理过滤、拦截越界访问
/// </summary>
public class DnaManagerService(ILogger<DnaManagerService> logger)
{
    // 从 architecture.md 解析 boundary 字段的正则
    private static readonly Regex BoundaryRegex =
        new(@"\*\*边界模式\*\*[：:]\s*`boundary:\s*(hard|soft|shared)`", RegexOptions.IgnoreCase);

    // 从 architecture.md 解析维护者字段
    private static readonly Regex MaintainerRegex =
        new(@"\*\*维护者\*\*[：:]\s*(.+)", RegexOptions.IgnoreCase);

    // 从 dependencies.md 解析依赖的程序集名称（识别 `程序集名/` 或 `程序集名` 格式）
    private static readonly Regex DependencyNameRegex =
        new(@"^\s*[-*]\s+\*?\*?`?([A-Za-z0-9._\-]+)`?\*?\*?", RegexOptions.Multiline);

    // 匹配 ## Public API 段落
    private static readonly Regex PublicApiSectionRegex =
        new(@"(##\s+Public API[\s\S]*?)(?=^##\s|\z)", RegexOptions.Multiline);

    /// <summary>
    /// 扫描项目根目录，发现所有含 .dna/ 目录的程序集，构建拓扑图
    /// </summary>
    public TopologyResult ScanTopology(string projectRoot)
    {
        var result = new TopologyResult { ProjectRoot = projectRoot };

        if (!Directory.Exists(projectRoot))
        {
            logger.LogWarning("项目根目录不存在: {Root}", projectRoot);
            return result;
        }

        // 递归查找所有含 .dna/ 的目录（排除 .git、node_modules、obj、bin 等）
        var assemblies = FindAssemblyDirectories(projectRoot);

        foreach (var assemblyPath in assemblies)
        {
            var node = ParseAssemblyNode(assemblyPath);
            result.Assemblies.Add(node);
            logger.LogDebug("发现程序集: {Name} ({Boundary})", node.Name, node.Boundary);
        }

        // 构建依赖边
        foreach (var node in result.Assemblies)
        {
            foreach (var dep in node.Dependencies)
            {
                if (result.Assemblies.Any(a => a.Name.Equals(dep, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Edges.Add(new DependencyEdge { From = node.Name, To = dep });
                }
            }
        }

        logger.LogInformation("拓扑扫描完成：{Count} 个程序集，{EdgeCount} 条依赖边",
            result.Assemblies.Count, result.Edges.Count);
        return result;
    }

    /// <summary>
    /// 对给定程序集列表执行拓扑排序（Kahn 算法），返回执行计划
    /// 被依赖方优先（底层先，上层后）
    /// </summary>
    public ExecutionPlan GetExecutionPlan(List<string> assemblyNames, string projectRoot)
    {
        var topology = ScanTopology(projectRoot);
        var plan = new ExecutionPlan();

        // 只考虑输入的程序集子集
        var targetSet = assemblyNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nodes = topology.Assemblies
            .Where(a => targetSet.Contains(a.Name))
            .ToList();

        if (nodes.Count == 0)
        {
            plan.OrderedAssemblies = assemblyNames;
            return plan;
        }

        // 构建子图的入度表和邻接表
        // 注意：依赖边 A→B 表示 A 依赖 B，执行顺序 B 先于 A
        // 所以在排序图中，B 指向 A（B 完成后 A 才能执行）
        var inDegree = nodes.ToDictionary(n => n.Name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var graph = nodes.ToDictionary(n => n.Name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!targetSet.Contains(dep)) continue;
                // dep 是被依赖方，node 是依赖方；dep 先执行
                graph[dep].Add(node.Name);
                inDegree[node.Name]++;
            }
        }

        // Kahn 算法
        var queue = new Queue<string>(inDegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var ordered = new List<string>();

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ordered.Add(current);

            foreach (var next in graph[current])
            {
                inDegree[next]--;
                if (inDegree[next] == 0)
                    queue.Enqueue(next);
            }
        }

        if (ordered.Count != nodes.Count)
        {
            // 存在循环依赖
            var remaining = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
            plan.HasCycle = true;
            plan.CycleDescription = $"检测到循环依赖，涉及程序集：{string.Join(", ", remaining)}。" +
                                    "请检查这些程序集的 dependencies.md，消除循环引用（可引入事件解耦或提取公共接口到更底层程序集）。";
            plan.OrderedAssemblies = ordered; // 返回已排序的部分
        }
        else
        {
            plan.OrderedAssemblies = ordered;
        }

        logger.LogInformation("执行计划生成：{Order}", string.Join(" → ", plan.OrderedAssemblies));
        return plan;
    }

    /// <summary>
    /// 按视界分级物理过滤，返回 AI 被允许访问的上下文内容
    /// </summary>
    /// <param name="targetAssembly">目标程序集名称</param>
    /// <param name="currentAssembly">当前正在开发的程序集名称（null 表示查询模式）</param>
    /// <param name="projectRoot">项目根目录</param>
    public DnaContext GetAssemblyContext(string targetAssembly, string? currentAssembly, string projectRoot)
    {
        var topology = ScanTopology(projectRoot);
        var target = topology.Assemblies
            .FirstOrDefault(a => a.Name.Equals(targetAssembly, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            return new DnaContext
            {
                AssemblyName = targetAssembly,
                Level = ContextLevel.Unlinked,
                BlockMessage = $"[拦截] 程序集 '{targetAssembly}' 未在拓扑图中注册。请先执行 register_assembly 或 @coder init 初始化该程序集。"
            };
        }

        // 确定访问级别
        var level = DetermineContextLevel(targetAssembly, currentAssembly, topology);

        var context = new DnaContext
        {
            AssemblyName = targetAssembly,
            Level = level,
            Boundary = target.Boundary
        };

        switch (level)
        {
            case ContextLevel.Unlinked:
                context.BlockMessage = $"[拦截] 物理隔离。程序集 '{targetAssembly}' 不在当前程序集 '{currentAssembly}' 的依赖链上，无权访问任何文件。";
                break;

            case ContextLevel.HardDependency:
                // 只返回 ## Public API 段
                context.ArchitectureContent = ExtractPublicApiSection(target.ArchitecturePath);
                if (context.ArchitectureContent == null)
                {
                    context.BlockMessage = $"[警告] 程序集 '{targetAssembly}' 的 architecture.md 不存在或缺少 ## Public API 段。请先执行 @coder init 初始化。";
                }
                break;

            case ContextLevel.SharedOrSoft:
                context.ArchitectureContent = ReadFileOrNull(target.ArchitecturePath);
                context.PitfallsContent = ReadFileOrNull(target.PitfallsPath);
                context.DependenciesContent = ReadFileOrNull(target.DependenciesPath);
                context.SourceFilePaths = GetPublicApiSourceFiles(target.Path);
                break;

            case ContextLevel.Current:
                context.ArchitectureContent = ReadFileOrNull(target.ArchitecturePath);
                context.PitfallsContent = ReadFileOrNull(target.PitfallsPath);
                context.DependenciesContent = ReadFileOrNull(target.DependenciesPath);
                context.WipContent = ReadFileOrNull(target.WipPath);
                context.SourceFilePaths = GetAllSourceFiles(target.Path);
                break;
        }

        return context;
    }

    /// <summary>
    /// 校验程序集 A 调用程序集 B 的 API 是否在白名单内
    /// </summary>
    public DependencyValidationResult ValidateDependency(
        string callerAssembly, string calleeAssembly, string? apiName, string projectRoot)
    {
        var topology = ScanTopology(projectRoot);
        var caller = topology.Assemblies
            .FirstOrDefault(a => a.Name.Equals(callerAssembly, StringComparison.OrdinalIgnoreCase));

        if (caller == null)
            return new DependencyValidationResult
            {
                IsValid = false,
                Message = $"调用方程序集 '{callerAssembly}' 未注册。"
            };

        // 检查依赖声明
        bool isDeclared = caller.Dependencies.Any(d => d.Equals(calleeAssembly, StringComparison.OrdinalIgnoreCase));

        if (!isDeclared)
        {
            var blockMsg = $"[阻断] 依赖模块 {calleeAssembly} 未在 {callerAssembly}/dependencies.md 中声明。\n" +
                           $"请先在 {callerAssembly}/.dna/dependencies.md 中添加 {calleeAssembly} 的依赖声明，再来续接本任务。";
            return new DependencyValidationResult
            {
                IsValid = false,
                Message = $"程序集 '{callerAssembly}' 的 dependencies.md 中未声明对 '{calleeAssembly}' 的依赖。",
                BlockMessage = blockMsg
            };
        }

        // 如果指定了 API 名称，检查是否在 Public API 中
        if (!string.IsNullOrEmpty(apiName))
        {
            var callee = topology.Assemblies
                .FirstOrDefault(a => a.Name.Equals(calleeAssembly, StringComparison.OrdinalIgnoreCase));

            if (callee != null)
            {
                var publicApiContent = ExtractPublicApiSection(callee.ArchitecturePath);
                if (publicApiContent != null && !publicApiContent.Contains(apiName))
                {
                    var blockMsg = $"[阻断] 依赖模块 {calleeAssembly} 缺少 {apiName} 接口。\n" +
                                   $"请先通过 @coder dev {calleeAssembly} 补充该接口，再来续接本任务。";
                    return new DependencyValidationResult
                    {
                        IsValid = false,
                        Message = $"程序集 '{calleeAssembly}' 的 Public API 中未找到 '{apiName}'。",
                        BlockMessage = blockMsg
                    };
                }
            }
        }

        return new DependencyValidationResult
        {
            IsValid = true,
            Message = $"依赖校验通过：'{callerAssembly}' → '{calleeAssembly}'" +
                      (apiName != null ? $" API '{apiName}'" : "")
        };
    }

    /// <summary>
    /// 将新程序集注册到拓扑图（创建 .dna/ 目录结构）
    /// </summary>
    public string RegisterAssembly(string assemblyPath, string projectRoot)
    {
        var fullPath = Path.IsPathRooted(assemblyPath)
            ? assemblyPath
            : Path.Combine(projectRoot, assemblyPath);

        if (!Directory.Exists(fullPath))
            return $"错误：目录 '{fullPath}' 不存在。请先创建目录。";

        var dnaPath = Path.Combine(fullPath, ".dna");
        if (Directory.Exists(dnaPath))
            return $"程序集 '{Path.GetFileName(fullPath)}' 已有 .dna/ 目录，无需重复注册。";

        Directory.CreateDirectory(dnaPath);
        logger.LogInformation("已注册程序集: {Path}", fullPath);
        return $"已注册程序集 '{Path.GetFileName(fullPath)}'，.dna/ 目录已创建于 {dnaPath}。\n" +
               "请使用 @coder init 初始化完整的 DNA 记忆文件（architecture.md、pitfalls.md 等）。";
    }

    // ── 私有辅助方法 ──────────────────────────────────────────────

    private List<string> FindAssemblyDirectories(string root)
    {
        var result = new List<string>();
        var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".idea", "node_modules", "obj", "bin",
            "__pycache__", ".dna", "agentic-os"
        };

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            {
                var dirName = Path.GetFileName(dir);
                if (excludedDirs.Contains(dirName)) continue;

                // 跳过路径中包含排除目录的
                if (excludedDirs.Any(ex => dir.Contains(Path.DirectorySeparatorChar + ex + Path.DirectorySeparatorChar)))
                    continue;

                var dnaDir = Path.Combine(dir, ".dna");
                if (Directory.Exists(dnaDir))
                    result.Add(dir);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning("无权访问目录: {Message}", ex.Message);
        }

        return result;
    }

    private AssemblyNode ParseAssemblyNode(string assemblyPath)
    {
        var node = new AssemblyNode
        {
            Name = Path.GetFileName(assemblyPath),
            Path = assemblyPath
        };

        // 解析 architecture.md
        var archPath = Path.Combine(assemblyPath, ".dna", "architecture.md");
        if (File.Exists(archPath))
        {
            var content = File.ReadAllText(archPath);

            var boundaryMatch = BoundaryRegex.Match(content);
            if (boundaryMatch.Success)
            {
                node.Boundary = boundaryMatch.Groups[1].Value.ToLower() switch
                {
                    "soft" => BoundaryMode.Soft,
                    "shared" => BoundaryMode.Shared,
                    _ => BoundaryMode.Hard
                };
            }

            var maintainerMatch = MaintainerRegex.Match(content);
            if (maintainerMatch.Success)
                node.Maintainer = maintainerMatch.Groups[1].Value.Trim();
        }

        // 解析 dependencies.md
        var depsPath = Path.Combine(assemblyPath, ".dna", "dependencies.md");
        if (File.Exists(depsPath))
        {
            node.Dependencies = ParseDependencyNames(File.ReadAllText(depsPath));
        }

        return node;
    }

    private List<string> ParseDependencyNames(string dependenciesContent)
    {
        var names = new List<string>();
        // 查找 "## 依赖声明" 或 "## Dependencies" 段落后的列表项
        var matches = DependencyNameRegex.Matches(dependenciesContent);
        foreach (Match m in matches)
        {
            var name = m.Groups[1].Value.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith('#'))
                names.Add(name);
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private ContextLevel DetermineContextLevel(string targetAssembly, string? currentAssembly, TopologyResult topology)
    {
        if (currentAssembly == null)
            return ContextLevel.Current; // 无当前程序集时，视为查询模式，返回完整上下文

        if (targetAssembly.Equals(currentAssembly, StringComparison.OrdinalIgnoreCase))
            return ContextLevel.Current;

        var target = topology.Assemblies
            .FirstOrDefault(a => a.Name.Equals(targetAssembly, StringComparison.OrdinalIgnoreCase));

        if (target == null) return ContextLevel.Unlinked;

        // shared 边界：任何程序集均可访问
        if (target.Boundary == BoundaryMode.Shared)
            return ContextLevel.SharedOrSoft;

        var current = topology.Assemblies
            .FirstOrDefault(a => a.Name.Equals(currentAssembly, StringComparison.OrdinalIgnoreCase));

        if (current == null) return ContextLevel.Unlinked;

        bool isDeclaredDependency = current.Dependencies
            .Any(d => d.Equals(targetAssembly, StringComparison.OrdinalIgnoreCase));

        if (!isDeclaredDependency)
            return ContextLevel.Unlinked;

        // soft 边界依赖：SharedOrSoft 级别
        if (target.Boundary == BoundaryMode.Soft)
            return ContextLevel.SharedOrSoft;

        // hard 边界依赖：HardDependency 级别
        return ContextLevel.HardDependency;
    }

    private string? ExtractPublicApiSection(string architecturePath)
    {
        if (!File.Exists(architecturePath)) return null;
        var content = File.ReadAllText(architecturePath);
        var match = PublicApiSectionRegex.Match(content);
        return match.Success ? match.Value.Trim() : null;
    }

    private List<string> GetAllSourceFiles(string assemblyPath)
    {
        if (!Directory.Exists(assemblyPath)) return [];
        return Directory.EnumerateFiles(assemblyPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();
    }

    private List<string> GetPublicApiSourceFiles(string assemblyPath)
    {
        // 对于 soft/shared 边界，返回包含 public 声明的源文件路径
        // 简化实现：返回所有非 internal/private 的文件（实际由 Roslyn 精确过滤）
        return GetAllSourceFiles(assemblyPath);
    }

    private static string? ReadFileOrNull(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
