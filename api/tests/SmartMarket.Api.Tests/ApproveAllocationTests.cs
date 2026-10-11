using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.StallRequests;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>.</summary>
[Collection(ApiCollection.Name)]
public sealed class ApproveAllocationTests(ApiFactory api)
{
    internal sealed record RunRefDto(string ReferenceNo, string Status, DateTimeOffset RanAt);
    internal sealed record MyRequestDto(Guid Id, Guid RoundId, Guid SpaceId, string Status, int? WaitlistPosition, string? LockCode,
        string? ResultReason, string? ReferenceNo, DateTimeOffset? ConfirmBy, RunRefDto[] Runs, DateTimeOffset? LastCheckInAt = null);

    private HttpClient Officer => api.Officer();

    [Fact]
    public async Task Approving_a_draft_decides_every_request_and_assigns_locks_to_winners()
    {
        var (roundId, run) = await DraftRun(legacy: 2, vulnerable: 3, @new: 5);

        var response = await Officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        approved.Status.Should().Be("Approved");

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.Rounds.SingleAsync(r => r.Id == roundId)).Status.Should().Be(RoundStatus.Approved);
        var stored = await db.AllocationRuns.SingleAsync(r => r.Id == run.Id);
        stored.DecidedBy.Should().Be("officer-bangphlat");
        stored.DecidedAt.Should().BeCloseTo(TestClock.DemoNow, TimeSpan.FromSeconds(1));

        var requests = await db.StallRequests.Where(q => q.RoundId == roundId).ToDictionaryAsync(q => q.VendorId);
        foreach (var o in run.Outcomes)
        {
            requests[o.VendorId].Status.ToString().Should().Be(o.Kind);
            requests[o.VendorId].WaitlistPosition.Should().Be(o.WaitlistPosition);
        }

        var assignments = await (
            from a in db.Assignments
            join l in db.Locks on a.LockId equals l.Id
            where a.RoundId == roundId
            select new { a.VendorId, l.Code, a.ConfirmBy, a.ConfirmedAt }).ToListAsync();
        assignments.Select(a => (a.VendorId, a.Code)).Should().BeEquivalentTo(
            run.Outcomes.Where(o => o.Kind == "Won").Select(o => (o.VendorId, o.LockCode!)));
        assignments.Should().OnlyContain(a => a.ConfirmedAt == null);
        assignments.Should().OnlyContain(a => Math.Abs((a.ConfirmBy - TestClock.DemoNow.AddDays(3)).TotalSeconds) < 1,
            "a winner confirms within 3 days of approval");
    }

    [Fact]
    public async Task Vendors_see_their_outcome_with_reason_and_reference_only_after_approval()
    {
        var (roundId, run) = await DraftRun(legacy: 1, vulnerable: 2, @new: 5);
        var winner = run.Outcomes.First(o => o.Kind == "Won");
        var waitlisted = run.Outcomes.First(o => o.Kind == "Waitlisted" && o.WaitlistPosition == 1);

        var draft = await api.Mine(winner.VendorId, roundId);
        draft.Should().Match<MyRequestDto>(r => r.Status == "Eligible" && r.LockCode == null && r.ResultReason == null && r.ReferenceNo == null);
        draft.Runs.Should().ContainSingle().Which.Should().Match<RunRefDto>(r => r.ReferenceNo == run.ReferenceNo && r.Status == "Draft");

        (await Officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var won = await api.Mine(winner.VendorId, roundId);
        won.Status.Should().Be("Won");
        won.LockCode.Should().Be(winner.LockCode);
        won.ResultReason.Should().Be(winner.Reason);
        won.ReferenceNo.Should().Be(run.ReferenceNo);
        won.ConfirmBy.Should().BeCloseTo(TestClock.DemoNow.AddDays(3), TimeSpan.FromSeconds(1));
        won.Runs.Should().ContainSingle().Which.Status.Should().Be("Approved");

        var queued = await api.Mine(waitlisted.VendorId, roundId);
        queued.Should().Match<MyRequestDto>(r => r.Status == "Waitlisted" && r.WaitlistPosition == 1 && r.LockCode == null && r.ConfirmBy == null);
        queued.ResultReason.Should().StartWith("สำรองลำดับ 1 · กลุ่ม");
    }

    [Fact]
    public async Task One_audit_entry_per_approval_filed_under_the_round()
    {
        var (roundId, run) = await DraftRun(legacy: 1, vulnerable: 1, @new: 2);
        var since = await api.AuditMark();

        (await Officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var entry = (await api.AuditSince(since)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Round.ApproveAllocation");
        entry.EntityType.Should().Be("Round");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Changes.Should().Contain("Assignment").And.Contain("\"Won\"").And.Contain("\"Approved\"");
    }

    [Fact]
    public async Task Only_a_draft_can_be_approved()
    {
        var (_, run) = await DraftRun(legacy: 1, vulnerable: 1, @new: 1);
        (await Officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var again = await Officer.PostAsync($"/api/allocation-runs/{run.Id}/approve", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("vendor", HttpStatusCode.Forbidden)]
    [InlineData("executive", HttpStatusCode.OK)]
    public async Task Only_officers_and_executives_approve(string role, HttpStatusCode expected)
    {
        var (_, run) = await DraftRun(legacy: 1, vulnerable: 1, @new: 1);

        (await api.ClientAs(role, "someone").PostAsync($"/api/allocation-runs/{run.Id}/approve", null))
            .StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Unknown_run_is_not_found()
    {
        (await Officer.PostAsync($"/api/allocation-runs/{Guid.NewGuid()}/approve", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<(Guid RoundId, RunDto Run)> DraftRun(int legacy, int vulnerable, int @new)
    {
        var roundId = await api.CheckedRound(legacy, vulnerable, @new);
        var response = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (roundId, (await response.Content.ReadFromJsonAsync<RunDto>(Json))!);
    }
}
