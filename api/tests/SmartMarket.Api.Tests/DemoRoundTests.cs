using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartMarket.Application.CheckIns;
using SmartMarket.Infrastructure.Seed;
using static SmartMarket.Api.Tests.AllocationRunTests;
using static SmartMarket.Api.Tests.ApproveAllocationTests;

namespace SmartMarket.Api.Tests;

/// <summary>
/// The demo script on the seeded ปากซอยจรัญฯ 79 round, end to end, then again after a reset. It draws that round, so it
/// runs against a database of its own rather than the shared <see cref="ApiCollection"/> one, and the script is one test
/// so nothing else touches that round in between (the forbidden-reset test changes nothing).
/// </summary>
public sealed class DemoRoundTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task The_demo_script_runs_end_to_end_and_again_after_a_reset()
    {
        var before = await DashboardTests.Kpis(api.Officer());
        await RunDemoScript();
        api.Clock.Set(TestClock.DemoNow.AddDays(5));
        var mark = await api.AuditMark();

        var reset = await api.Officer().PostAsync("/api/demo/reset", null);

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.AuditSince(mark)).Should().ContainSingle("the reset is the first entry of the new trail")
            .Which.Action.Should().Be("Demo.Reset");
        api.Clock.Now.Should().BeCloseTo(TestClock.DemoNow, TimeSpan.FromSeconds(1), "the clock goes back to Demo:Now");
        (await DashboardTests.Kpis(api.Officer())).Should().BeEquivalentTo(before, "the seed and history are back as they were");
        (await api.ClientAs("vendor", SeedData.Somsri.ToString()).GetFromJsonAsync<MyRequestDto[]>("/api/stall-requests/me", Json))!
            .Should().NotContain(r => r.RoundId == SeedData.CharanCurrentRound, "สมศรี ใ. submits live again");
        await RunDemoScript();
    }

    [Fact]
    public async Task Only_officers_reset_the_demo()
    {
        (await api.ClientAs("vendor", SeedData.Somsri.ToString()).PostAsync("/api/demo/reset", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>สมศรี ใ. submits as the 11th → eligibility → run (demo seed) → approve → she wins B2, confirms and checks in.</summary>
    private async Task RunDemoScript()
    {
        var officer = api.Officer();
        var somsri = api.ClientAs("vendor", SeedData.Somsri.ToString());
        var before = await DashboardTests.Kpis(officer);
        (await somsri.PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await officer.PostAsync($"/api/rounds/{SeedData.CharanCurrentRound}/eligibility", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await officer.PostAsync($"/api/rounds/{SeedData.CharanCurrentRound}/allocation-runs", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        run.SpaceName.Should().Be("ปากซอยจรัญฯ 79");
        run.Seed.Should().Be(SeedData.CharanDemoSeed);
        run.ReferenceNo.Should().Be("BPL-2569-1025-01");
        run.InputHash.Should().HaveLength(64);
        run.Quota.Select(q => (q.Category, q.Base, q.Allocated))
            .Should().Equal(("Legacy", 2, 2), ("Vulnerable", 1, 1), ("New", 1, 1));
        run.Outcomes.Should().HaveCount(10, "ชัย ด. is ineligible and never drawn");
        run.Outcomes.Should().NotContain(o => o.VendorId == SeedData.VendorId(11));
        run.Outcomes.Single(o => o.VendorId == SeedData.VendorId(7)).Weight.Should().Be(2);
        run.Outcomes.Single(o => o.VendorId == SeedData.Somsri)
            .Should().Match<OutcomeDto>(o => o.VendorName == "สมศรี ใ." && o.Kind == "Won" && o.LockCode == "B2");
        (await VerifyAllocationRunTests.Verify(officer, run.Id))
            .Should().Match<VerifyAllocationRunTests.VerificationDto>(v => v.ResultMatches && v.InputHashMatches);

        (await officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await AnnouncementTests.Announcement(officer, run.Id)).Text.Should()
            .Contain("โควตา 2 · ได้สิทธิ์ 2").And.Contain("โควตา 1 · ได้สิทธิ์ 1").And.Contain("เลขอ้างอิง BPL-2569-1025-01")
            .And.NotContain("สมศรี");

        var mine = (await somsri.GetFromJsonAsync<MyRequestDto[]>("/api/stall-requests/me", Json))!
            .Single(r => r.RoundId == SeedData.CharanCurrentRound);
        mine.Status.Should().Be("Won");
        mine.LockCode.Should().Be("B2");
        mine.ReferenceNo.Should().Be("BPL-2569-1025-01");
        mine.ResultReason.Should().Contain("ได้สิทธิ์ในโควตากลุ่มผู้ค้าเดิม");
        mine.ConfirmBy.Should().NotBeNull();

        (await somsri.PostAsync($"/api/stall-requests/{mine.Id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await somsri.GetFromJsonAsync<MyRequestDto[]>("/api/stall-requests/me", Json))!
            .Single(r => r.RoundId == SeedData.CharanCurrentRound)
            .Should().Match<MyRequestDto>(r => r.Status == "Confirmed" && r.LockCode == "B2");
        var after = await DashboardTests.Kpis(officer);
        after.Occupancy.Count.Should().Be(before.Occupancy.Count + 1, "สมศรี ใ. confirmed B2");
        after.Occupancy.Of.Should().Be(before.Occupancy.Of, "the locks stay the same");
        after.LeadTime.Runs.Should().Be(before.LeadTime.Runs + 1);

        (await somsri.PostAsJsonAsync("/api/check-ins", new
        {
            stallRequestId = mine.Id, qrCode = CheckInQr.For(SeedData.PakSoiCharan79, "B2"), withinGeofence = true,
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await somsri.GetFromJsonAsync<MyRequestDto[]>("/api/stall-requests/me", Json))!
            .Single(r => r.RoundId == SeedData.CharanCurrentRound).LastCheckInAt.Should().NotBeNull();
    }
}
