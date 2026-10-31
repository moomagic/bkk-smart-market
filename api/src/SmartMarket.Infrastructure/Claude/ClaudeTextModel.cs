using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Assistant;

namespace SmartMarket.Infrastructure.Claude;

/// <summary><see cref="ITextModel"/> through the Claude Messages API: plain text back, or null and the caller's template.</summary>
public sealed class ClaudeTextModel(HttpClient http, IOptions<AssistantOptions> options, ILogger<ClaudeTextModel> log) : ITextModel
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ApiKey);

    public async Task<string?> Write(TextPrompt prompt, CancellationToken ct)
    {
        var text = await ClaudeMessages.Send(http, options.Value,
            ClaudeMessages.Body(options.Value.Model, prompt.Instructions, prompt.Data), log, "using the template", ct);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
