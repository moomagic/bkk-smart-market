using FluentAssertions;
using SmartMarket.Application.Spaces;

namespace SmartMarket.Application.Tests.Spaces;

public sealed class SpaceMatcherTests
{
    private static readonly OpenRoundSummary Open = new(Guid.NewGuid(), new DateOnly(2026, 11, 1), new DateOnly(2027, 4, 30),
        new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.FromHours(7)));

    private static readonly MatchCandidate Charan = new(Guid.NewGuid(), "ปากซอยจรัญฯ 79", ["อาหาร"], "ทุกวัน 16:00–22:00", Open, 4, 10);
    private static readonly MatchCandidate Mrt = new(Guid.NewGuid(), "หน้า MRT บางพลัด ทางออก 1", ["อาหาร", "เครื่องดื่ม"],
        "ทุกวัน 06:00–10:00, 16:00–21:00", Open, 5, 0);
    private static readonly MatchCandidate Market = new(Guid.NewGuid(), "ตลาดนัดเขตบางพลัด", ["ทั่วไป"], "ศ–อา 15:00–22:00", Open, 20, 0);
    private static readonly MatchCandidate Closed = new(Guid.NewGuid(), "ลานใต้สะพานพระราม 8", ["ทั่วไป"], "ส–อา 16:00–22:00", null, 6, 0);

    private static SpaceMatch Score(MatchCandidate space, string product, string? area = null, PreferredTime? time = null) =>
        SpaceMatcher.Score(new MatchQuery(product, area, time), space);

    [Theory]
    [InlineData("ข้าวแกง", 40)]
    [InlineData("กาแฟโบราณ", 0)]
    [InlineData("เสื้อผ้ามือสอง", 0)]
    public void Product_type_scores_40_when_the_space_takes_that_kind_of_goods(string product, int expected)
    {
        Score(Charan, product).Parts.Product.Should().Be(expected);
    }

    [Theory]
    [InlineData("ขนมปังสังขยา", 40, 40)] // food, not a drink: "นม" sits inside ขนม
    [InlineData("ชามเซรามิก", 0, 0)]     // bowls are general goods: "ชา" sits inside ชาม
    [InlineData("เนื้อผ้าไหม", 0, 0)]
    [InlineData("น้ำพริกหนุ่ม", 40, 40)]
    [InlineData("ชาไทย", 0, 40)]
    public void Short_product_words_do_not_match_inside_unrelated_words(string product, int charan, int mrt)
    {
        Score(Charan, product).Parts.Product.Should().Be(charan);
        Score(Mrt, product).Parts.Product.Should().Be(mrt);
    }

    [Fact]
    public void Drinks_match_a_space_that_takes_drinks()
    {
        Score(Mrt, "กาแฟโบราณ").Parts.Product.Should().Be(40);
    }

    [Fact]
    public void A_general_goods_space_fully_fits_general_goods_and_partly_fits_food()
    {
        Score(Market, "เสื้อผ้ามือสอง").Parts.Product.Should().Be(40);
        Score(Market, "ข้าวแกง").Parts.Product.Should().Be(20);
    }

    [Fact]
    public void An_open_round_is_a_free_lock_worth_30()
    {
        var open = Score(Charan, "ข้าวแกง");
        open.Parts.FreeLock.Should().Be(30);
        open.Reasons.Should().Contain(r => r.Contains("เปิดรับคำขอถึง 31 ต.ค. 2569") && r.Contains("10 คำขอ ต่อ 4 ล็อค"));
        Score(Closed, "ข้าวแกง").Parts.FreeLock.Should().Be(0);
    }

    [Theory]
    [InlineData("ย่านจรัญฯ", 20)]
    [InlineData("จรัญ", 20)]
    [InlineData("แถว MRT", 0)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Area_scores_20_when_the_space_is_in_the_chosen_neighbourhood(string? area, int expected)
    {
        Score(Charan, "ข้าวแกง", area).Parts.Area.Should().Be(expected);
    }

    [Fact]
    public void Area_matching_ignores_case_and_spacing()
    {
        Score(Mrt, "กาแฟ", "mrt").Parts.Area.Should().Be(20);
        Score(Closed, "เสื้อผ้า", "พระราม8").Parts.Area.Should().Be(20);
    }

    [Theory]
    [InlineData(PreferredTime.Morning, 10)]
    [InlineData(PreferredTime.Evening, 10)]
    [InlineData(null, 0)]
    public void Hours_score_10_when_the_space_opens_at_the_preferred_time(PreferredTime? time, int expected)
    {
        Score(Mrt, "กาแฟ", time: time).Parts.Hours.Should().Be(expected);
    }

    [Fact]
    public void An_evening_only_space_does_not_suit_the_morning()
    {
        Score(Charan, "ข้าวแกง", time: PreferredTime.Morning).Parts.Hours.Should().Be(0);
    }

    [Fact]
    public void Returns_the_top_three_by_score_each_with_a_thai_reason_per_scored_part()
    {
        var ranked = SpaceMatcher.Rank(new MatchQuery("ข้าวแกง", "จรัญฯ", null), [Market, Closed, Mrt, Charan]);

        ranked.Should().HaveCount(3);
        ranked.Select(m => m.Name).Should().Equal("ปากซอยจรัญฯ 79", "หน้า MRT บางพลัด ทางออก 1", "ตลาดนัดเขตบางพลัด");
        ranked[0].Score.Should().Be(90);
        ranked[0].Reasons.Should().HaveCount(3).And.OnlyContain(r => r.Length > 0);
        ranked[0].Reasons[0].Should().Contain("อาหาร").And.Contain("ข้าวแกง");
    }

    [Fact]
    public void A_space_that_does_not_take_the_goods_is_never_recommended()
    {
        SpaceMatcher.Rank(new MatchQuery("เสื้อผ้ามือสอง", "จรัญฯ", null), [Charan, Market])
            .Select(m => m.Name).Should().Equal("ตลาดนัดเขตบางพลัด");
    }
}
