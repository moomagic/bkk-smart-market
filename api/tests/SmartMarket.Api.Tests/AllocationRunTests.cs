using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Allocation;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Rounds;
using SmartMarket.Domain.Spaces;
using Lock = SmartMarket.Domain.Spaces.Lock;
using SmartMarket.Domain.StallRequests;
using SmartMarket.Domain.Vendors;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

/// <summary>Every test draws a round of its own — see <see cref="AllocationArrange"/>.</summary>
[Collection(ApiCollection.Name)]
public sealed class AllocationRunTests(ApiFactory api)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private HttpClient Officer => api.Officer();

    [Fact]
    public async Task Officer_runs_a_draft_with_quota_ref_seed_and_a_reason_per_vendor()
    {
        var roundId = await api.CheckedRound(legacy: 2, vulnerable: 3, @new: 5);

        var response = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var run = (await response.Content.ReadFromJsonAsync<RunDto>(Json))!;
        response.Headers.Location!.ToString().Should().Be($"/api/allocation-runs/{run.Id}");
        run.Status.Should().Be("Draft");
        run.ReferenceNo.Should().MatchRegex(@"^BPL-2569-1025-\d{2}$", "Buddhist year and Bangkok date of the run");
        run.InputHash.Should().MatchRegex("^[0-9a-f]{64}$");
        run.RanBy.Should().Be("officer-bangphlat");
        run.Rules.Should().Be(new RulesSnapshot(0.50m, 0.25m, 0.25m, 3));
        run.Quota.Select(q => (q.Category, q.Base, q.Allocated))
            .Should().Equal(("Legacy", 2, 2), ("Vulnerable", 1, 1), ("New", 1, 1));
        run.Outcomes.Should().HaveCount(10);
        run.Outcomes.Should().OnlyContain(o => o.Reason.Length > 0 && o.VendorName.EndsWith('.'));
        run.Outcomes.Where(o => o.Kind == "Won").Select(o => o.LockCode).Should().BeEquivalentTo(["B1", "B2", "B3", "B4"]);

        (await Officer.GetFromJsonAsync<RunDto>($"/api/allocation-runs/{run.Id}", Json))
            .Should().BeEquivalentTo(run);
        (await Officer.GetFromJsonAsync<RunDto[]>($"/api/rounds/{roundId}/allocation-runs", Json))
            .Should().ContainSingle().Which.Id.Should().Be(run.Id);
    }

    [Fact]
    public async Task A_draft_draws_the_round_but_does_not_decide_any_request()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 2);

        (await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null)).StatusCode.Should().Be(HttpStatusCode.Created);

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.Rounds.SingleAsync(r => r.Id == roundId)).Status.Should().Be(RoundStatus.Drawn);
        (await db.StallRequests.Where(q => q.RoundId == roundId).Select(q => q.Status).ToListAsync())
            .Should().OnlyContain(s => s == StallRequestStatus.Eligible, "results reach requests only on approval (#15)");
    }

    [Fact]
    public async Task Re_running_from_the_stored_seed_and_rules_gives_the_same_result()
    {
        var roundId = await api.CheckedRound(legacy: 2, vulnerable: 2, @new: 4);
        var run = (await (await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null))
            .Content.ReadFromJsonAsync<RunDto>(Json))!;

        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var stored = await db.AllocationRuns.SingleAsync(r => r.Id == run.Id);
        // Only what the run itself stores: request statuses move on after approval, the run does not.
        var rules = JsonSerializer.Deserialize<RulesSnapshot>(stored.RulesSnapshot, Json)!;
        var result = JsonSerializer.Deserialize<AllocationResult>(stored.ResultJson, Json)!;
        var locks = result.Outcomes.Select(o => o.LockCode).OfType<string>().Concat(result.UnassignedLockCodes).ToList();
        var candidates = result.Outcomes.Select(o => new Candidate(o.VendorId, o.Category, o.Weight, Eligible: true)).ToList();

        var replay = AllocationEngine.Allocate(new AllocationInput(locks, candidates, stored.Seed, rules));

        replay.InputHash.Should().Be(stored.InputHash);
        result.Should().BeEquivalentTo(replay, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task One_audit_entry_per_run_filed_under_the_round()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
        var since = await api.AuditMark();

        (await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null)).StatusCode.Should().Be(HttpStatusCode.Created);

        var entry = (await api.AuditSince(since)).Should().ContainSingle().Subject;
        entry.Action.Should().Be("Round.RunAllocation");
        entry.EntityType.Should().Be("Round");
        entry.EntityId.Should().Be(roundId.ToString());
        entry.Actor.Should().Be("officer-bangphlat");
        entry.Changes.Should().Contain("AllocationRun").And.Contain("Drawn");
    }

    [Fact]
    public async Task The_seed_comes_from_the_system_whatever_the_officer_sends()
    {
        var seeds = new List<int>();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
            var response = await Officer.PostAsJsonAsync($"/api/rounds/{roundId}/allocation-runs", new { seed = 42 });
            seeds.Add((await response.Content.ReadFromJsonAsync<RunDto>(Json))!.Seed);
        }

        seeds.Should().NotContain(42).And.OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Requests_not_yet_checked_block_the_run()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
        await api.AddRequest(roundId, Category.New, StallRequestStatus.Submitted);

        var response = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("ยังไม่ได้ตรวจสิทธิ 1 คำขอ");
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.AllocationRuns.AnyAsync(r => r.RoundId == roundId)).Should().BeFalse();
    }

    [Fact]
    public async Task A_round_with_a_draft_cannot_be_run_again()
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
        (await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null)).StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(again)).Should().Contain("ร่าง");
    }

    [Fact]
    public async Task A_round_without_eligible_requests_cannot_be_run()
    {
        var roundId = await api.CheckedRound(legacy: 0, vulnerable: 0, @new: 0);

        var response = await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Detail(response)).Should().Contain("ไม่มีคำขอที่ผ่านการตรวจสิทธิ");
    }

    [Fact]
    public async Task Reference_numbers_are_unique_across_the_district_on_the_same_day()
    {
        var refs = new List<string>();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);
            refs.Add((await (await Officer.PostAsync($"/api/rounds/{roundId}/allocation-runs", null))
                .Content.ReadFromJsonAsync<RunDto>(Json))!.ReferenceNo);
        }

        refs.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData("vendor", HttpStatusCode.Forbidden)]
    [InlineData("executive", HttpStatusCode.Created)]
    public async Task Only_officers_and_executives_run_allocations(string role, HttpStatusCode expected)
    {
        var roundId = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);

        (await api.ClientAs(role, "someone").PostAsync($"/api/rounds/{roundId}/allocation-runs", null))
            .StatusCode.Should().Be(expected);
    }

    [Fact]
    public async Task Unknown_round_and_run_are_not_found()
    {
        (await Officer.PostAsync($"/api/rounds/{Guid.NewGuid()}/allocation-runs", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Officer.GetAsync($"/api/allocation-runs/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    internal sealed record QuotaDto(string Category, int Base, int Allocated);
    internal sealed record OutcomeDto(Guid VendorId, string VendorName, string Category, int Weight, string Kind, string? LockCode,
        int? WaitlistPosition, bool ViaRollover, string Reason);
    internal sealed record RunDto(Guid Id, Guid RoundId, string SpaceName, string ReferenceNo, int Seed, string InputHash, string Status,
        DateTimeOffset RanAt, string RanBy, RulesSnapshot Rules, bool PooledDraw, QuotaDto[] Quota, OutcomeDto[] Outcomes,
        string[] UnassignedLockCodes);

    private static async Task<string> Detail(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!;
}
