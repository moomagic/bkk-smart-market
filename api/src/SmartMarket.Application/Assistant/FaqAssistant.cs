namespace SmartMarket.Application.Assistant;

public sealed record Citation(string File, string Heading);

/// <param name="Found">False when the answer is the not-found or the individual-outcome reply.</param>
/// <param name="Source">"faq" for keyword matching; the AI path (#22) reports its own.</param>
public sealed record AssistantAnswer(string Text, bool Found, IReadOnlyList<Citation> Citations, string Source = FaqAssistant.Source);

/// <summary>
/// The assistant without an AI model: keyword matching against the rules' section keywords (CLAUDE.md › AI components).
/// Pure. It recommends and explains only — never an individual's outcome, never a promise of a lock (Guardrail 1).
/// </summary>
public static class FaqAssistant
{
    public const string Source = "faq";
    public const string NotFound = "ไม่พบในระเบียบที่ประกาศ กรุณาติดต่อสำนักงานเขตบางพลัด";
    public const string NoIndividualOutcomes =
        "ผู้ช่วยตอบเรื่องผลการจัดสรรของแต่ละคนหรือรับปากเรื่องสิทธิ์ไม่ได้ ผลของคุณดูได้ที่ \"คำขอของฉัน\" หลังเจ้าหน้าที่อนุมัติผล";

    // Asking about one person's outcome, or for a guarantee. Thai has no word breaks and many spellings, so this
    // looks for an outcome phrase + a yes/no question, unless the question is about a group ("ผู้สูงอายุได้สิทธิ์ไหม").
    private static readonly string[] OutcomePhrases = ["ได้ล็อค", "ได้ล็อก", "ได้สิทธิ", "ได้ที่", "ติดสำรอง", "ชนะ", "ถูกจับ"];
    private static readonly string[] YesNo = ["ไหม", "มั้ย", "หรือเปล่า", "รึเปล่า", "หรือยัง", "ป่าว"];
    // Not "ใคร": "ใครได้ล็อค B2 ไหม" asks who won — an individual outcome.
    private static readonly string[] GroupTerms = ["ผู้สูงอายุ", "ผู้พิการ", "คนพิการ", "เปราะบาง", "ผู้ค้าเดิม", "ผู้ค้าใหม่", "กลุ่ม"];
    private static readonly string[] OwnResultOrGuarantee =
        ["ผลของฉัน", "ผลของผม", "ผลของหนู", "ผลของเรา", "ผลของดิฉัน", "ผลจับฉลากของ", "รับรองได้ไหม", "รับประกัน", "การันตี"];

    /// <summary>Shared by every path: such a question never reaches a model (Guardrail 1).</summary>
    public static bool AsksForIndividualOutcome(string q) =>
        OwnResultOrGuarantee.Any(q.Contains)
        || (OutcomePhrases.Any(q.Contains) && YesNo.Any(q.Contains) && !GroupTerms.Any(q.Contains));

    /// <summary>Up to 3 lines of the section plus a citation line: at most 4 lines.</summary>
    public const int MaxBodyLines = 3;

    public static string CitationLine(IEnumerable<Citation> cited) => $"อ้างอิง: {string.Join(" · ", cited.Select(c => $"{c.File} › {c.Heading}"))}";

    public static AssistantAnswer Answer(string question, IReadOnlyList<RuleSection> sections)
    {
        var q = question.Trim();
        if (AsksForIndividualOutcome(q))
            return new AssistantAnswer(NoIndividualOutcomes, false, []);

        // Longer keyword matches are more specific, so score by matched length; ties keep file/section order.
        var best = sections
            .Select((s, i) => (Section: s, Order: i, Score: s.Keywords.Where(k => q.Contains(k, StringComparison.Ordinal)).Sum(k => k.Length)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Order)
            .Select(x => x.Section)
            .FirstOrDefault();
        if (best is null)
            return new AssistantAnswer(NotFound, false, []);

        var citation = new Citation(best.File, best.Heading);
        var text = string.Join('\n', best.Lines.Take(MaxBodyLines).Append(CitationLine([citation])));
        return new AssistantAnswer(text, true, [citation]);
    }
}
