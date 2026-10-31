using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

/// <summary>Replay verification (CONTEXT.md): draw the run again from what it stores and compare with its draw record.</summary>
[Collection(ApiCollection.Name)]
public sealed class VerifyAllocationRunTests(ApiFactory api)
{
    internal sealed record MismatchDto(string? VendorName, string Field, string? Recorded, string? Replayed);
    internal sealed record VerificationDto(Guid RunId, string ReferenceNo, int Seed, bool InputHashMatches, bool ResultMatches, MismatchDto[] Mismatches);

    private HttpClient Officer => api.Officer();

    [Fact]
    public async Task A_fresh_draft_replays_to_the_same_result_and_hash()
    {
        var roundId = await api.CheckedRound(legacy: 2, vulnerable: 2, @new: 4);
        var run = await Draw(roundId);

        var check = await Verify(run.Id);

        check.Should().BeEquivalentTo(new VerificationDto(run.Id, run.ReferenceNo, run.Seed, true, true, []));
    }

    [Fact]
    public async Task An_approved_run_still_matches_after_a_forfeit_moved_the_waitlist_up()
    {
        var (roundId, run) = await api.ApprovedRound(legacy: 2, vulnerable: 2, @new: 2);
        var forfeiter = run.Outcomes.Single(o => o.Category == "Vulnerable" && o.Kind == "Won");
        var request = await api.Mine(forfeiter.VendorId, roundId);
        (await api.Vendor(forfeiter.VendorId).PostAsync($"/api/stall-requests/{request.Id}/forfeit", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var check = await Verify(run.Id);

        check.ResultMatches.Should().BeTrue("the draw record is compared, not the result after promotions");
        check.InputHashMatches.Should().BeTrue();
    }

    [Fact]
    public async Task A_rejected_run_can_be_verified_too()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 2);
        var run = await Draw(roundId);
        (await Officer.PostAsJsonAsync($"/api/allocation-runs/{run.Id}/reject", new { reason = "ทดสอบตรวจซ้ำ" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await Verify(run.Id)).Should().Match<VerificationDto>(v => v.ResultMatches && v.InputHashMatches);
    }

    [Fact]
    public async Task Swapping_two_winners_locks_in_the_record_shows_up_against_each_of_them()
    {
        var roundId = await api.CheckedRound(legacy: 2, vulnerable: 1, @new: 1);
        var run = await Draw(roundId);
        var (a, b) = (run.Outcomes[0], run.Outcomes[1]);
        await Tamper(run.Id, r => r with
        {
            Outcomes = [.. r.Outcomes.Select(o =>
                o.VendorId == a.VendorId ? o with { LockCode = b.LockCode } :
                o.VendorId == b.VendorId ? o with { LockCode = a.LockCode } : o)],
        });

        var check = await Verify(run.Id);

        check.InputHashMatches.Should().BeTrue("who was drawn, their group and weight are untouched");
        check.ResultMatches.Should().BeFalse();
        check.Mismatches.Should().BeEquivalentTo([
            new MismatchDto(a.VendorName, "lockCode", b.LockCode, a.LockCode),
            new MismatchDto(b.VendorName, "lockCode", a.LockCode, b.LockCode),
        ]);
    }

    [Fact]
    public async Task Changing_a_vendors_weight_in_the_record_breaks_the_input_hash()
    {
        var roundId = await api.CheckedRound(legacy: 2, vulnerable: 1, @new: 3);
        var run = await Draw(roundId);
        var target = run.Outcomes.Last().VendorId;
        await Tamper(run.Id, r => r with { Outcomes = [.. r.Outcomes.Select(o => o.VendorId == target ? o with { Weight = 2 } : o)] });

        var check = await Verify(run.Id);

        check.InputHashMatches.Should().BeFalse();
        check.ResultMatches.Should().BeFalse();
    }

    [Fact]
    public async Task A_one_lock_pooled_draw_also_compares_its_pooled_order()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 2);
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var spaceId = await db.Rounds.Where(r => r.Id == roundId).Select(r => r.SpaceId).SingleAsync();
            await db.Locks.Where(l => l.SpaceId == spaceId && l.Code != "B1").ExecuteDeleteAsync();
        }
        var run = await Draw(roundId);
        run.PooledDraw.Should().BeTrue();
        (await Verify(run.Id)).ResultMatches.Should().BeTrue();

        await Tamper(run.Id, r => r with { PooledOrder = [.. r.PooledOrder.Reverse()] });

        (await Verify(run.Id)).Mismatches.Should().Contain(m => m.Field == "pooledOrder");
    }

    [Fact]
    public async Task Verifying_writes_no_audit_entry_and_an_unknown_run_is_not_found()
    {
        var run = await Draw(await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1));
        var since = await api.AuditMark();

        await Verify(run.Id);

        (await api.AuditSince(since)).Should().BeEmpty();
        (await Officer.GetAsync($"/api/allocation-runs/{Guid.NewGuid()}/verify")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_officers_verify_for_now()
    {
        var run = await Draw(await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1));
        var vendor = run.Outcomes[0].VendorId;

        (await api.Vendor(vendor).GetAsync($"/api/allocation-runs/{run.Id}/verify")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.ClientAs("executive", "executive-bkk").GetAsync($"/api/allocation-runs/{run.Id}/verify"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    internal static async Task<VerificationDto> Verify(HttpClient client, Guid runId)
    {
        var response = await client.GetAsync($"/api/allocation-runs/{runId}/verify");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<VerificationDto>(Json))!;
    }

    private Task<VerificationDto> Verify(Guid runId) => Verify(Officer, runId);

    private async Task<RunDto> Draw(Guid roundId)
    {
        var response = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
    }

    /// <summary>Rewrites the stored draw record behind the API's back, as someone editing the database would.</summary>
    private async Task Tamper(Guid runId, Func<AllocationResult, AllocationResult> change)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var stored = await db.AllocationRuns.Where(r => r.Id == runId).Select(r => r.ResultJson).SingleAsync();
        var json = AllocationJson.Serialize(change(AllocationJson.Deserialize<AllocationResult>(stored)));
        await db.AllocationRuns.Where(r => r.Id == runId).ExecuteUpdateAsync(s => s.SetProperty(r => r.ResultJson, json));
    }
}
