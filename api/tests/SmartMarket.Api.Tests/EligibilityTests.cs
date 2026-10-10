using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class EligibilityTests(ApiFactory api)
{
    private sealed record Row(Guid RequestId, Guid VendorId, string VendorName, string Category, string Status, string? IneligibleReason, int CarryOverWeight);
    private sealed record MyRequest(Guid RoundId, string Status, string? IneligibleReason);

    private HttpClient Officer => api.ClientAs("officer", "officer-bangphlat");

    [Fact]
    public async Task Officer_checks_the_whole_charan_round()
    {
        var response = await Officer.PostAsync($"/api/rounds/{SeedData.CharanCurrentRound}/eligibility", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = (await response.Content.ReadFromJsonAsync<Row[]>())!;
        list.Select(r => r.Category).Distinct().Should().Equal("Legacy", "Vulnerable", "New");
        var rows = list.ToDictionary(r => r.VendorId);

        var chai = rows[SeedData.VendorId(11)];
        chai.Status.Should().Be("Ineligible");
        chai.IneligibleReason.Should().Be("ยังไม่ผ่านการอบรมสุขาภิบาลอาหาร");
        chai.VendorName.Should().Be("ชัย ด.", "officers see masked names");

        rows[SeedData.VendorId(7)].Should().Match<Row>(r => r.Status == "Eligible" && r.CarryOverWeight == 2,
            "ปิยะ น. was NotSelected here last round");
        rows[SeedData.VendorId(2)].Should().Match<Row>(r => r.Status == "Eligible" && r.CarryOverWeight == 1,
            "บุญมี ร. held a lock last round");

        var mine = await api.ClientAs("vendor", SeedData.VendorId(11).ToString())
            .GetFromJsonAsync<MyRequest[]>("/api/stall-requests/me");
        mine!.Single(r => r.RoundId == SeedData.CharanCurrentRound)
            .Should().Be(new MyRequest(SeedData.CharanCurrentRound, "Ineligible", "ยังไม่ผ่านการอบรมสุขาภิบาลอาหาร"));
    }

    [Fact]
    public async Task One_audit_entry_per_run_filed_under_the_round_even_when_nothing_changes()
    {
        await Officer.PostAsync($"/api/rounds/{SeedData.MrtCurrentRound}/eligibility", null);

        foreach (var _ in Enumerable.Range(0, 2))
        {
            var since = await LatestAuditAt();
            (await Officer.PostAsync($"/api/rounds/{SeedData.MrtCurrentRound}/eligibility", null))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            var entry = (await AuditSince(since)).Should().ContainSingle().Subject;
            entry.Action.Should().Be("Round.CheckEligibility");
            entry.EntityType.Should().Be("Round");
            entry.EntityId.Should().Be(SeedData.MrtCurrentRound.ToString());
            entry.Actor.Should().Be("officer-bangphlat");
        }
    }

    [Fact]
    public async Task A_second_trial_area_request_in_the_same_year_is_ineligible()
    {
        var vendorId = await VendorWithEarlierTrialAreaRequestThisYear();
        (await api.ClientAs("vendor", vendorId.ToString())
            .PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.Rama8CurrentRound }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var rows = await (await Officer.PostAsync($"/api/rounds/{SeedData.Rama8CurrentRound}/eligibility", null))
            .Content.ReadFromJsonAsync<Row[]>();

        var row = rows!.Single(r => r.VendorId == vendorId);
        row.Status.Should().Be("Ineligible");
        row.IneligibleReason.Should().Contain("พื้นที่ทดลองค้าได้ 1 ครั้งต่อปี");
    }

    [Fact]
    public async Task A_later_trial_area_round_in_the_same_year_does_not_count_against_this_one()
    {
        var vendorId = await VendorWithTrialAreaRequestIn(new DateOnly(2026, 12, 1), StallRequestStatus.Submitted);
        (await api.ClientAs("vendor", vendorId.ToString())
            .PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.Rama8CurrentRound }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var rows = await (await Officer.PostAsync($"/api/rounds/{SeedData.Rama8CurrentRound}/eligibility", null))
            .Content.ReadFromJsonAsync<Row[]>();

        rows!.Single(r => r.VendorId == vendorId).Status.Should().Be("Eligible");
    }

    [Fact]
    public async Task Officer_sees_rounds_with_request_counts()
    {
        var rounds = await Officer.GetFromJsonAsync<RoundDto[]>("/api/rounds");

        rounds!.Single(r => r.Id == SeedData.CharanCurrentRound)
            .Should().Match<RoundDto>(r => r.SpaceName == "ปากซอยจรัญฯ 79" && r.RequestCount >= 10);
    }

    [Theory]
    [InlineData("vendor", HttpStatusCode.Forbidden)]
    [InlineData("executive", HttpStatusCode.OK)]
    public async Task Only_officers_and_executives_check_eligibility(string role, HttpStatusCode expected)
    {
        var response = await api.ClientAs(role, "someone").PostAsync($"/api/rounds/{SeedData.MarketCurrentRound}/eligibility", null);

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Unknown_round_is_not_found()
    {
        (await Officer.PostAsync($"/api/rounds/{Guid.NewGuid()}/eligibility", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record RoundDto(Guid Id, string SpaceName, int RequestCount);

    /// <summary>A vendor who already had a (finished) TrialArea request earlier in 2026.</summary>
    private Task<Guid> VendorWithEarlierTrialAreaRequestThisYear() =>
        VendorWithTrialAreaRequestIn(new DateOnly(2026, 6, 1), StallRequestStatus.NotSelected);

    /// <summary>A vendor with a request in a one-month TrialArea round at ลานใต้สะพานพระราม 8 starting <paramref name="startsOn"/>.</summary>
    private async Task<Guid> VendorWithTrialAreaRequestIn(DateOnly startsOn, StallRequestStatus status)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var vendor = new Vendor(Guid.NewGuid(), "ทดสอบ ทดลองค้า", "089-000-0001", null, "เสื้อผ้า", true, null, TestClock.DemoNow);
        var other = await db.Rounds.SingleOrDefaultAsync(r => r.SpaceId == SeedData.Rama8BridgeYard && r.StartsOn == startsOn)
            ?? new Round(Guid.NewGuid(), SeedData.Rama8BridgeYard, startsOn, startsOn.AddMonths(1).AddDays(-1),
                TestClock.DemoNow.AddMonths(-6), TestClock.DemoNow.AddMonths(-5), RoundStatus.Approved);
        if (db.Rounds.Local.All(r => r.Id != other.Id) && !await db.Rounds.AnyAsync(r => r.Id == other.Id)) db.Rounds.Add(other);
        db.Vendors.Add(vendor);
        db.StallRequests.Add(new StallRequest(Guid.NewGuid(), other.Id, vendor.Id, TestClock.DemoNow.AddMonths(-6),
            Category.New, status));
        await db.CommitAsync("Test.Arrange", CancellationToken.None);
        return vendor.Id;
    }

    private async Task<DateTimeOffset> LatestAuditAt()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.AuditEntries.MaxAsync(a => (DateTimeOffset?)a.At) ?? DateTimeOffset.MinValue;
    }

    private async Task<List<Domain.Audit.AuditEntry>> AuditSince(DateTimeOffset since)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.AuditEntries.Where(a => a.At > since).ToListAsync();
    }
}
