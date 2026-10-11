using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Domain.StallRequests;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>. Tests that move the clock put it back.</summary>
[Collection(ApiCollection.Name)]
public sealed class ConfirmForfeitTests(ApiFactory api) : IDisposable
{
    public void Dispose() => api.Clock.Reset();

    private static readonly TimeSpan Second = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task A_winner_confirms_before_the_deadline_and_keeps_the_lock()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var mine = await api.Mine(winner.VendorId, roundId);
        var since = await api.AuditMark();

        var response = await api.Vendor(winner.VendorId).PostAsync($"/api/stall-requests/{mine.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await api.Mine(winner.VendorId, roundId);
        after.Status.Should().Be("Confirmed");
        after.LockCode.Should().Be(winner.LockCode, "the lock stays visible for check-in");
        var entry = (await api.AuditSince(since)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Round.ConfirmAssignment");
        entry.EntityType.Should().Be("Round");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Actor.Should().Be(winner.VendorId.ToString());
    }

    [Fact]
    public async Task Forfeiting_hands_the_lock_to_the_head_of_the_same_groups_waitlist()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 2, vulnerable: 3, @new: 2);
        var forfeiter = run.Outcomes.Single(o => o.Category == "Vulnerable" && o.Kind == "Won");
        var head = run.Outcomes.Single(o => o.Category == "Vulnerable" && o.WaitlistPosition == 1);
        var second = run.Outcomes.Single(o => o.Category == "Vulnerable" && o.WaitlistPosition == 2);
        var request = await api.Mine(forfeiter.VendorId, roundId);
        var since = await api.AuditMark();

        var response = await api.Vendor(forfeiter.VendorId).PostAsync($"/api/stall-requests/{request.Id}/forfeit", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.Mine(forfeiter.VendorId, roundId)).Should().Match<ApproveAllocationTests.MyRequestDto>(
            r => r.Status == "Forfeited" && r.LockCode == null && r.ConfirmBy == null);
        var promoted = await api.Mine(head.VendorId, roundId);
        promoted.Status.Should().Be("Won");
        promoted.LockCode.Should().Be(forfeiter.LockCode);
        promoted.WaitlistPosition.Should().BeNull();
        promoted.ResultReason.Should().Contain("เลื่อนจากสำรองลำดับ 1");
        promoted.ConfirmBy.Should().BeCloseTo(TestClock.DemoNow.AddDays(3), Second);
        (await api.Mine(second.VendorId, roundId)).WaitlistPosition.Should().Be(1);

