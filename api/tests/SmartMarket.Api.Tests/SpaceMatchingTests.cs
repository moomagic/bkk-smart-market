using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SmartMarket.Infrastructure.Seed;

namespace SmartMarket.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class SpaceMatchingTests(ApiFactory api)
{
    private sealed record PartsDto(int Product, int FreeLock, int Area, int Hours);
    private sealed record MatchDto(Guid SpaceId, string Name, int Score, PartsDto Parts, string[] Reasons);

    [Fact]
    public async Task Khao_kaeng_near_charan_puts_pak_soi_charan_79_first_with_reasons()
    {
        var matches = await Match(new { productType = "ข้าวแกง", preferredArea = "ย่านจรัญฯ" });

        matches.Should().HaveCount(3);
        var top = matches[0];
        top.SpaceId.Should().Be(SeedData.PakSoiCharan79);
        top.Parts.Should().Be(new PartsDto(40, 30, 20, 0));
        top.Score.Should().Be(90);
        top.Reasons.Should().HaveCount(3);
        top.Reasons[0].Should().Contain("อาหาร");
        top.Reasons[1].Should().Contain("เปิดรับคำขอ");
        top.Reasons[2].Should().Contain("จรัญ");
    }

    [Fact]
    public async Task Each_part_of_the_score_shows_up_in_the_api()
    {
        var coffee = await Match(new { productType = "กาแฟโบราณ", preferredArea = "MRT", preferredTime = "Morning" });
        coffee[0].SpaceId.Should().Be(SeedData.MrtBangPhlatExit1);
        coffee[0].Parts.Should().Be(new PartsDto(40, 30, 20, 10));

        var clothes = await Match(new { productType = "เสื้อผ้ามือสอง", preferredArea = "พระราม 8" });
        clothes[0].SpaceId.Should().Be(SeedData.Rama8BridgeYard);
        clothes[0].Parts.Product.Should().Be(40, "a general-goods space takes clothes");
        clothes.Should().NotContain(m => m.SpaceId == SeedData.PakSoiCharan79, "a food-only space scores 0 for clothes");
    }

    [Fact]
    public async Task Partial_and_zero_parts_show_up_in_the_api_too()
    {
        var matches = await Match(new { productType = "ข้าวแกง", preferredArea = "พระราม 8", preferredTime = "Morning" });

        var charan = matches.Single(m => m.SpaceId == SeedData.PakSoiCharan79);
        charan.Parts.Should().Be(new PartsDto(40, 30, 0, 0), "not in พระราม 8, and open only in the evening");
        var rama8 = matches.Single(m => m.SpaceId == SeedData.Rama8BridgeYard);
        rama8.Parts.Should().Be(new PartsDto(20, 30, 20, 0), "a general-goods space only partly fits food; evening only");
    }

    [Fact]
    public async Task A_product_type_is_required()
    {
        (await api.CreateClient().PostAsJsonAsync($"/api/spaces/match?districtId={SeedData.BangPhlat}", new { productType = " " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<MatchDto[]> Match(object query)
    {
        var response = await api.CreateClient().PostAsJsonAsync($"/api/spaces/match?districtId={SeedData.BangPhlat}", query);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<MatchDto[]>())!;
    }
}
