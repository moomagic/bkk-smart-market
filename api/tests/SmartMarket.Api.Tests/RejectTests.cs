using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>.</summary>
[Collection(ApiCollection.Name)]
public sealed class RejectTests(ApiFactory api)
{
    private sealed record RejectedRunDto(Guid Id, string ReferenceNo, int Seed, string Status, string? RejectReason,
        bool RejectCountsTowardLimit, DateTimeOffset? DecidedAt, string? DecidedBy);
    private sealed record RoundDto(Guid Id, string Status, int RejectsLeft);

    private HttpClient Officer => api.Officer();
    private HttpClient Executive => api.ClientAs("executive", "executive-bkk");

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task A_reject_needs_a_reason(string? reason)
    {
        var (_, run) = await Draft();

        var response = await Officer.PostAsJsonAsync($"/api/allocation-runs/{run.Id}/reject", new { reason });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await Officer.GetFromJsonAsync<RejectedRunDto>($"/api/allocation-runs/{run.Id}", Json))!.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task A_rejected_draft_reopens_the_round_for_a_new_draw_with_a_new_seed()
    {
        var (roundId, run) = await Draft();

        var response = await Officer.PostAsJsonAsync($"/api/allocation-runs/{run.Id}/reject", new { reason = "ข้อมูลกลุ่มผู้ค้าผิด 1 คำขอ" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejected = (await response.Content.ReadFromJsonAsync<RejectedRunDto>(Json))!;
        rejected.Should().Match<RejectedRunDto>(r => r.Status == "Rejected" && r.RejectReason == "ข้อมูลกลุ่มผู้ค้าผิด 1 คำขอ"
            && r.RejectCountsTowardLimit && r.DecidedBy == "officer-bangphlat");
        (await Round(roundId)).Should().Match<RoundDto>(r => r.Status == "Closed" && r.RejectsLeft == 1);

        (await Officer.PostAsync($"/api/rounds/{roundId}/eligibility", null)).StatusCode.Should().Be(HttpStatusCode.OK,
            "an eligibility mistake can be fixed before the new draw");
        var rerun = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);
        rerun.StatusCode.Should().Be(HttpStatusCode.Created);
        var second = (await rerun.Content.ReadFromJsonAsync<RunDto>(Json))!;
        second.Seed.Should().NotBe(run.Seed);
        second.ReferenceNo.Should().NotBe(run.ReferenceNo);
    }

    [Fact]
    public async Task Vendors_see_every_run_of_the_round_including_rejected_ones_with_the_reason()
    {
        var (roundId, run) = await Draft();
        await Reject(run.Id, Officer, "ตรวจสิทธิผิดพลาด");
        var second = await Rerun(roundId);

        var mine = await api.Mine(run.Outcomes[0].VendorId, roundId);

        mine.Runs.Select(r => (r.ReferenceNo, r.Status)).Should().Equal((second.ReferenceNo, "Draft"), (run.ReferenceNo, "Rejected"));
        (await RunRefs(run.Outcomes[0].VendorId, roundId)).Last().RejectReason.Should().Be("ตรวจสิทธิผิดพลาด");
    }

    [Fact]
    public async Task Two_rejects_per_round_then_only_an_executive_may_reject()
    {
        var (roundId, run) = await Draft();
        await Reject(run.Id, Officer, "ครั้งที่ 1");
        await Reject((await Rerun(roundId)).Id, Officer, "ครั้งที่ 2");
        (await Round(roundId)).RejectsLeft.Should().Be(0);
        var third = await Rerun(roundId);

        var byOfficer = await Officer.PostAsJsonAsync($"/api/allocation-runs/{third.Id}/reject", new { reason = "ครั้งที่ 3" });
        byOfficer.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var byExecutive = await Executive.PostAsJsonAsync($"/api/allocation-runs/{third.Id}/reject", new { reason = "ครั้งที่ 3 ผู้บริหารอนุมัติ" });
        byExecutive.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task One_audit_entry_per_reject_filed_under_the_round()
    {
        var (roundId, run) = await Draft();
        var mark = await api.AuditMark();

        await Reject(run.Id, Officer, "เหตุผลทดสอบ");

        var entry = (await api.AuditSince(mark)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Round.RejectAllocation");
        entry.EntityType.Should().Be("Round");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Changes.Should().Contain("เหตุผลทดสอบ").And.Contain("Rejected");
    }

    [Fact]
    public async Task Only_a_draft_can_be_rejected_and_only_by_officers()
    {
        var (_, approved) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 1);
        (await Officer.PostAsJsonAsync($"/api/allocation-runs/{approved.Id}/reject", new { reason = "สาย" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var (_, draft) = await Draft();
        (await api.Vendor(Guid.NewGuid()).PostAsJsonAsync($"/api/allocation-runs/{draft.Id}/reject", new { reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Officer.PostAsJsonAsync($"/api/allocation-runs/{Guid.NewGuid()}/reject", new { reason = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(Guid RoundId, RunDto Run)> Draft()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 2);
        return (roundId, await Rerun(roundId));
    }

    private async Task<RunDto> Rerun(Guid roundId)
    {
        var response = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
    }

    private static async Task Reject(Guid runId, HttpClient client, string reason) =>
        (await client.PostAsJsonAsync($"/api/allocation-runs/{runId}/reject", new { reason })).StatusCode.Should().Be(HttpStatusCode.OK);

    private async Task<RoundDto> Round(Guid roundId) =>
        (await Officer.GetFromJsonAsync<RoundDto[]>("/api/rounds", Json))!.Single(r => r.Id == roundId);

    private sealed record RunRefWithReason(string ReferenceNo, string Status, string? RejectReason);
    private sealed record MineWithReasons(Guid RoundId, RunRefWithReason[] Runs);

    private async Task<RunRefWithReason[]> RunRefs(Guid vendorId, Guid roundId) =>
        (await api.Vendor(vendorId).GetFromJsonAsync<MineWithReasons[]>("/api/stall-requests/me", Json))!
        .Single(r => r.RoundId == roundId).Runs;
}
