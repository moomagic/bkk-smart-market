using FluentAssertions;
using SmartMarket.Application.Assistant;

namespace SmartMarket.Application.Tests.Assistant;

public sealed class FaqAssistantTests
{
    private const string Documents = """
        # เอกสารที่ต้องใช้

        > ระเบียบตัวอย่างสำหรับ prototype ไม่ใช่ระเบียบจริงของ กทม.

        ## เอกสารที่ต้องใช้ในการยื่นคำขอ
        <!-- keywords: เอกสาร, ใช้อะไร, บัตรประชาชน -->
        1. บัตรประจำตัวประชาชน
        2. เบอร์โทรศัพท์
        3. ใบรับรองอบรมสุขาภิบาลอาหาร
        4. หลักฐานกลุ่มเปราะบาง
        5. บรรทัดที่ห้า

        ## หลักฐานกลุ่มเปราะบาง
        <!-- keywords: เปราะบาง, ผู้สูงอายุ -->
        แสดงบัตรผู้สูงอายุหรือบัตรคนพิการ
        """;

    private const string Allocation = """
        # กติกาจัดสรร

        ## สำรอง
        <!-- keywords: สำรอง, คิว -->
        กลุ่มละไม่เกิน 3 คน
        """;

    private static readonly IReadOnlyList<RuleSection> Library =
        [.. RulesMarkdown.Parse("documents.md", Documents), .. RulesMarkdown.Parse("allocation.md", Allocation)];

    [Fact]
    public void Parses_sections_with_their_file_title_heading_keywords_and_body()
    {
        var sections = RulesMarkdown.Parse("documents.md", Documents);

        sections.Should().HaveCount(2);
        sections[0].Should().Match<RuleSection>(s =>
            s.File == "documents.md" && s.FileTitle == "เอกสารที่ต้องใช้" && s.Heading == "เอกสารที่ต้องใช้ในการยื่นคำขอ");
        sections[0].Keywords.Should().Equal("เอกสาร", "ใช้อะไร", "บัตรประชาชน");
        sections[0].Lines.Should().HaveCount(5).And.NotContain(l => l.Contains("keywords"));
    }

    [Fact]
    public void Answers_from_the_best_matching_section_and_cites_file_and_heading()
    {
        var answer = FaqAssistant.Answer("ต้องใช้เอกสารอะไรบ้างคะ", Library);

        answer.Found.Should().BeTrue();
        answer.Citations.Should().ContainSingle().Which.Should().Be(new Citation("documents.md", "เอกสารที่ต้องใช้ในการยื่นคำขอ"));
        answer.Text.Should().Contain("บัตรประจำตัวประชาชน");
        answer.Text.Split('\n').Should().HaveCountLessThanOrEqualTo(4, "answers are at most 4 lines");
        answer.Text.Should().EndWith("อ้างอิง: documents.md › เอกสารที่ต้องใช้ในการยื่นคำขอ");
    }

    [Fact]
    public void The_section_matching_more_keywords_wins()
    {
        FaqAssistant.Answer("ผู้สูงอายุกลุ่มเปราะบางใช้หลักฐานอะไร", Library)
            .Citations.Single().Heading.Should().Be("หลักฐานกลุ่มเปราะบาง");
        FaqAssistant.Answer("คิวสำรองมีกี่คน", Library).Citations.Single().File.Should().Be("allocation.md");
    }

    [Theory]
    [InlineData("ราคาทองวันนี้เท่าไร")]
    [InlineData("   ")]
    public void A_question_outside_the_rules_gets_the_not_found_answer(string question)
    {
        var answer = FaqAssistant.Answer(question, Library);

        answer.Found.Should().BeFalse();
        answer.Text.Should().Be(FaqAssistant.NotFound);
        FaqAssistant.NotFound.Should().Be("ไม่พบในระเบียบที่ประกาศ กรุณาติดต่อสำนักงานเขตบางพลัด");
        answer.Citations.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ฉันได้ล็อคไหม")]
    [InlineData("ผลของฉันเป็นยังไง")]
    [InlineData("สมศรีได้สิทธิ์ไหม")]
    [InlineData("รับรองได้ไหมว่าจะได้ที่")]
    [InlineData("ได้สิทธิไหมคะ")]
    [InlineData("ได้ล็อคหรือเปล่า")]
    [InlineData("ผลจับฉลากของฉันออกยัง")]
    [InlineData("ใครได้ล็อค B2 ไหม")]
    [InlineData("บุญมีติดสำรองไหม")]
    public void Never_discusses_individual_outcomes_or_promises_a_lock(string question)
    {
        var answer = FaqAssistant.Answer(question, Library);

        answer.Found.Should().BeFalse();
        answer.Text.Should().Be(FaqAssistant.NoIndividualOutcomes);
        answer.Citations.Should().BeEmpty();
    }

    [Fact]
    public void A_rules_question_about_a_group_is_still_answered()
    {
        var answer = FaqAssistant.Answer("ผู้สูงอายุได้สิทธิ์ไหม ต้องใช้หลักฐานกลุ่มเปราะบางอะไร", Library);

        answer.Found.Should().BeTrue();
        answer.Citations.Single().Heading.Should().Be("หลักฐานกลุ่มเปราะบาง");
    }
}
