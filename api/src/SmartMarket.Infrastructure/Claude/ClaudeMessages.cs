using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SmartMarket.Infrastructure.Assistant;

namespace SmartMarket.Infrastructure.Claude;

/// <summary>
/// One call to the Claude Messages API (CLAUDE.md › AI components): headers, model from config, the server-side refusal
/// fallback, the timeout, and the reply's final text block. Every failure — HTTP error, timeout, refusal, unreadable
/// reply — returns null so the caller falls back to what it does without a model. Logs never carry the prompt or the
/// reply (Guardrail 6). Shared by every Claude-backed model, each a typed <see cref="HttpClient"/> of its own.
/// </summary>
internal static class ClaudeMessages
{
    private const string ApiVersion = "2023-06-01";

    /// <summary>Models that accept server-side refusal fallbacks routed by category ("fallbacks": "default").</summary>
    private static readonly HashSet<string> DefaultFallbackModels = ["claude-opus-5-5", "claude-opus-5", "claude-fable-5-1", "claude-sonnet-5-5"];

    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    /// <param name="system">Instructions (and any fixed context); sent as one block.</param>
    /// <param name="cacheSystem">Cache the system block: worth it when it is large and the same on every call.</param>
    /// <param name="schema">JSON schema the reply's text must follow; null for plain text.</param>
    public static JsonObject Body(string model, string system, string user, bool cacheSystem = false, JsonObject? schema = null)
    {
        var block = new JsonObject { ["type"] = "text", ["text"] = system };
        if (cacheSystem) block["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        var output = new JsonObject { ["effort"] = "low" }; // short, bounded replies: low effort keeps them inside the timeout
        if (schema is not null) output["format"] = new JsonObject { ["type"] = "json_schema", ["schema"] = schema.DeepClone() };
        var body = new JsonObject
        {
            ["model"] = model,
            ["max_tokens"] = 4096,
            ["output_config"] = output,
            ["system"] = new JsonArray(block),
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = user }),
        };
        if (DefaultFallbackModels.Contains(model))
            body["fallbacks"] = "default"; // a safety decline is retried on a suitable model inside the same call
        return body;
    }

    /// <param name="fallback">What the caller does instead, for the log line (e.g. "using keyword matching").</param>
    /// <returns>The reply's last text block, or null on any failure.</returns>
    public static async Task<string?> Send(
        HttpClient http, AssistantOptions o, JsonObject body, ILogger log, string fallback, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(o.TimeoutSeconds));
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.BaseUrl), "v1/messages"))
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Add("x-api-key", o.ApiKey);
            request.Headers.Add("anthropic-version", ApiVersion);
            if (DefaultFallbackModels.Contains(o.Model))
                request.Headers.Add("anthropic-beta", FallbackBeta);

            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Claude Messages API returned {Status}; {Fallback}", (int)response.StatusCode, fallback);
                return null;
            }
            var message = await response.Content.ReadFromJsonAsync<JsonObject>(timeout.Token);
            var stop = message?["stop_reason"]?.GetValue<string>();
            if (stop != "end_turn")
            {
                log.LogWarning("Claude stopped with {StopReason}; {Fallback}", stop ?? "nothing", fallback);
                return null;
            }
            return message!["content"]?.AsArray()
                .LastOrDefault(b => b?["type"]?.GetValue<string>() == "text")?["text"]?.GetValue<string>();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            log.LogWarning("Claude Messages API timed out after {Seconds}s; {Fallback}", o.TimeoutSeconds, fallback);
            return null;
        }
        // InvalidOperationException: a reply whose JSON has an unexpected shape (e.g. a non-string stop_reason).
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException or InvalidOperationException)
        {
            log.LogWarning("Claude Messages API call failed ({Error}); {Fallback}", e.GetType().Name, fallback);
            return null;
        }
    }
}
