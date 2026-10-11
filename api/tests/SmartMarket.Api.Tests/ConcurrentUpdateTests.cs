using System.Net;
using FluentAssertions;

namespace SmartMarket.Api.Tests;

/// <summary>Commands other than submit that lose a race on a unique index answer 409 "try again", never 500 (#26).</summary>
[Collection(ApiCollection.Name)]
public sealed class ConcurrentUpdateTests(ApiFactory api)
{
    [Fact]
    public async Task Any_other_command_losing_a_race_answers_409_not_500()
    {
        var round = await api.CheckedRound(legacy: 1, vulnerable: 1, @new: 1);

        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.Officer().PostAsync($"/api/rounds/{round}/allocation-runs", null)));

        runs.Select(r => r.StatusCode).Should().Contain(HttpStatusCode.Created)
            .And.NotContain(HttpStatusCode.InternalServerError, "a second draft at the same moment hits the one-draft index");
        runs.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
    }

}
