using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>Public announcement (CONTEXT.md): a draft for the officer to publish, verifiable and naming nobody.</summary>
[Collection(ApiCollection.Name)]
public sealed class AnnouncementTests(ApiFactory api)
{
    internal sealed record AnnouncementDto(Guid RunId, string Text, string Source);

    [Fact]
    public async Task An_approved_run_gets_a_template_draft_with_what_it_takes_to_verify_and_no_one_named()
    {
        var (_, run) = await api.ApprovedRound(legacy: 2, vulnerable: 2, @new: 3);

        var draft = await Announcement(api.Officer(), run.Id);

        draft.Source.Should().Be("template");
        draft.Text.Should().Contain($"เลขอ้างอิง {run.ReferenceNo}").And.Contain($"(seed) {run.Seed}").And.Contain(run.InputHash)
            .And.Contain("ไม่มีช่องทางจ่ายเงินเพื่อได้แผง").And.Contain("ข้อมูลจำลอง")
            .And.Contain("- ผู้ค้าเดิม: ผู้เข้าจับฉลาก 2 คน · โควตา 2 · ได้สิทธิ์ 2")
            .And.Contain("- กลุ่มเปราะบาง: ผู้เข้าจับฉลาก 2 คน · โควตา 1 · ได้สิทธิ์ 1")
            .And.Contain("- ผู้ค้าใหม่: ผู้เข้าจับฉลาก 3 คน · โควตา 1 · ได้สิทธิ์ 1");
        foreach (var o in run.Outcomes)
            draft.Text.Should().NotContain(o.VendorName).And.NotContain(o.VendorId.ToString());
    }

    [Fact]
    public async Task It_states_the_draw_record_so_a_later_forfeit_does_not_change_it()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 2, vulnerable: 2, @new: 2);
        var before = await Announcement(api.Officer(), run.Id);
        var forfeiter = run.Outcomes.Single(o => o.Category == "Vulnerable" && o.Kind == "Won");
        var request = await api.Mine(forfeiter.VendorId, roundId);
        (await api.Vendor(forfeiter.VendorId).PostAsync($"/api/stall-requests/{request.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await Announcement(api.Officer(), run.Id)).Text.Should().Be(before.Text);
    }

    [Fact]
    public async Task Only_an_approved_run_is_announced_and_drafting_writes_nothing()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
        var draft = (await (await api.Officer().PostAsync($"/api/rounds/{roundId}/allocation-runs", null)).Content.ReadFromJsonAsync<RunDto>(Json))!;
        (await api.Officer().PostAsync($"/api/allocation-runs/{draft.Id}/announcement", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await api.Officer().PostAsJsonAsync($"/api/allocation-runs/{draft.Id}/reject", new { reason = "ทดสอบประกาศ" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.Officer().PostAsync($"/api/allocation-runs/{draft.Id}/announcement", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await api.Officer().PostAsync($"/api/allocation-runs/{Guid.NewGuid()}/announcement", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var (_, approved) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 1);
        var since = await api.AuditMark();
        await Announcement(api.ClientAs("executive", "executive-bkk"), approved.Id);
        (await api.AuditSince(since)).Should().BeEmpty();
        (await api.Vendor(approved.Outcomes[0].VendorId).PostAsync($"/api/allocation-runs/{approved.Id}/announcement", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    internal static async Task<AnnouncementDto> Announcement(HttpClient client, Guid runId)
    {
        var response = await client.PostAsync($"/api/allocation-runs/{runId}/announcement", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AnnouncementDto>())!;
    }
}
