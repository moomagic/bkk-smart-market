using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class DashboardTests(ApiFactory api)
{
    internal sealed record Ratio(int Count, int Of, decimal? Percent);
    internal sealed record LeadTimeDto(decimal? MedianDays, int Runs);
    internal sealed record QuotaDto(string Category, int Base, int Winners);
    internal sealed record SpaceRowDto(Guid SpaceId, string Name, string? ReferenceNo, int Locks, int Winners, int Confirmed,
        QuotaDto[] Quota);
    internal sealed record KpisDto(Ratio Occupancy, LeadTimeDto LeadTime, Ratio NewVendors, Ratio FairRotation, SpaceRowDto[] Spaces);

    [Fact]
    public async Task Every_kpi_has_a_number_from_the_seeded_history()
    {
        var kpis = await Kpis(api.ClientAs("executive", "executive-bkk"));

        // Worked out from HistorySeed: MRT 5 + market 20 + พระราม 8 6 winners, all confirmed; ปากซอยจรัญฯ 79 has no
        // approved run yet, so its 4 locks count with none confirmed.
        kpis.Occupancy.Should().Be(new Ratio(31, 35, 88.6m));
        kpis.LeadTime.Should().Be(new LeadTimeDto(3.0m, 3), "median of 2, 4 and 3 days from close to approval");
        kpis.NewVendors.Should().Be(new Ratio(11, 31, 35.5m), "New winners: MRT 2 + market 5 + พระราม 8 4");
        kpis.FairRotation.Should().Be(new Ratio(22, 31, 71.0m), "no earlier round on record, so everyone but Legacy (1 + 8 + 0)");
    }

    [Fact]
    public async Task Each_space_shows_its_latest_approved_round_and_winners_against_quota()
    {
        var spaces = (await Kpis(api.Officer())).Spaces.ToDictionary(s => s.SpaceId);

        spaces.Should().HaveCount(4);
        spaces[SeedData.PakSoiCharan79].Should().Match<SpaceRowDto>(s => s.ReferenceNo == null && s.Locks == 4 && s.Winners == 0);
        var market = spaces[SeedData.BangPhlatMarket];
        market.ReferenceNo.Should().Be("BPL-2569-0422-02");
        // Legacy's shortfall rolled over to Vulnerable.
        market.Quota.Select(q => (q.Category, q.Base, q.Winners))
            .Should().Equal(("Legacy", 10, 8), ("Vulnerable", 5, 7), ("New", 5, 5));
    }

    [Fact]
    public async Task The_seeded_history_replays_from_its_stored_seed_and_rules()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        foreach (var run in await db.AllocationRuns.Where(r => r.ReferenceNo.StartsWith("BPL-2569-04") || r.ReferenceNo.StartsWith("BPL-2569-09")).ToListAsync())
        {
            var stored = AllocationJson.Deserialize<AllocationResult>(run.ResultJson);
            var spaceId = await db.Rounds.Where(r => r.Id == run.RoundId).Select(r => r.SpaceId).SingleAsync();
            var locks = await db.Locks.Where(l => l.SpaceId == spaceId).Select(l => l.Code).ToListAsync();
            var candidates = stored.Outcomes.Select(o => new Candidate(o.VendorId, o.Category, o.Weight, true)).ToList();

            var replay = AllocationEngine.Allocate(new AllocationInput(locks, candidates, run.Seed,
                AllocationJson.Deserialize<RulesSnapshot>(run.RulesSnapshot)));

            replay.InputHash.Should().Be(run.InputHash, run.ReferenceNo);
            stored.Should().BeEquivalentTo(replay, o => o.WithStrictOrdering());
        }
    }

    [Fact]
    public async Task The_history_has_three_months_of_check_ins_on_selling_days()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var days = await db.CheckIns.Where(c => c.Day < new DateOnly(2026, 10, 25)).Select(c => c.Day).ToListAsync();

        days.Should().NotBeEmpty();
        days.Min().Should().Be(new DateOnly(2026, 8, 1));
        days.Max().Should().BeOnOrBefore(new DateOnly(2026, 10, 24));
    }

    [Fact]
    public async Task Reading_the_dashboard_writes_nothing()
    {
        var mark = await api.AuditMark();

        await Kpis(api.Officer());

        (await api.AuditSince(mark)).Should().BeEmpty();
    }

    [Fact]
    public async Task Vendors_cannot_read_the_dashboard()
    {
        (await api.Vendor(Guid.NewGuid()).GetAsync($"/api/dashboard/kpis?districtId={SeedData.BangPhlat}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    internal static async Task<KpisDto> Kpis(HttpClient client) =>
        (await client.GetFromJsonAsync<KpisDto>($"/api/dashboard/kpis?districtId={SeedData.BangPhlat}", AllocationRunTests.Json))!;
}
