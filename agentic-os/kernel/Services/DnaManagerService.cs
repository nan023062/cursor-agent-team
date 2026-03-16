using System.Text.RegularExpressions;
using AgenticOs.Models;
using Microsoft.Extensions.Logging;

namespace AgenticOs.Services;

/// <summary>
/// DNA 记忆与拓扑管理器
/// 职责：扫描项目模块、维护 DAG 拓扑图、执行视界分级物理过滤、拦截越界访问
/// </summary>
public class DnaManagerService(ILogger<DnaManagerService> logger)
{
    private static readonly Regex BoundaryRegex =
        new(@"\*\*边界模式\*\*[：:]\s*`boundary:\s*(hard|soft|shared)`", RegexOptions.IgnoreCase);

    private static readonly Regex MaintainerRegex =
        new(@"\*\*维护者\*\*[：:]\s*(.+)", RegexOptions.IgnoreCase);

    // 从 links.md 解析依赖的模块名称
    private static readonly Regex DependencyNameRegex =
        new(@"^\s*[-*]\s+\*?\*?`?([A-Za-z0-9._\-]+)`?\*?\*?", RegexOptions.Multiline);

    // 匹配 Contract 段（支持多种命名：Contract / Public API / 职责声明 / 交付契约 / 资产规范）
    private static readonly Regex ContractSectionRegex =
        new(@"(##\s+(?:Contract|Public API|职责声明|交付契约|资产规范)[\s\S]*?)(?=^##\s|\z)", RegexOptions.Multiline);

