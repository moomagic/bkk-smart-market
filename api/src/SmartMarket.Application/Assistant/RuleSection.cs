namespace SmartMarket.Application.Assistant;

/// <summary>One <c>##</c> section of a file in <c>docs/rules/</c> — the unit the assistant answers from and cites.</summary>
/// <param name="Keywords">From the section's <c>&lt;!-- keywords: a, b --&gt;</c> line; what the fallback matches on.</param>
public sealed record RuleSection(string File, string FileTitle, string Heading, IReadOnlyList<string> Keywords, IReadOnlyList<string> Lines);
