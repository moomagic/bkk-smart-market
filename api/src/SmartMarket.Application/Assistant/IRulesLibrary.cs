namespace SmartMarket.Application.Assistant;

/// <summary>The published rules (<c>docs/rules/*.md</c>), the only source the Vendor Assistant may answer from.</summary>
public interface IRulesLibrary
{
    IReadOnlyList<RuleSection> Sections { get; }

    /// <summary>The whole file as published, e.g. for the AI model's context.</summary>
    string Raw(string file);
}
