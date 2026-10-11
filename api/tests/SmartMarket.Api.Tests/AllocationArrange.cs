using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Audit;
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
    public static HttpClient Officer(this ApiFactory api) => api.ClientAs("officer", "officer-bangphlat");

    /// <summary>A fresh non-food space with 4 locks and an open round holding the given requests, eligibility already checked.</summary>
    public static async Task<Guid> CheckedRound(this ApiFactory api, int legacy, int vulnerable, int @new)
    {
        var roundId = Guid.NewGuid();
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var space = new Space(Guid.NewGuid(), SeedData.BangPhlat, $"ทดสอบจัดสรร {roundId:N}"[..20], SpaceType.Relaxation,
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

        (await api.Officer().PostAsync($"/api/rounds/{roundId}/eligibility", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return roundId;
    }

    /// <returns>The new vendor's id.</returns>
    public static async Task<Guid> AddRequest(this ApiFactory api, Guid roundId, Category category, StallRequestStatus status)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var vendor = new Vendor(Guid.NewGuid(), "ผู้ค้า ทดสอบจัดสรร", "089-000-1000",
            category == Category.Vulnerable ? "ผู้สูงอายุ" : null, "ทั่วไป", true, null, TestClock.DemoNow);
        db.Vendors.Add(vendor);
        db.StallRequests.Add(new StallRequest(Guid.NewGuid(), roundId, vendor.Id, TestClock.DemoNow, category, status));
        await db.CommitAsync("Test.Arrange", CancellationToken.None);
        return vendor.Id;
    }

    public static async Task<DateTimeOffset> LatestAuditAt(this ApiFactory api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.AuditEntries.MaxAsync(a => (DateTimeOffset?)a.At) ?? DateTimeOffset.MinValue;
    }

    public static async Task<List<AuditEntry>> AuditSince(this ApiFactory api, DateTimeOffset since)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.AuditEntries.Where(a => a.At > since).ToListAsync();
    }
}
