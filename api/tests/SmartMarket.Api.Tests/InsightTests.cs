using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartMarket.Application.Common;
using SmartMarket.Infrastructure.Seed;
using static SmartMarket.Api.Tests.AllocationRunTests;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class InsightTests(ApiFactory api)
{
    private sealed record FlagDto(string Kind, string Severity, string VendorName, string LockCode, string Message);

    private static readonly Guid[] HistoryRounds = [HistorySeed.MrtPreviousRound, HistorySeed.MarketPreviousRound, HistorySeed.Rama8PreviousRound];

    [Fact]
    public async Task The_seeded_history_shows_every_kind_of_flag_at_least_once()
    {
        var flags = new List<FlagDto>();
        foreach (var round in HistoryRounds)
            flags.AddRange(await Insights(round));

        flags.Select(f => (f.Kind, f.Severity)).Should().Contain([
            ("OutsideGeofence", "Info"), ("Absent", "Warning"), ("Absent", "Critical"), ("LowCheckIn", "Warning"),
            ("SharedSurname", "Warning"), ("SharedPhone", "Warning"),
        ]);
        flags.Should().OnlyContain(f => f.VendorName.Contains('.'), "names are masked");
    }

    [Fact]
    public async Task Flags_never_act_reading_them_writes_nothing_and_nobody_is_forfeited()
    {
        var mark = await api.AuditMark();

        foreach (var round in HistoryRounds) await Insights(round);

        (await api.AuditSince(mark)).Should().BeEmpty();
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        (await db.Assignments.CountAsync(a => HistoryRounds.Contains(a.RoundId) && a.ForfeitedAt != null))
            .Should().Be(0, "even past the 14-day line, only an officer revokes");
    }

    [Fact]
    public async Task Only_officers_see_insights()
    {
        (await api.Vendor(Guid.NewGuid()).GetAsync($"/api/rounds/{HistorySeed.MrtPreviousRound}/insights"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await api.Officer().GetAsync($"/api/rounds/{Guid.NewGuid()}/insights")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<FlagDto[]> Insights(Guid roundId) =>
        (await api.Officer().GetFromJsonAsync<FlagDto[]>($"/api/rounds/{roundId}/insights", Json))!;
}
