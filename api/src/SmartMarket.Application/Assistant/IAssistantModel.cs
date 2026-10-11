namespace SmartMarket.Application.Assistant;

/// <param name="Found">False when the model says the rules don't cover the question.</param>
public sealed record ModelReply(string Answer, bool Found, IReadOnlyList<Citation> Citations);

/// <summary>An AI model that answers from the published rules (CLAUDE.md › AI components › Vendor Assistant).</summary>
public interface IAssistantModel
{
    /// <summary>False without an API key — the assistant then uses keyword matching only.</summary>
    bool IsConfigured { get; }

    /// <param name="rules">Every rules file, verbatim; the only source the model may answer from.</param>
    /// <returns>Null on any failure (error, timeout, refusal, unreadable reply) — the caller falls back.</returns>
    Task<ModelReply?> Ask(string question, string rules, CancellationToken ct);
}