    /// <summary>
    /// 扫描项目根目录，发现所有含 .dna/ 目录的模块，构建拓扑图
    /// </summary>
    public TopologyResult ScanTopology(string projectRoot)
    {
        var result = new TopologyResult { ProjectRoot = projectRoot };

        if (!Directory.Exists(projectRoot))
        {
            logger.LogWarning("项目根目录不存在: {Root}", projectRoot);
            return result;
        }

        var modules = FindModuleDirectories(projectRoot);

        foreach (var modulePath in modules)
        {
            var node = ParseModuleNode(modulePath);
            result.Modules.Add(node);
            logger.LogDebug("发现模块: {Name} ({Boundary})", node.Name, node.Boundary);
        }

        foreach (var node in result.Modules)
        {
            foreach (var dep in node.Dependencies)
            {
                if (result.Modules.Any(a => a.Name.Equals(dep, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Edges.Add(new DependencyEdge { From = node.Name, To = dep });
                }
            }
        }

        logger.LogInformation("拓扑扫描完成：{Count} 个模块，{EdgeCount} 条依赖边",
            result.Modules.Count, result.Edges.Count);
        return result;
    }

    /// <summary>
    /// 对给定模块列表执行拓扑排序（Kahn 算法），返回执行计划
    /// 被依赖方优先（底层先，上层后）
    /// </summary>
    public ExecutionPlan GetExecutionPlan(List<string> moduleNames, string projectRoot)
    {
        var topology = ScanTopology(projectRoot);
        var plan = new ExecutionPlan();

        var targetSet = moduleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nodes = topology.Modules
            .Where(a => targetSet.Contains(a.Name))
            .ToList();

        if (nodes.Count == 0)
        {
            plan.OrderedModules = moduleNames;
            return plan;
        }

        // 依赖边 A→B 表示 A 依赖 B，执行顺序 B 先于 A
        var inDegree = nodes.ToDictionary(n => n.Name, _ => 0, StringComparer.OrdinalIgnoreCase);
        var graph = nodes.ToDictionary(n => n.Name, _ => new List<string>(), StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            foreach (var dep in node.Dependencies)
            {
                if (!targetSet.Contains(dep)) continue;
                graph[dep].Add(node.Name);
                inDegree[node.Name]++;
            }
        }

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
            var remaining = inDegree.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
            plan.HasCycle = true;
            plan.CycleDescription = $"检测到循环依赖，涉及模块：{string.Join(", ", remaining)}。" +
                                    "请检查这些模块的 links.md，消除循环引用（可提取公共内容到更底层模块）。";
            plan.OrderedModules = ordered;
        }
        else
        {
            plan.OrderedModules = ordered;
        }

        logger.LogInformation("执行计划生成：{Order}", string.Join(" → ", plan.OrderedModules));
        return plan;
    }

    /// <summary>
    /// 按视界分级物理过滤，返回 AI 被允许访问的上下文内容
    /// </summary>
    /// <param name="targetModule">目标模块名称</param>
    /// <param name="currentModule">当前正在操作的模块名称（null 表示查询模式）</param>
    /// <param name="projectRoot">项目根目录</param>
    public DnaContext GetModuleContext(string targetModule, string? currentModule, string projectRoot)
    {
        var topology = ScanTopology(projectRoot);
        var target = topology.Modules
            .FirstOrDefault(a => a.Name.Equals(targetModule, StringComparison.OrdinalIgnoreCase));

        if (target == null)
        {
            return new DnaContext
            {
                ModuleName = targetModule,
                Level = ContextLevel.Unlinked,
                BlockMessage = $"[拦截] 模块 '{targetModule}' 未在拓扑图中注册。请先执行 register_module 初始化该模块。"
            };
        }

        var level = DetermineContextLevel(targetModule, currentModule, topology);

        var context = new DnaContext
        {
            ModuleName = targetModule,
            Level = level,
            Boundary = target.Boundary
        };

        switch (level)
        {
            case ContextLevel.Unlinked:
                context.BlockMessage = $"[拦截] 物理隔离。模块 '{targetModule}' 不在当前模块 '{currentModule}' 的依赖链上，无权访问任何文件。";
                break;

            case ContextLevel.HardDependency:
                context.IdentityContent = ExtractContractSection(target.IdentityPath);
                if (context.IdentityContent == null)
                {
                    context.BlockMessage = $"[警告] 模块 '{targetModule}' 的 identity.md 不存在或缺少 ## Contract 段。请先初始化该模块。";
                }
                break;

            case ContextLevel.SharedOrSoft:
                context.IdentityContent = ReadFileOrNull(target.IdentityPath);
                context.LessonsContent = ReadFileOrNull(target.LessonsPath);
                context.LinksContent = ReadFileOrNull(target.LinksPath);
                context.ContentFilePaths = GetContentFiles(target.Path);
                break;

            case ContextLevel.Current:
                context.IdentityContent = ReadFileOrNull(target.IdentityPath);
                context.LessonsContent = ReadFileOrNull(target.LessonsPath);
                context.LinksContent = ReadFileOrNull(target.LinksPath);
                context.ActiveContent = ReadFileOrNull(target.ActivePath);
                context.ContentFilePaths = GetAllContentFiles(target.Path);
                break;
        }

        return context;
    }

    /// <summary>
    /// 校验模块 A 调用模块 B 的依赖是否在白名单内
    /// </summary>
    public DependencyValidationResult ValidateDependency(
        string callerModule, string calleeModule, string? apiName, string projectRoot)
    {
        var topology = ScanTopology(projectRoot);
        var caller = topology.Modules
            .FirstOrDefault(a => a.Name.Equals(callerModule, StringComparison.OrdinalIgnoreCase));

        if (caller == null)
            return new DependencyValidationResult
            {
                IsValid = false,
                Message = $"调用方模块 '{callerModule}' 未注册。"
            };

        bool isDeclared = caller.Dependencies.Any(d => d.Equals(calleeModule, StringComparison.OrdinalIgnoreCase));

        if (!isDeclared)
        {
            var blockMsg = $"[阻断] 依赖模块 {calleeModule} 未在 {callerModule}/links.md 中声明。\n" +
                           $"请先在 {callerModule}/.dna/links.md 中添加 {calleeModule} 的依赖声明，再来续接本任务。";
            return new DependencyValidationResult
            {
                IsValid = false,
                Message = $"模块 '{callerModule}' 的 links.md 中未声明对 '{calleeModule}' 的依赖。",
                BlockMessage = blockMsg
            };
        }

        if (!string.IsNullOrEmpty(apiName))
        {
            var callee = topology.Modules
                .FirstOrDefault(a => a.Name.Equals(calleeModule, StringComparison.OrdinalIgnoreCase));

            if (callee != null)
            {
                var contractContent = ExtractContractSection(callee.IdentityPath);
                if (contractContent != null && !contractContent.Contains(apiName))
                {
                    var blockMsg = $"[阻断] 依赖模块 {calleeModule} 的 Contract 中缺少 {apiName}。\n" +
                                   $"请先补充该接口/职责到 {calleeModule} 的 identity.md ## Contract 段，再来续接本任务。";
                    return new DependencyValidationResult
                    {
                        IsValid = false,
                        Message = $"模块 '{calleeModule}' 的职责声明中未找到 '{apiName}'。",
                        BlockMessage = blockMsg
                    };
                }
            }
        }

        return new DependencyValidationResult
        {
            IsValid = true,
            Message = $"依赖校验通过：'{callerModule}' → '{calleeModule}'" +
                      (apiName != null ? $" API '{apiName}'" : "")
        };
    }

    /// <summary>
    /// 将新模块注册到拓扑图（创建 .dna/ 目录结构）
    /// </summary>
    public string RegisterModule(string modulePath, string projectRoot)
    {
        var fullPath = Path.IsPathRooted(modulePath)
            ? modulePath
            : Path.Combine(projectRoot, modulePath);

        if (!Directory.Exists(fullPath))
            return $"错误：目录 '{fullPath}' 不存在。请先创建目录。";

        var dnaPath = Path.Combine(fullPath, ".dna");
        if (Directory.Exists(dnaPath))
            return $"模块 '{Path.GetFileName(fullPath)}' 已有 .dna/ 目录，无需重复注册。";

        Directory.CreateDirectory(dnaPath);
        logger.LogInformation("已注册模块: {Path}", fullPath);
        return $"已注册模块 '{Path.GetFileName(fullPath)}'，.dna/ 目录已创建于 {dnaPath}。\n" +
               "请初始化 DNA 文件（identity.md、lessons.md、links.md、history.md、active.md）。";
    }

    // ── 私有辅助方法 ──────────────────────────────────────────────

    private List<string> FindModuleDirectories(string root)
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

    private ModuleNode ParseModuleNode(string modulePath)
    {
        var node = new ModuleNode
        {
            Name = Path.GetFileName(modulePath),
            Path = modulePath
        };

        var archPath = Path.Combine(modulePath, ".dna", "identity.md");
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

        var depsPath = Path.Combine(modulePath, ".dna", "links.md");
        if (File.Exists(depsPath))
        {
            node.Dependencies = ParseDependencyNames(File.ReadAllText(depsPath));
        }

        return node;
    }

    private List<string> ParseDependencyNames(string dependenciesContent)
    {
        var names = new List<string>();
        var matches = DependencyNameRegex.Matches(dependenciesContent);
        foreach (Match m in matches)
        {
            var name = m.Groups[1].Value.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith('#'))
                names.Add(name);
        }
        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private ContextLevel DetermineContextLevel(string targetModule, string? currentModule, TopologyResult topology)
    {
        if (currentModule == null)
            return ContextLevel.Current; // 无当前模块时，视为查询模式，返回完整上下文

        if (targetModule.Equals(currentModule, StringComparison.OrdinalIgnoreCase))
            return ContextLevel.Current;

        var target = topology.Modules
            .FirstOrDefault(a => a.Name.Equals(targetModule, StringComparison.OrdinalIgnoreCase));

        if (target == null) return ContextLevel.Unlinked;

        // shared 边界：任何模块均可访问
        if (target.Boundary == BoundaryMode.Shared)
            return ContextLevel.SharedOrSoft;

        var current = topology.Modules
            .FirstOrDefault(a => a.Name.Equals(currentModule, StringComparison.OrdinalIgnoreCase));

        if (current == null) return ContextLevel.Unlinked;

        bool isDeclaredDependency = current.Dependencies
            .Any(d => d.Equals(targetModule, StringComparison.OrdinalIgnoreCase));

        if (!isDeclaredDependency)
            return ContextLevel.Unlinked;

        if (target.Boundary == BoundaryMode.Soft)
            return ContextLevel.SharedOrSoft;

        return ContextLevel.HardDependency;
    }

    private string? ExtractContractSection(string identityPath)
    {
        if (!File.Exists(identityPath)) return null;
        var content = File.ReadAllText(identityPath);
        var match = ContractSectionRegex.Match(content);
        return match.Success ? match.Value.Trim() : null;
    }

    private static readonly HashSet<string> ExcludedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".pdb", ".cache", ".nupkg", ".suo", ".user",
        ".DS_Store", ".meta" // Unity meta files are kept separate
    };

    private static readonly HashSet<string> ExcludedContentDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "obj", "bin", ".git", ".vs", ".idea", "node_modules", "__pycache__", ".dna"
    };

    private List<string> GetAllContentFiles(string modulePath)
    {
        if (!Directory.Exists(modulePath)) return [];
        return Directory.EnumerateFiles(modulePath, "*", SearchOption.AllDirectories)
            .Where(f => !ExcludedContentDirs.Any(ex =>
                            f.Contains(Path.DirectorySeparatorChar + ex + Path.DirectorySeparatorChar))
                     && !ExcludedFileExtensions.Contains(Path.GetExtension(f)))
            .ToList();
    }

    private List<string> GetContentFiles(string modulePath)
    {
        // SharedOrSoft 边界：返回与职责相关的内容文件
        return GetAllContentFiles(modulePath);
    }

    private static string? ReadFileOrNull(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
