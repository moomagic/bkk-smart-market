using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class StallRequestsTests(ApiFactory api) : IDisposable
{
    private sealed record RequestDto(Guid Id, Guid RoundId, string SpaceName, string Category, string Status);

    public void Dispose() => api.Clock.Reset();

    [Fact]
    public async Task Somsri_submits_at_charan_79_and_is_legacy()
    {
        var somsri = api.ClientAs("vendor", SeedData.Somsri.ToString());

        var response = await somsri.PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<RequestDto>();
        created!.Status.Should().Be("Submitted");
        created.Category.Should().Be("Legacy", "she held a lock here last round");
        created.SpaceName.Should().Be("ปากซอยจรัญฯ 79");

        var mine = await somsri.GetFromJsonAsync<RequestDto[]>("/api/stall-requests/me");
        mine!.Should().ContainSingle(r => r.RoundId == SeedData.CharanCurrentRound && r.Status == "Submitted");
    }

    [Fact]
    public async Task Submitting_writes_one_audit_entry_attributed_to_the_demo_user()
    {
        var vendorId = await NewVendor("ทดสอบ ออดิต");
        var since = await LatestAuditAt();

        var response = await api.ClientAs("vendor", vendorId.ToString())
            .PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.MrtCurrentRound });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var entries = await AuditSince(since);
        var entry = entries.Should().ContainSingle().Subject;
        entry.Action.Should().Be("StallRequest.Submit");
        entry.EntityType.Should().Be("StallRequest");
        entry.Actor.Should().Be(vendorId.ToString());
        entry.ActorRole.Should().Be("vendor");
    }

    [Theory]
    [InlineData(null, "New")]
    [InlineData("ผู้พิการ", "Vulnerable")]
    public async Task Category_is_snapshotted_from_the_vendor(string? vulnerableReason, string expected)
    {
        var vendorId = await NewVendor("ทดสอบ กลุ่ม", vulnerableReason);

        var created = await (await api.ClientAs("vendor", vendorId.ToString())
            .PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound }))
            .Content.ReadFromJsonAsync<RequestDto>();

        created!.Category.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "ปากซอยจรัญฯ 79", "Legacy")] // held a lock here last round
    [InlineData(null, "หน้า MRT บางพลัด ทางออก 1", "New")] // Legacy only at the same space
    [InlineData("ผู้สูงอายุ", "ปากซอยจรัญฯ 79", "Vulnerable")] // Vulnerable outranks Legacy
    public async Task Legacy_is_per_space_and_vulnerable_takes_precedence(string? vulnerableReason, string space, string expected)
    {
        var vendorId = await NewVendor("ทดสอบ ผู้ค้าเดิม", vulnerableReason, heldLockAtCharanLastRound: true);
        var roundId = space == "ปากซอยจรัญฯ 79" ? SeedData.CharanCurrentRound : SeedData.MrtCurrentRound;

        var created = await (await api.ClientAs("vendor", vendorId.ToString())
            .PostAsJsonAsync("/api/stall-requests", new { roundId }))
            .Content.ReadFromJsonAsync<RequestDto>();

        created!.Category.Should().Be(expected);
    }

    [Fact]
    public async Task A_second_request_in_the_district_for_the_same_period_is_refused_with_a_reason()
    {
        var client = api.ClientAs("vendor", (await NewVendor("ทดสอบ สองคำขอ")).ToString());
        (await client.PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.MarketCurrentRound });

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        problem!.Detail.Should().Contain("ปากซอยจรัญฯ 79").And.Contain("1 คำขอ");
    }

    [Fact]
    public async Task Submitting_after_the_window_closes_is_refused()
    {
        var client = api.ClientAs("vendor", (await NewVendor("ทดสอบ ปิดรับ")).ToString());
        api.Clock.Set(SeedData.CurrentApplyClosesAt.AddSeconds(1));

        var response = await client.PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Contain("ไม่ได้เปิดรับคำขอ");
    }

    [Fact]
    public async Task Unknown_round_is_not_found()
    {
        var client = api.ClientAs("vendor", (await NewVendor("ทดสอบ ไม่มีรอบ")).ToString());

        var response = await client.PostAsJsonAsync("/api/stall-requests", new { roundId = Guid.NewGuid() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("officer", HttpStatusCode.Forbidden)]
    public async Task Only_vendors_can_submit(string? role, HttpStatusCode expected)
    {
        var client = role is null ? api.CreateClient() : api.ClientAs(role, "someone");

        var response = await client.PostAsJsonAsync("/api/stall-requests", new { roundId = SeedData.CharanCurrentRound });

        response.StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Spaces_list_shows_the_round_open_for_requests()
    {
        var spaces = await api.CreateClient().GetFromJsonAsync<JsonSpace[]>("/api/spaces");

        spaces!.Single(s => s.Name == "ปากซอยจรัญฯ 79").OpenRound!.Id.Should().Be(SeedData.CharanCurrentRound);

        api.Clock.Set(SeedData.CurrentApplyClosesAt.AddDays(1));
        var closed = await api.CreateClient().GetFromJsonAsync<JsonSpace[]>("/api/spaces");
        closed!.Should().OnlyContain(s => s.OpenRound == null);
    }

    private sealed record JsonOpenRound(Guid Id);
    private sealed record JsonSpace(string Name, JsonOpenRound? OpenRound);

    private async Task<Guid> NewVendor(string name, string? vulnerableReason = null, bool heldLockAtCharanLastRound = false)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var vendor = new Vendor(Guid.NewGuid(), name, "089-000-0000", vulnerableReason, "อาหาร", true, null, TestClock.DemoNow);
        db.Vendors.Add(vendor);
        if (heldLockAtCharanLastRound)
            db.StallRequests.Add(new StallRequest(Guid.NewGuid(), SeedData.CharanPreviousRound, vendor.Id,
                TestClock.DemoNow.AddMonths(-6), Category.New, StallRequestStatus.Confirmed));
        await db.CommitAsync("Test.CreateVendor", CancellationToken.None);
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
