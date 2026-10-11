using System.Text;
using SmartMarket.Application.Common;

namespace SmartMarket.Application.Assistant;

public sealed record AskAssistant(string? Question);

/// <summary>
/// Vendor Assistant. Answers from <c>docs/rules</c> only: through the AI model when one is configured, otherwise — or
/// on any model failure — by keyword matching (<see cref="FaqAssistant"/>). Questions about anyone's outcome or asking
/// for a guarantee are declined before any model sees them (Guardrail 1). A model answer must cite a section that
/// exists, and is cut to the same shape as the fallback: up to 3 lines plus the citation. Reads, changes nothing, so it
/// writes no audit entry; the question is never logged (it may contain personal data — Guardrail 6).
/// </summary>
public sealed class AskAssistantHandler(IRulesLibrary rules, IAssistantModel model)
{
    public const int MaxQuestionLength = 500;
    public const string ModelSource = "claude";

    public async Task<Result<AssistantAnswer>> Handle(AskAssistant command, CancellationToken ct)
    {
        var question = command.Question?.Trim() ?? "";
        if (question.Length == 0)
            return Error.Invalid("assistant.empty_question", "กรุณาพิมพ์คำถาม");
        if (question.Length > MaxQuestionLength)
            return Error.Invalid("assistant.question_too_long", $"คำถามยาวเกิน {MaxQuestionLength} ตัวอักษร");
        AssistantAnswer Fallback() => FaqAssistant.Answer(question, rules.Sections);
        if (FaqAssistant.AsksForIndividualOutcome(question) || !model.IsConfigured)
            return Fallback();

        var reply = await model.Ask(question, RulesContext(), ct);
        return (reply is null ? null : FromModel(reply)) ?? Fallback();
    }

    /// <returns>Null when the reply can't be trusted as is (claims an answer but cites nothing real).</returns>
    private AssistantAnswer? FromModel(ModelReply reply)
    {
        if (!reply.Found)
            return new AssistantAnswer(FaqAssistant.NotFound, false, [], ModelSource);

        var known = rules.Sections.Select(s => new Citation(s.File, s.Heading)).ToHashSet();
        var citations = reply.Citations.Where(known.Contains).Distinct().ToList();
        var lines = reply.Answer.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !l.StartsWith("อ้างอิง", StringComparison.Ordinal))
            .Take(FaqAssistant.MaxBodyLines).ToList();
        if (citations.Count == 0 || lines.Count == 0)
            return null;

        return new AssistantAnswer(string.Join('\n', lines.Append(FaqAssistant.CitationLine(citations))), true, citations, ModelSource);
    }

    /// <summary>Every rules file verbatim, in file order — stable, so the model's cached prefix stays valid.</summary>
    private string RulesContext()
    {
        var text = new StringBuilder();
        foreach (var file in rules.Files)
            text.Append("<file name=\"").Append(file).Append("\">\n").Append(rules.Raw(file).Trim()).Append("\n</file>\n");
        return text.ToString();
    }
}
