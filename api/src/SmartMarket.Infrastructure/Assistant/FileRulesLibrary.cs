using Microsoft.Extensions.Options;
using SmartMarket.Application.Assistant;

namespace SmartMarket.Infrastructure.Assistant;

public sealed class AssistantOptions
{
    public const string Section = "Assistant";

    /// <summary>
    /// Folder holding the rules. When empty, the nearest <c>docs/rules</c> above the app is used: next to the binary in
    /// the Docker image, the repository's own folder when running from source.
    /// </summary>
    public string? RulesPath { get; init; }
}

/// <summary>Reads <c>docs/rules/*.md</c> once; the files are the single source the assistant may answer from.</summary>
public sealed class FileRulesLibrary : IRulesLibrary
{
    private readonly Lazy<(IReadOnlyList<RuleSection> Sections, IReadOnlyDictionary<string, string> Files)> _rules;

    public FileRulesLibrary(IOptions<AssistantOptions> options) =>
        _rules = new(() => Load(options.Value.RulesPath is { Length: > 0 } path ? path : FindRulesFolder()));

    public IReadOnlyList<RuleSection> Sections => _rules.Value.Sections;

    public string Raw(string file) => _rules.Value.Files[file];

    private static (IReadOnlyList<RuleSection>, IReadOnlyDictionary<string, string>) Load(string folder)
    {
        var files = Directory.GetFiles(folder, "*.md")
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToDictionary(f => Path.GetFileName(f), File.ReadAllText);
        if (files.Count == 0)
            throw new InvalidOperationException($"No rules found in '{folder}'. The assistant answers from docs/rules only.");
        return ([.. files.SelectMany(f => RulesMarkdown.Parse(f.Key, f.Value))], files);
    }

    private static string FindRulesFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "rules");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new InvalidOperationException("docs/rules not found; set Assistant:RulesPath.");
    }
}