        var entry = (await api.AuditSince(since)).Should().ContainSingle("forfeit and promotion are one command").Subject;
        entry.Action.Should().Be("Round.ForfeitAssignment");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Changes.Should().Contain(forfeiter.VendorId.ToString()).And.Contain(head.VendorId.ToString());
    }

    [Fact]
    public async Task A_group_without_a_waitlist_promotes_along_the_rollover_cycle()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 2, vulnerable: 1, @new: 5);
        var forfeiter = run.Outcomes.Single(o => o.Category == "Vulnerable");
        var newHead = run.Outcomes.Single(o => o.Category == "New" && o.WaitlistPosition == 1);
        var request = await api.Mine(forfeiter.VendorId, roundId);

        (await api.Vendor(forfeiter.VendorId).PostAsync($"/api/stall-requests/{request.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var promoted = await api.Mine(newHead.VendorId, roundId);
        promoted.Status.Should().Be("Won");
        promoted.LockCode.Should().Be(forfeiter.LockCode);
    }

    [Fact]
    public async Task Missing_the_deadline_forfeits_on_the_next_read_and_promotes_the_waitlist()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 2, vulnerable: 2, @new: 5);
        var winners = run.Outcomes.Where(o => o.Kind == "Won").ToList();
        var waitlisted = run.Outcomes.Where(o => o.Kind == "Waitlisted").ToList();
        var since = await api.AuditMark();
        var later = TestClock.DemoNow.AddDays(3).AddMinutes(5);
        api.Clock.Set(later);

        var first = await api.Mine(winners[0].VendorId, roundId);

        first.Status.Should().Be("Forfeited");
        first.ResultReason.Should().Be("ไม่ยืนยันสิทธิ์ภายในกำหนด");
        foreach (var w in winners)
            (await api.Mine(w.VendorId, roundId)).Status.Should().Be("Forfeited");
        var promoted = new List<ApproveAllocationTests.MyRequestDto>();
        foreach (var w in waitlisted)
            promoted.Add(await api.Mine(w.VendorId, roundId));
        promoted.Where(p => p.Status == "Won").Select(p => p.LockCode)
            .Should().BeEquivalentTo(winners.Select(w => w.LockCode), "every freed lock goes to someone waiting");
        promoted.Where(p => p.Status == "Won")
            .Should().OnlyContain(p => p.ConfirmBy!.Value > later, "a promoted vendor gets a fresh deadline");

        var expiries = await api.AuditSince(since);
        expiries.Should().HaveCount(winners.Count, "one entry per expired assignment");
        expiries.Should().OnlyContain(e => e.Action == "Round.ExpireAssignment" && e.Actor == "system");
    }

    [Fact]
    public async Task A_late_confirm_is_refused_because_the_lock_has_already_moved_on()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var request = await api.Mine(winner.VendorId, roundId);
        api.Clock.Set(TestClock.DemoNow.AddDays(3).AddMinutes(1));

        var response = await api.Vendor(winner.VendorId).PostAsync($"/api/stall-requests/{request.Id}/confirm", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await api.Mine(winner.VendorId, roundId)).Status.Should().Be("Forfeited");
    }

    [Fact]
    public async Task Only_a_winner_who_has_not_confirmed_can_confirm_or_forfeit()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 2, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var waiting = run.Outcomes.First(o => o.Kind == "Waitlisted");
        var winnerRequest = await api.Mine(winner.VendorId, roundId);
        var waitingRequest = await api.Mine(waiting.VendorId, roundId);

        (await api.Vendor(waiting.VendorId).PostAsync($"/api/stall-requests/{waitingRequest.Id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await api.Vendor(waiting.VendorId).PostAsync($"/api/stall-requests/{waitingRequest.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await api.Vendor(winner.VendorId).PostAsync($"/api/stall-requests/{winnerRequest.Id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await api.Vendor(winner.VendorId).PostAsync($"/api/stall-requests/{winnerRequest.Id}/confirm", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await api.Vendor(winner.VendorId).PostAsync($"/api/stall-requests/{winnerRequest.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "once confirmed, giving up the lock goes through the district office");
    }

    [Fact]
    public async Task A_vendor_cannot_act_on_someone_elses_request()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 1, vulnerable: 1, @new: 2);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var other = run.Outcomes.First(o => o.VendorId != winner.VendorId);
        var request = await api.Mine(winner.VendorId, roundId);

        (await api.Vendor(other.VendorId).PostAsync($"/api/stall-requests/{request.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await api.Officer().PostAsync($"/api/stall-requests/{request.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_original_draw_stays_replayable_after_a_forfeit()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 2, vulnerable: 3, @new: 2);
        var forfeiter = run.Outcomes.First(o => o.Kind == "Won");
        var request = await api.Mine(forfeiter.VendorId, roundId);
        (await api.Vendor(forfeiter.VendorId).PostAsync($"/api/stall-requests/{request.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var stored = await db.AllocationRuns.SingleAsync(r => r.Id == run.Id);
        var drawn = JsonSerializer.Deserialize<AllocationResult>(stored.ResultJson, Json)!;
        drawn.Outcomes.Single(o => o.VendorId == forfeiter.VendorId).Kind.Should().Be(OutcomeKind.Won, "the draw record is immutable");
        var current = JsonSerializer.Deserialize<AllocationResult>(stored.CurrentResultJson, Json)!;
        current.Outcomes.Single(o => o.VendorId == forfeiter.VendorId).Kind.Should().Be(OutcomeKind.Forfeited);
        (await db.StallRequests.SingleAsync(q => q.RoundId == roundId && q.VendorId == forfeiter.VendorId))
            .Status.Should().Be(StallRequestStatus.Forfeited);
    }

    [Fact]
    public async Task The_demo_clock_can_be_moved_in_development()
    {
        var target = TestClock.DemoNow.AddDays(4);

        var response = await api.Officer().PostAsJsonAsync("/api/demo/clock", new { now = target });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        api.Clock.Now.Should().BeCloseTo(target, Second);
        (await api.Officer().GetFromJsonAsync<JsonElement>("/api/demo/clock")).GetProperty("now").GetDateTimeOffset()
            .Should().BeCloseTo(target, Second);
        (await api.Vendor(Guid.NewGuid()).PostAsJsonAsync("/api/demo/clock", new { now = target }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
