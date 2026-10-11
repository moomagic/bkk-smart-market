using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Assistant;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AssistantTests(ApiFactory api)
{
    private sealed record CitationDto(string File, string Heading);
    private sealed record AnswerDto(string Text, bool Found, CitationDto[] Citations, string Source);

    [Fact]
    public async Task Somsri_asks_which_documents_she_needs_and_gets_a_cited_answer()
    {
        var response = await api.Vendor(Guid.NewGuid()).PostAsJsonAsync("/api/assistant/ask", new { question = "ต้องใช้เอกสารอะไร" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var answer = (await response.Content.ReadFromJsonAsync<AnswerDto>())!;
        answer.Found.Should().BeTrue();
        answer.Source.Should().Be("faq");
        answer.Citations.Should().ContainSingle().Which.File.Should().Be("documents.md");
        answer.Text.Should().Contain("บัตรประจำตัวประชาชน").And.Contain("อ้างอิง: documents.md");
        answer.Text.Split('\n').Length.Should().BeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task A_question_outside_the_rules_is_sent_to_the_district_office()
    {
        var answer = await Ask("ราคาทองวันนี้เท่าไร");

        answer.Found.Should().BeFalse();
        answer.Text.Should().Be("ไม่พบในระเบียบที่ประกาศ กรุณาติดต่อสำนักงานเขตบางพลัด");
    }

    [Fact]
    public async Task An_empty_question_is_a_bad_request_and_anonymous_callers_are_refused()
    {
        (await api.Vendor(Guid.NewGuid()).PostAsJsonAsync("/api/assistant/ask", new { question = " " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await api.CreateClient().PostAsJsonAsync("/api/assistant/ask", new { question = "เอกสาร" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void The_published_rules_carry_the_prototype_disclaimer_and_never_say_booking()
    {
        var sections = api.Services.GetRequiredService<IRulesLibrary>().Sections;
        var files = sections.Select(s => s.File).Distinct().ToList();

        files.Should().BeEquivalentTo(["allocation.md", "applying.md", "documents.md", "review.md", "using-the-space.md"]);
        foreach (var file in files)
        {
            var text = api.Services.GetRequiredService<IRulesLibrary>().Raw(file);
            text.Should().Contain("ระเบียบตัวอย่างสำหรับ prototype ไม่ใช่ระเบียบจริงของ กทม.", file);
            text.Should().NotContain("จอง", $"{file}: submitting is a request, never a booking (Guardrail 4)");
        }
    }

    [Fact]
    public void Every_published_section_can_be_found_and_fits_in_an_answer()
    {
        foreach (var s in api.Services.GetRequiredService<IRulesLibrary>().Sections)
        {
            s.Keywords.Should().NotBeEmpty($"{s.File} › {s.Heading} needs a keywords line, or the fallback can never answer it");
            s.Lines.Should().HaveCountLessThanOrEqualTo(3, $"{s.File} › {s.Heading}: answers are ≤ 4 lines with the citation");
        }
    }

    [Fact]
    public void The_allocation_rules_match_ADR_0001_and_0002()
    {
        var text = api.Services.GetRequiredService<IRulesLibrary>().Raw("allocation.md");

        text.Should().Contain("ผู้ค้าเดิมไม่เกิน 50%").And.Contain("กลุ่มเปราะบางอย่างน้อย 25%").And.Contain("ผู้ค้าใหม่อย่างน้อย 25%");
        text.Should().Contain("เปราะบาง → ผู้ค้าใหม่ → ผู้ค้าเดิม → เปราะบาง", "rollover cycle (ADR-0001)");
        text.Should().Contain("กลุ่มละไม่เกิน 3 คน", "waitlist per group (ADR-0001)");
        text.Should().Contain("รหัสล็อคสุ่มสลับ", "lock codes are shuffled (ADR-0001)");
        text.Should().Contain("seed สุ่มโดยระบบ เจ้าหน้าที่กำหนดเองไม่ได้", "ADR-0002");
        text.Should().Contain("ไม่เกิน 2 ครั้งต่อรอบ").And.Contain("ต้องระบุเหตุผล", "reject limit and reason (ADR-0002)");
    }

    private async Task<AnswerDto> Ask(string question) =>
        (await (await api.Vendor(Guid.NewGuid()).PostAsJsonAsync("/api/assistant/ask", new { question }))
            .Content.ReadFromJsonAsync<AnswerDto>())!;
}
