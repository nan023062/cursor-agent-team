namespace AgenticOs.Models;

/// <summary>
/// 模块边界模式，对应 identity.md 中的 boundary 字段
/// </summary>
public enum BoundaryMode
{
    /// <summary>默认硬边界：只能访问 Contract 段，禁止读取内部内容</summary>
    Hard,
    /// <summary>软边界：同层模块间受控共享，可读取 Contract 相关文件</summary>
    Soft,
    /// <summary>共享层：任何模块均可引用，可读取 Contract 相关文件</summary>
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

    /// <summary>边界模式，从 identity.md 的 boundary 字段读取</summary>
    public BoundaryMode Boundary { get; set; } = BoundaryMode.Hard;

    /// <summary>该模块依赖的其他模块名称列表（从 links.md 解析）</summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>维护者（从 identity.md 的 维护者 字段读取，可为空）</summary>
    public string? Maintainer { get; set; }

    /// <summary>.dna/ 目录是否存在</summary>
    public bool HasDna => Directory.Exists(System.IO.Path.Combine(Path, ".dna"));

    /// <summary>identity.md 路径</summary>
    public string IdentityPath => System.IO.Path.Combine(Path, ".dna", "identity.md");

    /// <summary>lessons.md 路径</summary>
    public string LessonsPath => System.IO.Path.Combine(Path, ".dna", "lessons.md");

    /// <summary>links.md 路径</summary>
    public string LinksPath => System.IO.Path.Combine(Path, ".dna", "links.md");

    /// <summary>history.md 路径</summary>
    public string HistoryPath => System.IO.Path.Combine(Path, ".dna", "history.md");

    /// <summary>active.md 路径</summary>
    public string ActivePath => System.IO.Path.Combine(Path, ".dna", "active.md");
}
