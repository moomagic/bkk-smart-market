using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartMarket.Infrastructure.Seed;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>
/// The demo script on the seeded ปากซอยจรัญฯ 79 round, end to end. It draws that round, so it runs
/// against a database of its own rather than the shared <see cref="ApiCollection"/> one.
/// </summary>
public sealed class DemoRoundTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Charan_79_with_somsri_as_the_11th_request_draws_two_one_one()
    {
        var officer = api.ClientAs("officer", "officer-bangphlat");
        (await api.ClientAs("vendor", SeedData.Somsri.ToString())
            .PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await officer.PostAsync($"/api/rounds/{SeedData.CharanCurrentRound}/eligibility", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await officer.PostAsync($"/api/rounds/{SeedData.CharanCurrentRound}/allocation-runs", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        run.SpaceName.Should().Be("ปากซอยจรัญฯ 79");
        run.ReferenceNo.Should().Be("BPL-2569-1025-01");
        run.InputHash.Should().HaveLength(64);
        run.Quota.Select(q => (q.Category, q.Base, q.Allocated))
            .Should().Equal(("Legacy", 2, 2), ("Vulnerable", 1, 1), ("New", 1, 1));
        run.Outcomes.Should().HaveCount(10, "ชัย ด. is ineligible and never drawn");
        run.Outcomes.Should().NotContain(o => o.VendorId == SeedData.VendorId(11));
        run.Outcomes.Single(o => o.VendorId == SeedData.VendorId(7)).Weight.Should().Be(2);
        run.Outcomes.Single(o => o.VendorId == SeedData.Somsri)
            .Should().Match<OutcomeDto>(o => o.VendorName == "สมศรี ใ." && o.Kind == "Won");
    }
}
