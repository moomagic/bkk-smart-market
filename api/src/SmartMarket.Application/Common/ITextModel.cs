namespace SmartMarket.Application.Common;

/// <param name="Instructions">What to write and the limits it must keep (no targets, no scores, no names…).</param>
/// <param name="Data">The only facts it may use — figures, never personal data; treated as data, not instructions.</param>
public sealed record TextPrompt(string Instructions, string Data);

/// <summary>
/// An AI model that writes a short text from given data (CLAUDE.md › AI components): it recommends and explains, never
/// decides (Guardrail 1). Every caller keeps a template that needs no model, used without a key or on any failure.
/// </summary>
public interface ITextModel
{
    /// <summary>False without an API key — callers then use their template only.</summary>
    bool IsConfigured { get; }

    /// <returns>The text, or null on any failure (error, timeout, refusal, empty reply) — the caller falls back.</returns>
    Task<string?> Write(TextPrompt prompt, CancellationToken ct);
}
