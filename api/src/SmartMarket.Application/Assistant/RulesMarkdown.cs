using System.Text.RegularExpressions;

namespace SmartMarket.Application.Assistant;

/// <summary>Splits a rules file into its <c>##</c> sections; each must carry a <c>&lt;!-- keywords: ... --&gt;</c> line.</summary>
public static partial class RulesMarkdown
{
    public static IReadOnlyList<RuleSection> Parse(string file, string markdown)
    {
        var title = file;
        var sections = new List<RuleSection>();
        string? heading = null;
        List<string> keywords = [];
        List<string> lines = [];

        void Flush()
        {
            if (heading is not null) sections.Add(new RuleSection(file, title, heading, keywords, lines));
        }

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                heading = line[3..].Trim();
                keywords = [];
                lines = [];
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
            }
            else if (heading is not null && KeywordLine().Match(line) is { Success: true } m)
            {
                keywords = [.. m.Groups[1].Value.Split(',').Select(k => k.Trim()).Where(k => k.Length > 0)];
            }
            else if (heading is not null && line.Length > 0)
            {
                lines.Add(line);
            }
        }
        Flush();
        return sections;
    }

    [GeneratedRegex(@"^<!--\s*keywords:(.*?)-->$")]
    private static partial Regex KeywordLine();
}
