namespace AgenticOs.Models;

/// <summary>
/// 模块边界模式，对应 architecture.md 中的 boundary 字段
/// </summary>
public enum BoundaryMode
{
    /// <summary>默认硬边界：只能访问职责声明段，禁止读取内部内容</summary>
    Hard,
    /// <summary>软边界：同层模块间受控共享，可读取职责声明相关文件</summary>
    Soft,
    /// <summary>共享层：任何模块均可引用，可读取职责声明相关文件</summary>
    Shared
}

/// <summary>
/// 模块节点，代表 DNA 拓扑图中的一个工作区单元。
/// 模块可以是代码、美术资产、策划文档、音视频等任何类型的工作内容。
/// </summary>
public class ModuleNode
{
    /// <summary>模块名称（目录名）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>模块根目录的绝对路径</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>边界模式，从 architecture.md 的 boundary 字段读取</summary>
    public BoundaryMode Boundary { get; set; } = BoundaryMode.Hard;

    /// <summary>该模块依赖的其他模块名称列表（从 dependencies.md 解析）</summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>维护者（从 architecture.md 的 维护者 字段读取，可为空）</summary>
    public string? Maintainer { get; set; }

    /// <summary>.dna/ 目录是否存在</summary>
    public bool HasDna => Directory.Exists(System.IO.Path.Combine(Path, ".dna"));

    /// <summary>architecture.md 路径</summary>
    public string ArchitecturePath => System.IO.Path.Combine(Path, ".dna", "architecture.md");

    /// <summary>pitfalls.md 路径</summary>
    public string PitfallsPath => System.IO.Path.Combine(Path, ".dna", "pitfalls.md");

    /// <summary>dependencies.md 路径</summary>
    public string DependenciesPath => System.IO.Path.Combine(Path, ".dna", "dependencies.md");

    /// <summary>changelog.md 路径</summary>
    public string ChangelogPath => System.IO.Path.Combine(Path, ".dna", "changelog.md");

    /// <summary>wip.md 路径</summary>
    public string WipPath => System.IO.Path.Combine(Path, ".dna", "wip.md");
}
