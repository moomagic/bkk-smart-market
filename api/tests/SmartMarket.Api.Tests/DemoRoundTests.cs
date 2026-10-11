using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartMarket.Infrastructure.Seed;
using static SmartMarket.Api.Tests.AllocationRunTests;
using static SmartMarket.Api.Tests.ApproveAllocationTests;

namespace SmartMarket.Api.Tests;

/// <summary>
/// The demo script on the seeded ปากซอยจรัญฯ 79 round, end to end, as one test: it draws that round, so it runs
/// against a database of its own rather than the shared <see cref="ApiCollection"/> one.
/// </summary>
public sealed class DemoRoundTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Somsri_submits_as_the_11th_officer_checks_runs_and_approves_and_she_wins_and_confirms_B2()
    {
        var officer = api.Officer();
        var somsri = api.ClientAs("vendor", SeedData.Somsri.ToString());
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

        (await officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

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
    }
}
