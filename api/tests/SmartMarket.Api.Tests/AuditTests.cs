using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Audit;
using SmartMarket.Infrastructure.Persistence;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class AuditTests(ApiFactory api)
{
    [Fact]
    public async Task A_command_writes_one_audit_entry_with_personal_data_masked()
    {
        var vendorId = SeedData.VendorId(9);
        var since = await LatestAuditAt();

        await Command("Vendor.UpdateContact", async db =>
        {
            var vendor = await db.Vendors.SingleAsync(v => v.Id == vendorId);
            vendor.UpdateContact("ทดสอบ สมมุติ", "089-999-0009");
        });

        var entry = (await AuditSince(since)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Vendor.UpdateContact");
        entry.EntityType.Should().Be("Vendor");
        entry.EntityId.Should().Be(vendorId.ToString());
        entry.Actor.Should().Be("system");
        var change = JsonDocument.Parse(entry.Changes).RootElement.EnumerateArray().Should().ContainSingle().Subject;
        foreach (var side in new[] { "Before", "After" })
        {
            change.GetProperty(side).GetProperty("DisplayName").GetString().Should().Be("***");
            change.GetProperty(side).GetProperty("Phone").GetString().Should().Be("***");
        }
        change.GetProperty("Before").GetProperty("MaskedName").GetString().Should().Be("กิตติ ว.");
        change.GetProperty("After").GetProperty("MaskedName").GetString().Should().Be("ทดสอบ ส.");
        entry.Changes.Should().NotContain("ทดสอบ สมมุติ").And.NotContain("089-999-0009")
            .And.NotContain("กิตติ วงศ์ใหญ่").And.NotContain("080-000-0009");
    }

    [Fact]
    public async Task A_command_touching_several_entities_still_writes_one_entry()
    {
        var since = await LatestAuditAt();

        await Command("Vendor.BulkUpdate", async db =>
        {
            foreach (var n in new[] { 10, 11 })
            {
                var id = SeedData.VendorId(n);
                var vendor = await db.Vendors.SingleAsync(v => v.Id == id);
                vendor.UpdateContact($"ผู้ค้า ทดสอบ{n}", $"089-999-00{n}");
            }
        });

        var entry = (await AuditSince(since)).Should().ContainSingle().Subject;
        entry.Changes.Should().Contain(SeedData.VendorId(10).ToString()).And.Contain(SeedData.VendorId(11).ToString());
    }

    [Fact]
    public async Task A_command_that_changes_nothing_writes_no_entry()
    {
        var since = await LatestAuditAt();

        await Command("Vendor.Noop", db => db.Vendors.FirstAsync());

        (await AuditSince(since)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_bare_save_is_still_audited_as_unscoped()
    {
        var since = await LatestAuditAt();

        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartMarketDbContext>();
            var id = SeedData.VendorId(8);
            (await db.Vendors.SingleAsync(v => v.Id == id)).UpdateContact("ผู้ค้า ไม่ระบุ", "089-999-0008");
            await db.SaveChangesAsync();
        }

        (await AuditSince(since)).Should().ContainSingle().Which.Action.Should().Be("Unscoped");
    }

    [Fact]
    public async Task Personal_data_never_reaches_the_application_log()
    {
        await Command("Vendor.UpdateContact", async db =>
        {
            var id = SeedData.VendorId(7);
            (await db.Vendors.SingleAsync(v => v.Id == id)).UpdateContact("ลับมาก ห้ามหลุด", "089-777-7777");
        });
        await api.CreateClient().GetAsync("/api/spaces");

        var personal = SeedData.Vendors.SelectMany(v => new[] { v.DisplayName, v.Phone })
            .Append("ลับมาก ห้ามหลุด").Append("089-777-7777");
        api.Logs.Should().NotBeEmpty();
        foreach (var value in personal)
            api.Logs.Should().NotContain(line => line.Contains(value), $"'{value}' is personal data");
    }

    private async Task Command(string action, Func<IAppDbContext, Task> work)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        await work(db);
        await db.CommitAsync(action, CancellationToken.None);
    }

    private async Task<DateTimeOffset> LatestAuditAt()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.AuditLogs.MaxAsync(a => (DateTimeOffset?)a.At) ?? DateTimeOffset.MinValue;
    }

    private async Task<List<AuditLog>> AuditSince(DateTimeOffset since)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        return await db.AuditLogs.Where(a => a.At > since).ToListAsync();
    }
}
