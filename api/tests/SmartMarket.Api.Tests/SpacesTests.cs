using System.Net.Http.Json;
using FluentAssertions;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SpacesTests(ApiFactory api)
{
    private sealed record SpaceDto(Guid Id, string Name, string Type, int LockCount, string[] ProductTypes, string OpenHours);

    [Fact]
    public async Task Lists_the_four_seeded_spaces_of_bang_phlat()
    {
        var spaces = await api.CreateClient().GetFromJsonAsync<SpaceDto[]>($"/api/spaces?districtId={SeedData.BangPhlat}");

        spaces.Should().BeEquivalentTo(
            [
                new { Name = "ปากซอยจรัญฯ 79", Type = "Relaxation", LockCount = 4 },
                new { Name = "หน้า MRT บางพลัด ทางออก 1", Type = "Relaxation", LockCount = 5 },
                new { Name = "ตลาดนัดเขตบางพลัด", Type = "DistrictMarket", LockCount = 20 },
                new { Name = "ลานใต้สะพานพระราม 8", Type = "TrialArea", LockCount = 6 },
            ]);
    }

    [Fact]
    public async Task Filters_by_space_type()
    {
        var spaces = await api.CreateClient().GetFromJsonAsync<SpaceDto[]>($"/api/spaces?districtId={SeedData.BangPhlat}&type=TrialArea");

        spaces!.Select(s => s.Name).Should().Equal("ลานใต้สะพานพระราม 8");
    }
}
