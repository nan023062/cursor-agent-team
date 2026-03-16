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
/// 职责：文件读写、C# Public API 精确提取（Roslyn）、lessons/history 结构化写入
/// </summary>
public class WorkspaceService(ILogger<WorkspaceService> logger)
{
    // 匹配 ## Contract 段落（用于替换，兼容旧 ## Public API）
    private static readonly Regex ContractSectionRegex =
        new(@"(##\s+(?:Contract|Public API)\s*\n)([\s\S]*?)(?=\n##\s|\z)", RegexOptions.Multiline);

    private static readonly Regex LastPitfallEntryRegex =
        new(@"(---\s*$)", RegexOptions.Multiline | RegexOptions.RightToLeft);

    /// <summary>
    /// 使用 Roslyn 从 C# 源文件中精确提取所有 public 成员签名。
    /// 注意：此功能专用于 C# 代码模块，非代码模块应由 AI 手动维护职责声明段。
    /// </summary>
    public async Task<string> ExtractPublicApiAsync(string modulePath)
    {
        if (!Directory.Exists(modulePath))
            return $"错误：目录 '{modulePath}' 不存在。";

        var csFiles = Directory.EnumerateFiles(modulePath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        if (csFiles.Count == 0)
            return "未找到任何 .cs 源文件。此工具仅适用于 C# 代码模块。";

        logger.LogInformation("Roslyn 扫描 {Count} 个 C# 文件: {Path}", csFiles.Count, modulePath);

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

        var sb = new StringBuilder();
        sb.AppendLine("## Contract");
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
    /// 将提取的 Contract 写入 identity.md 的 ## Contract 段
    /// </summary>
    public async Task<string> WriteContractToIdentityAsync(string modulePath, string contractContent)
    {
        var identityPath = Path.Combine(modulePath, ".dna", "identity.md");
        if (!File.Exists(identityPath))
            return $"错误：{identityPath} 不存在。请先初始化该模块。";

        var content = await File.ReadAllTextAsync(identityPath);

        if (ContractSectionRegex.IsMatch(content))
        {
            content = ContractSectionRegex.Replace(content, m => m.Groups[1].Value + contractContent.TrimStart() + "\n");
        }
        else
        {
            content = content.TrimEnd() + "\n\n" + contractContent;
        }

        await File.WriteAllTextAsync(identityPath, content, new UTF8Encoding(true));
        logger.LogInformation("Contract 已写入: {Path}", identityPath);
        return $"Contract 已成功写入 {identityPath}";
    }

    /// <summary>
    /// 结构化写入教训条目到 lessons.md，并同步更新 pitfall-index.md
    /// </summary>
    public async Task<string> WritePitfallAsync(
        string modulePath,
        string projectRoot,
        string tag,
        string rootCause,
        string fixMethod,
        string scope,
        string? summary = null)
    {
        var lessonsPath = Path.Combine(modulePath, ".dna", "lessons.md");
        if (!File.Exists(lessonsPath))
            return $"错误：{lessonsPath} 不存在。请先初始化该模块。";

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var moduleName = Path.GetFileName(modulePath);
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

        var content = await File.ReadAllTextAsync(lessonsPath);
        content = content.TrimEnd() + "\n" + entry;
        await File.WriteAllTextAsync(lessonsPath, content, new UTF8Encoding(true));

        var indexPath = Path.Combine(projectRoot, "coder", "pitfall-index.md");
        if (File.Exists(indexPath))
        {
            var indexLine = $"{date}|`{tag}`|{moduleName}|{shortSummary}\n";
            await File.AppendAllTextAsync(indexPath, indexLine);
            logger.LogDebug("pitfall-index.md 已更新");
        }

        logger.LogInformation("Lesson 已写入: {Module} [{Tag}]", moduleName, tag);
        return $"Lesson 已写入 {lessonsPath}\n标签: {tag}\n摘要: {shortSummary}";
    }

    /// <summary>
    /// 追加变更记录到 history.md
    /// </summary>
    public async Task<string> WriteChangelogAsync(
        string modulePath,
        string changeType,
        string description,
        bool isBreaking = false,
        string? relatedModules = null)
    {
        var historyPath = Path.Combine(modulePath, ".dna", "history.md");
        if (!File.Exists(historyPath))
            return $"错误：{historyPath} 不存在。请先初始化该模块。";

        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var breakingTag = isBreaking ? " [BREAKING]" : "";
        var crossTag = !string.IsNullOrEmpty(relatedModules) ? $" [跨模块: {relatedModules}]" : "";

        var entry = $"\n- **{date}** [{changeType}]{breakingTag}{crossTag}: {description}";

        var content = await File.ReadAllTextAsync(historyPath);
        var insertIndex = content.IndexOf("\n## ", StringComparison.Ordinal);
        if (insertIndex < 0)
            content = content.TrimEnd() + "\n" + entry;
        else
        {
            var afterHeader = content.IndexOf('\n', insertIndex + 1);
            if (afterHeader < 0)
                content += entry;
            else
                content = content[..(afterHeader + 1)] + entry + "\n" + content[(afterHeader + 1)..];
        }

        await File.WriteAllTextAsync(historyPath, content, new UTF8Encoding(true));
        logger.LogInformation("History 已写入: {Module} [{Type}]{Breaking}",
            Path.GetFileName(modulePath), changeType, isBreaking ? " BREAKING" : "");

        return $"变更记录已写入 {historyPath}\n类型: [{changeType}]{breakingTag}{crossTag}";
    }

    /// <summary>
    /// 一次性读取模块的完整 .dna/ 上下文（按上下文加载协议顺序组装）
    /// </summary>
    public async Task<string> ReadDnaAsync(string modulePath)
    {
        if (!Directory.Exists(modulePath))
            return $"错误：目录 '{modulePath}' 不存在。";

        var dnaPath = Path.Combine(modulePath, ".dna");
        if (!Directory.Exists(dnaPath))
            return $"错误：{dnaPath} 不存在。请先初始化该模块。";

        var moduleName = Path.GetFileName(modulePath);
        var sb = new StringBuilder();
        sb.AppendLine($"# DNA 上下文 — {moduleName}");
        sb.AppendLine($"> 加载时间: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        await AppendFileSection(sb, Path.Combine(dnaPath, "identity.md"), "## identity.md");
        await AppendFileSection(sb, Path.Combine(dnaPath, "lessons.md"), "## lessons.md");
        await AppendFileSection(sb, Path.Combine(dnaPath, "links.md"), "## links.md");
        await AppendFileSection(sb, Path.Combine(dnaPath, "active.md"), "## active.md（进行中任务）");

        return sb.ToString();
    }

    // ── 私有辅助方法 ──────────────────────────────────────────────

    private List<(string GroupComment, string Signature)> ExtractSignaturesFromSyntaxTree(
        SyntaxNode root, string fileName)
    {
        var signatures = new List<(string, string)>();

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
