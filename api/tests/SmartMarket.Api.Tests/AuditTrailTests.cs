using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>.</summary>
[Collection(ApiCollection.Name)]
public sealed class AuditTrailTests(ApiFactory api)
{
    private sealed record ChangeDto(string Entity, string Id, string State, JsonElement? Before, JsonElement? After);
    private sealed record EntryDto(Guid Id, DateTimeOffset At, string Actor, string ActorName, string ActorRole, string Action,
        string EntityType, string EntityId, ChangeDto[] Changes);

    [Fact]
    public async Task A_rounds_timeline_shows_every_step_from_submission_to_approval_in_order()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
        var vendorId = await NewVendor("ผู้ยื่น สด", "089-321-0001");
        (await api.Vendor(vendorId).PostAsJsonAsync("/api/stall-requests", new { roundId }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await api.Officer().PostAsync($"/api/rounds/{roundId}/eligibility", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var run = (await (await api.Officer().PostAsync($"/api/rounds/{roundId}/allocation-runs", null))
            .Content.ReadFromJsonAsync<AllocationRunTests.RunDto>(AllocationRunTests.Json))!;
        (await api.Officer().PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var timeline = await Trail($"entityType=Round&entityId={roundId}");

        timeline.Select(e => e.Action).Where(a => !a.StartsWith("Test.")).Should().ContainInOrder(
            "Round.CheckEligibility", "StallRequest.Submit", "Round.CheckEligibility", "Round.RunAllocation", "Round.ApproveAllocation");
        timeline.Should().BeInAscendingOrder(e => e.At);
        var submit = timeline.Single(e => e.Action == "StallRequest.Submit");
        submit.ActorName.Should().Be("ผู้ยื่น ส.", "a vendor actor is shown by masked name");
        submit.ActorRole.Should().Be("vendor");
        timeline.Single(e => e.Action == "Round.ApproveAllocation").Changes
            .Should().Contain(c => c.Entity == "StallRequest" && c.State == "Modified");
    }

    [Fact]
    public async Task Filtering_by_entity_also_finds_commands_that_touched_it_as_a_secondary_entity()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 1);
        var request = await api.Mine(run.Outcomes[0].VendorId, roundId);

        var timeline = await Trail($"entityType=StallRequest&entityId={request.Id}");

        timeline.Select(e => e.Action).Should().Contain(["Round.CheckEligibility", "Round.ApproveAllocation"]);
        timeline.Should().OnlyContain(e => e.Changes.Any(c => c.Entity == "StallRequest" && c.Id == request.Id.ToString())
                                           || e.EntityId == request.Id.ToString());
    }

    [Fact]
    public async Task Personal_data_never_appears_raw_in_the_trail()
    {
        const string name = "ลับเฉพาะ ห้ามเห็น";
        const string phone = "089-654-3210";
        var vendorId = await NewVendor(name, phone);

        var response = await api.Officer().GetAsync($"/api/audit?entityType=Vendor&entityId={vendorId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(name).And.NotContain(phone).And.Contain("***").And.Contain("ลับเฉพาะ ห.");
    }

    [Fact]
    public async Task Filtering_by_entity_type_alone_lists_that_kinds_entries()
    {
        var timeline = await Trail("entityType=AllocationRun");

        timeline.Should().OnlyContain(e => e.EntityType == "AllocationRun" || e.Changes.Any(c => c.Entity == "AllocationRun"));
    }

    [Fact]
    public async Task Entity_type_is_required_and_only_officers_read_the_trail()
    {
        (await api.Officer().GetAsync("/api/audit")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await api.Vendor(Guid.NewGuid()).GetAsync("/api/audit?entityType=Round")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.ClientAs("executive", "exec").GetAsync("/api/audit?entityType=Round")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<EntryDto[]> Trail(string query) =>
        (await api.Officer().GetFromJsonAsync<EntryDto[]>($"/api/audit?{query}"))!;

    private async Task<Guid> NewVendor(string name, string phone)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var vendor = new Vendor(Guid.NewGuid(), name, phone, null, "ทั่วไป", true, null, TestClock.DemoNow);
        db.Vendors.Add(vendor);
        await db.CommitAsync("Test.Arrange", CancellationToken.None);
        return vendor.Id;
    }
}
