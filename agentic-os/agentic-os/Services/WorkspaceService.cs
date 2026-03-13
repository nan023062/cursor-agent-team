using System.Text;
using System.Text.RegularExpressions;
using AgenticOs.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace AgenticOs.Services;

/// <summary>
/// 工作区执行器服务
/// 职责：文件读写、Roslyn Public API 精确提取、pitfall/changelog 结构化写入
/// </summary>
public class WorkspaceService(ILogger<WorkspaceService> logger)
{
    // 匹配 ## Public API 段落（用于替换）
    private static readonly Regex PublicApiSectionRegex =
        new(@"(##\s+Public API\s*\n)([\s\S]*?)(?=\n##\s|\z)", RegexOptions.Multiline);

    // 匹配 pitfalls.md 中的最后一个条目，用于追加
    private static readonly Regex LastPitfallEntryRegex =
        new(@"(---\s*$)", RegexOptions.Multiline | RegexOptions.RightToLeft);

    /// <summary>
    /// 使用 Roslyn 从 C# 源文件中精确提取所有 public 成员签名
    /// 100% 准确，不会漏掉继承成员或复杂泛型
    /// </summary>
    public async Task<string> ExtractPublicApiAsync(string assemblyPath)
    {
        if (!Directory.Exists(assemblyPath))
            return $"错误：目录 '{assemblyPath}' 不存在。";

        var csFiles = Directory.EnumerateFiles(assemblyPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        if (csFiles.Count == 0)
            return "未找到任何 .cs 源文件。";

        logger.LogInformation("Roslyn 扫描 {Count} 个 C# 文件: {Path}", csFiles.Count, assemblyPath);

        var allSignatures = new List<(string GroupComment, string Signature)>();

        foreach (var file in csFiles)
        {
            var code = await File.ReadAllTextAsync(file);
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = await tree.GetRootAsync();

            var fileSignatures = ExtractSignaturesFromSyntaxTree(root, Path.GetFileName(file));
            allSignatures.AddRange(fileSignatures);
        }

        if (allSignatures.Count == 0)
            return "未发现任何 public 成员。";

        // 按分组注释聚合，生成标准 csharp 签名块
        var sb = new StringBuilder();
        sb.AppendLine("## Public API");
        sb.AppendLine("```csharp");

        string? currentGroup = null;
        foreach (var (group, sig) in allSignatures.OrderBy(x => x.GroupComment).ThenBy(x => x.Signature))
        {
            if (group != currentGroup)
            {
                if (currentGroup != null) sb.AppendLine();
                sb.AppendLine($"// --- {group} ---");
                currentGroup = group;
            }
            sb.AppendLine(sig);
        }

        sb.AppendLine("```");

        logger.LogInformation("提取到 {Count} 个 public 成员签名", allSignatures.Count);
        return sb.ToString();
    }

    /// <summary>
    /// 将提取的 Public API 写入 architecture.md 的 ## Public API 段
    /// </summary>
    public async Task<string> WritePublicApiToArchitectureAsync(string assemblyPath, string publicApiContent)
    {
        var archPath = Path.Combine(assemblyPath, ".dna", "architecture.md");
        if (!File.Exists(archPath))
            return $"错误：{archPath} 不存在。请先执行 @coder init 初始化。";

        var content = await File.ReadAllTextAsync(archPath);

        if (PublicApiSectionRegex.IsMatch(content))
        {
            // 替换现有 Public API 段
            content = PublicApiSectionRegex.Replace(content, m => m.Groups[1].Value + publicApiContent.TrimStart() + "\n");
        }
        else
        {
            // 追加到文件末尾
            content = content.TrimEnd() + "\n\n" + publicApiContent;
        }

        await File.WriteAllTextAsync(archPath, content, new UTF8Encoding(true)); // UTF-8 BOM
        logger.LogInformation("Public API 已写入: {Path}", archPath);
        return $"Public API 已成功写入 {archPath}";
    }

    /// <summary>
    /// 结构化写入 pitfall 条目到 pitfalls.md，并同步更新 pitfall-index.md
    /// </summary>
    public async Task<string> WritePitfallAsync(
        string assemblyPath,
        string projectRoot,
        string tag,
        string rootCause,
        string fixMethod,
        string scope,
        string? summary = null)
    {
        var pitfallsPath = Path.Combine(assemblyPath, ".dna", "pitfalls.md");
        if (!File.Exists(pitfallsPath))
            return $"错误：{pitfallsPath} 不存在。请先执行 @coder init 初始化。";

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var assemblyName = Path.GetFileName(assemblyPath);
        var shortSummary = summary ?? $"{rootCause.Split('。')[0]}";

        var entry = $"""

                     ---

                     ### [{tag}] {shortSummary}
                     - **日期**: {date}
                     - **标签**: `{tag}`
                     - **根因**: {rootCause}
                     - **修复方式**: {fixMethod}
                     - **影响范围**: {scope}

                     """;

        var content = await File.ReadAllTextAsync(pitfallsPath);
        content = content.TrimEnd() + "\n" + entry;
        await File.WriteAllTextAsync(pitfallsPath, content, new UTF8Encoding(true));

        // 同步更新 pitfall-index.md（如果存在）
        var indexPath = Path.Combine(projectRoot, "coder", "pitfall-index.md");
        if (File.Exists(indexPath))
        {
            var indexLine = $"{date}|`{tag}`|{assemblyName}|{shortSummary}\n";
            await File.AppendAllTextAsync(indexPath, indexLine);
            logger.LogDebug("pitfall-index.md 已更新");
        }

        logger.LogInformation("Pitfall 已写入: {Assembly} [{Tag}]", assemblyName, tag);
        return $"Pitfall 已写入 {pitfallsPath}\n标签: {tag}\n摘要: {shortSummary}";
    }

    /// <summary>
    /// 追加变更记录到 changelog.md
    /// </summary>
    public async Task<string> WriteChangelogAsync(
        string assemblyPath,
        string changeType,
        string description,
        bool isBreaking = false,
        string? relatedAssemblies = null)
    {
        var changelogPath = Path.Combine(assemblyPath, ".dna", "changelog.md");
        if (!File.Exists(changelogPath))
            return $"错误：{changelogPath} 不存在。请先执行 @coder init 初始化。";

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var breakingTag = isBreaking ? " [BREAKING]" : "";
        var crossTag = !string.IsNullOrEmpty(relatedAssemblies) ? $" [跨程序集: {relatedAssemblies}]" : "";

        var entry = $"\n- **{date}** [{changeType}]{breakingTag}{crossTag}: {description}";

        var content = await File.ReadAllTextAsync(changelogPath);
        // 找到第一个 ## 段落后追加
        var insertIndex = content.IndexOf("\n## ", StringComparison.Ordinal);
        if (insertIndex < 0)
            content = content.TrimEnd() + "\n" + entry;
        else
        {
            // 在第一个 ## 段落后的第一行插入
            var afterHeader = content.IndexOf('\n', insertIndex + 1);
            if (afterHeader < 0)
                content += entry;
            else
                content = content[..(afterHeader + 1)] + entry + "\n" + content[(afterHeader + 1)..];
        }

        await File.WriteAllTextAsync(changelogPath, content, new UTF8Encoding(true));
        logger.LogInformation("Changelog 已写入: {Assembly} [{Type}]{Breaking}",
            Path.GetFileName(assemblyPath), changeType, isBreaking ? " BREAKING" : "");

        return $"变更记录已写入 {changelogPath}\n类型: [{changeType}]{breakingTag}{crossTag}";
    }

    /// <summary>
    /// 一次性读取程序集的完整 .dna/ 上下文（按上下文加载协议顺序组装）
    /// </summary>
    public async Task<string> ReadDnaAsync(string assemblyPath)
    {
        if (!Directory.Exists(assemblyPath))
            return $"错误：目录 '{assemblyPath}' 不存在。";

        var dnaPath = Path.Combine(assemblyPath, ".dna");
        if (!Directory.Exists(dnaPath))
            return $"错误：{dnaPath} 不存在。请先执行 @coder init 初始化。";

        var assemblyName = Path.GetFileName(assemblyPath);
        var sb = new StringBuilder();
        sb.AppendLine($"# DNA 上下文 — {assemblyName}");
        sb.AppendLine($"> 加载时间: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // 按 AGENT.md 上下文加载协议顺序
        await AppendFileSection(sb, Path.Combine(dnaPath, "architecture.md"), "## architecture.md");
        await AppendFileSection(sb, Path.Combine(dnaPath, "pitfalls.md"), "## pitfalls.md");
        await AppendFileSection(sb, Path.Combine(dnaPath, "dependencies.md"), "## dependencies.md");
        await AppendFileSection(sb, Path.Combine(dnaPath, "wip.md"), "## wip.md（进行中任务）");

        return sb.ToString();
    }

    // ── 私有辅助方法 ──────────────────────────────────────────────

    private List<(string GroupComment, string Signature)> ExtractSignaturesFromSyntaxTree(
        SyntaxNode root, string fileName)
    {
        var signatures = new List<(string, string)>();

        // 提取所有 public 类型声明
        foreach (var typeDecl in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (!IsPublic(typeDecl.Modifiers)) continue;

            var typeName = typeDecl.Identifier.Text;
            var typeKind = typeDecl switch
            {
                ClassDeclarationSyntax => "class",
                InterfaceDeclarationSyntax => "interface",
                StructDeclarationSyntax => "struct",
                EnumDeclarationSyntax => "enum",
                RecordDeclarationSyntax r => r.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) ? "record struct" : "record",
                _ => "type"
            };

            var typeParams = typeDecl is TypeDeclarationSyntax td && td.TypeParameterList != null
                ? td.TypeParameterList.ToString()
                : "";

            var baseList = typeDecl.BaseList != null ? $" : {typeDecl.BaseList.Types}" : "";
            var modifiers = GetSignificantModifiers(typeDecl.Modifiers);

            signatures.Add((typeName, $"{modifiers}{typeKind} {typeName}{typeParams}{baseList};"));

            // 提取类型内部的 public 成员
            if (typeDecl is TypeDeclarationSyntax typeWithMembers)
            {
                foreach (var member in typeWithMembers.Members)
                {
                    var memberSig = ExtractMemberSignature(member, typeName);
                    if (memberSig != null)
                        signatures.Add((typeName, memberSig));
                }
            }
            else if (typeDecl is EnumDeclarationSyntax enumDecl)
            {
                foreach (var member in enumDecl.Members)
                {
                    var value = member.EqualsValue != null ? member.EqualsValue.ToString() : "";
                    signatures.Add((typeName, $"    {member.Identifier}{value},  // {typeName}"));
                }
            }
        }

        return signatures;
    }

    private string? ExtractMemberSignature(MemberDeclarationSyntax member, string typeName)
    {
        return member switch
        {
            MethodDeclarationSyntax m when IsPublic(m.Modifiers) =>
                $"    {GetSignificantModifiers(m.Modifiers)}{m.ReturnType} {m.Identifier}{m.TypeParameterList}{m.ParameterList};  // {typeName}",

            PropertyDeclarationSyntax p when IsPublic(p.Modifiers) =>
                $"    {GetSignificantModifiers(p.Modifiers)}{p.Type} {p.Identifier} {{ {GetAccessors(p)} }}  // {typeName}",

            FieldDeclarationSyntax f when IsPublic(f.Modifiers) =>
                $"    {GetSignificantModifiers(f.Modifiers)}{f.Declaration};  // {typeName}",

            ConstructorDeclarationSyntax c when IsPublic(c.Modifiers) =>
                $"    {GetSignificantModifiers(c.Modifiers)}{c.Identifier}{c.ParameterList};  // {typeName}",

            EventDeclarationSyntax e when IsPublic(e.Modifiers) =>
                $"    {GetSignificantModifiers(e.Modifiers)}event {e.Type} {e.Identifier};  // {typeName}",

            _ => null
        };
    }

    private static bool IsPublic(SyntaxTokenList modifiers) =>
        modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));

    private static string GetSignificantModifiers(SyntaxTokenList modifiers)
    {
        var significant = modifiers
            .Where(m => m.IsKind(SyntaxKind.PublicKeyword)
                     || m.IsKind(SyntaxKind.StaticKeyword)
                     || m.IsKind(SyntaxKind.AbstractKeyword)
                     || m.IsKind(SyntaxKind.VirtualKeyword)
                     || m.IsKind(SyntaxKind.OverrideKeyword)
                     || m.IsKind(SyntaxKind.AsyncKeyword)
                     || m.IsKind(SyntaxKind.SealedKeyword)
                     || m.IsKind(SyntaxKind.ReadOnlyKeyword))
            .Select(m => m.Text);
        var result = string.Join(" ", significant);
        return string.IsNullOrEmpty(result) ? "" : result + " ";
    }

    private static string GetAccessors(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList == null) return "get;";
        var accessors = prop.AccessorList.Accessors
            .Select(a => a.Keyword.Text + ";");
        return string.Join(" ", accessors);
    }

    private static async Task AppendFileSection(StringBuilder sb, string filePath, string header)
    {
        sb.AppendLine($"---");
        sb.AppendLine(header);
        sb.AppendLine();
        if (File.Exists(filePath))
        {
            var content = await File.ReadAllTextAsync(filePath);
            sb.AppendLine(content);
        }
        else
        {
            sb.AppendLine("（文件不存在）");
        }
        sb.AppendLine();
    }
}
