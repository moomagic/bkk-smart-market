using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Domain.Vendors;

namespace SmartMarket.Api.Tests;

/// <summary>
/// ADR-0003's one-request rule under concurrent submits (#26). A race is timing-dependent, so each test fires the pair
/// for many fresh vendors at once; without the per-vendor lock some pair would get two 201s or a 500.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrentSubmitTests(ApiFactory api)
{
    private const int Attempts = 12;

    [Fact]
    public async Task Two_submits_to_different_overlapping_rounds_at_once_let_exactly_one_through()
    {
        var first = await api.OpenRound();
        var second = await api.OpenRound();
        var vendors = await NewVendors(Attempts);
        var mark = await api.AuditMark();

        var results = await Task.WhenAll(vendors.Select(async v =>
            await Task.WhenAll(Submit(v, first), Submit(v, second))));

        foreach (var pair in results)
            pair.Select(r => r.Status).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        results.SelectMany(p => p).Where(r => r.Status == HttpStatusCode.Conflict)
            .Should().OnlyContain(r => r.Code == "request.already_open");
        var submits = (await api.AuditSince(mark)).Where(e => e.Action == "StallRequest.Submit").ToList();
        submits.Should().HaveCount(Attempts, "one audit entry per successful submit, none for the refused one");
    }

    [Fact]
    public async Task Two_submits_to_the_same_round_at_once_give_one_201_and_one_409_never_a_500()
    {
        var round = await api.OpenRound();
        var vendors = await NewVendors(Attempts);
        var mark = await api.AuditMark();

        var results = await Task.WhenAll(vendors.Select(async v =>
            await Task.WhenAll(Submit(v, round), Submit(v, round))));

        foreach (var pair in results)
            pair.Select(r => r.Status).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.StallRequests.CountAsync(q => q.RoundId == round)).Should().Be(Attempts);
        (await api.AuditSince(mark)).Count(e => e.Action == "StallRequest.Submit").Should().Be(Attempts, "none for the refused submit");
    }

    private sealed record Outcome(HttpStatusCode Status, string? Code);

    private async Task<Outcome> Submit(Guid vendorId, Guid roundId)
    {
        var response = await api.Vendor(vendorId).PostAsJsonAsync("/api/stall-requests", new { roundId });
        var code = response.StatusCode == HttpStatusCode.Created ? null
            : (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("code", out var c) ? c.GetString() : null;
        return new Outcome(response.StatusCode, code);
    }

    private async Task<List<Guid>> NewVendors(int count)
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var vendors = Enumerable.Range(0, count)
            .Select(_ => new Vendor(Guid.NewGuid(), "ผู้ค้า พร้อมกัน", "089-000-2600", null, "ทั่วไป", true, null, TestClock.DemoNow))
            .ToList();
        db.Vendors.AddRange(vendors);
        await db.CommitAsync("Test.Arrange", CancellationToken.None);
        return [.. vendors.Select(v => v.Id)];
    }
}
