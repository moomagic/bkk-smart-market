using SmartMarket.Application.Common;

namespace SmartMarket.Application.Assistant;

public sealed record AskAssistant(string? Question);

/// <summary>
/// Vendor Assistant. Answers from <c>docs/rules</c> only. Reads, changes nothing, so it writes no audit entry; the
/// question is never logged (it may contain personal data — Guardrail 6).
/// </summary>
public sealed class AskAssistantHandler(IRulesLibrary rules)
{
    public const int MaxQuestionLength = 500;

    public Task<Result<AssistantAnswer>> Handle(AskAssistant command, CancellationToken ct) =>
        Task.FromResult(Answer(command.Question?.Trim() ?? ""));

    private Result<AssistantAnswer> Answer(string question) =>
        question.Length == 0 ? Error.Invalid("assistant.empty_question", "กรุณาพิมพ์คำถาม")
        : question.Length > MaxQuestionLength ? Error.Invalid("assistant.question_too_long", $"คำถามยาวเกิน {MaxQuestionLength} ตัวอักษร")
        : FaqAssistant.Answer(question, rules.Sections);
}
