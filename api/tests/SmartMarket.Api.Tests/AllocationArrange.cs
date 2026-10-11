using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Districts;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Seed;
using Lock = SmartMarket.Domain.Spaces.Lock;

namespace SmartMarket.Api.Tests;

/// <summary>
/// Drawing flips a round to Drawn, so allocation tests on the shared database each draw a round of their own
/// (a fresh space with fresh vendors) and leave the seeded rounds to the other test classes.
/// </summary>
internal static class AllocationArrange
{
    /// <summary>
    /// Fresh spaces live in a district of their own, so listings and matching scoped to บางพลัด only ever see the
    /// four seeded spaces, whatever order the test classes run in.
    /// </summary>
    public static readonly Guid TestDistrict = new("10000000-0000-0000-0000-0000000000ff");

    public static HttpClient Officer(this ApiFactory api) => api.ClientAs("officer", "officer-bangphlat");

    /// <summary>A fresh non-food space with 4 locks and an open round holding the given requests, eligibility already checked.</summary>
    /// <param name="banned">Extra New-group vendors under a ban, so eligibility marks them Ineligible.</param>
    public static async Task<Guid> CheckedRound(this ApiFactory api, int legacy, int vulnerable, int @new, int banned = 0)
    {
        var roundId = Guid.NewGuid();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            if (!await db.Districts.AnyAsync(d => d.Id == TestDistrict))
                db.Districts.Add(new District(TestDistrict, "เขตทดสอบ"));
            var space = new Space(Guid.NewGuid(), TestDistrict, $"ทดสอบจัดสรร {roundId:N}"[..20], SpaceType.Relaxation,
                ["ทั่วไป"], "ทุกวัน", "docs/rules");
            db.Spaces.Add(space);
            db.Locks.AddRange(Enumerable.Range(1, 4).Select(i => new Lock(Guid.NewGuid(), space.Id, $"B{i}")));
            var starts = new DateOnly(2026, 11, 1);
            db.Rounds.Add(new Round(roundId, space.Id, starts, starts.AddMonths(6).AddDays(-1),
                SeedData.CurrentApplyOpensAt, SeedData.CurrentApplyClosesAt, RoundStatus.Open));
            await db.CommitAsync("Test.Arrange", CancellationToken.None);
        }
        foreach (var (category, count) in new[] { (Category.Legacy, legacy), (Category.Vulnerable, vulnerable), (Category.New, @new) })
            for (var i = 0; i < count; i++)
                await api.AddRequest(roundId, category, StallRequestStatus.Submitted);
        for (var i = 0; i < banned; i++)
            await api.AddRequest(roundId, Category.New, StallRequestStatus.Submitted, bannedUntil: new DateOnly(2027, 12, 31));

        (await api.Officer().PostAsync($"/api/rounds/{roundId}/eligibility", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return roundId;
    }

    /// <returns>The new vendor's id.</returns>
    public static async Task<Guid> AddRequest(
        this ApiFactory api, Guid roundId, Category category, StallRequestStatus status, DateOnly? bannedUntil = null)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var vendor = new Vendor(Guid.NewGuid(), "ผู้ค้า ทดสอบจัดสรร", "089-000-1000",
            category == Category.Vulnerable ? "ผู้สูงอายุ" : null, "ทั่วไป", true, bannedUntil, TestClock.DemoNow);
        db.Vendors.Add(vendor);
        db.StallRequests.Add(new StallRequest(Guid.NewGuid(), roundId, vendor.Id, TestClock.DemoNow, category, status));
        await db.CommitAsync("Test.Arrange", CancellationToken.None);
        return vendor.Id;
    }

    /// <summary>A fresh round drawn and approved; returns the approved run as the officer saw its draft.</summary>
    public static async Task<(Guid RoundId, AllocationRunTests.RunDto Run)> ApprovedRound(
        this ApiFactory api, int legacy, int vulnerable, int @new, int banned = 0)
    {
        var roundId = await api.CheckedRound(legacy, vulnerable, @new, banned);
        var response = await api.Officer().PostAsync($"/api/rounds/{roundId}/allocation-runs", null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<AllocationRunTests.RunDto>(AllocationRunTests.Json))!;
        (await api.Officer().PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return (roundId, run);
    }

    public static HttpClient Vendor(this ApiFactory api, Guid vendorId) => api.ClientAs("vendor", vendorId.ToString());

    /// <summary>The vendor's own request in the round, as /stall-requests/me shows it.</summary>
    public static async Task<ApproveAllocationTests.MyRequestDto> Mine(this ApiFactory api, Guid vendorId, Guid roundId) =>
        (await api.Vendor(vendorId).GetFromJsonAsync<ApproveAllocationTests.MyRequestDto[]>("/api/stall-requests/me", AllocationRunTests.Json))!
        .Single(r => r.RoundId == roundId);
}
