using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.Foods;

/// <summary>API-01 食材検索（docs/09.API仕様.md §3）。</summary>
[Collection(ApiCollection.Name)]
public class FoodsApiTests(ApiFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<FoodSearchResultDto> SearchAsync(string q)
    {
        var res = await fixture.Client.GetAsync($"/api/foods?q={Uri.EscapeDataString(q)}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return (await res.Content.ReadFromJsonAsync<FoodSearchResultDto>())!;
    }

    [Theory]
    [InlineData("にんじん")]
    [InlineData("ニンジン")] // AC-FOOD-001: ひらがな⇔カタカナ
    [InlineData("ﾆﾝｼﾞﾝ")] // 半角カナ
    [InlineData("人参")] // AC-FOOD-002: 別名（漢字）
    public async Task AC_FOOD_001_002_Search_FindsByKanaKatakanaAndAlias(string q)
    {
        await fixture.WithDbAsync(async db =>
        {
            await TestData.AddCarrotAsync(db);
            await TestData.AddPorkAsync(db);
        });

        var result = await SearchAsync(q);

        Assert.Equal(1, result.Total);
        Assert.Equal("にんじん", result.Items.Single().Name);
        var unit = result.Items.Single().Units.Single();
        Assert.Equal(("本", 150m, true), (unit.UnitName, unit.GramsPerUnit, unit.IsDefault));
    }

    [Fact]
    public async Task AC_FOOD_003_Search_PartialMatch_PutsPrefixMatchFirst()
    {
        await fixture.WithDbAsync(async db =>
        {
            await TestData.AddFoodAsync(db, "A1", "ミニにんじん", "みにんじん"); // 部分一致
            await TestData.AddFoodAsync(db, "A2", "にんじんジュース", "にんじんじゅーす"); // 前方一致
        });

        var result = await SearchAsync("にんじん");

        Assert.Equal(["にんじんジュース", "ミニにんじん"], result.Items.Select(i => i.Name).ToList());
    }

    [Fact]
    public async Task AC_FOOD_005_Search_ExcludesInactiveFoods()
    {
        await fixture.WithDbAsync(async db =>
        {
            await TestData.AddFoodAsync(db, "B1", "旧にんじん", "きゅうにんじん", isActive: false);
            await TestData.AddCarrotAsync(db);
        });

        var result = await SearchAsync("にんじん");

        Assert.Equal(["にんじん"], result.Items.Select(i => i.Name).ToList());
    }

    [Fact]
    public async Task Search_SpecialCharactersAreNotTreatedAsWildcards()
    {
        await fixture.WithDbAsync(async db => await TestData.AddCarrotAsync(db));

        Assert.Equal(0, (await SearchAsync("%")).Total);
        Assert.Equal(0, (await SearchAsync("_")).Total);
    }

    [Fact]
    public async Task Search_PagesWithLimitAndOffset()
    {
        await fixture.WithDbAsync(async db =>
        {
            for (var i = 1; i <= 5; i++)
            {
                await TestData.AddFoodAsync(db, $"P{i}", $"豆{i}", $"まめ{i}");
            }
        });

        var res = await fixture.Client.GetAsync("/api/foods?q=豆&limit=2&offset=2");
        var page = (await res.Content.ReadFromJsonAsync<FoodSearchResultDto>())!;

        Assert.Equal(5, page.Total);
        Assert.Equal(2, page.Items.Count);
    }

    [Theory]
    [InlineData("/api/foods", "q")] // q が無い
    [InlineData("/api/foods?q=%20%20", "q")] // 空白のみ
    [InlineData("/api/foods?q=あ&limit=0", "limit")]
    [InlineData("/api/foods?q=あ&offset=-1", "offset")]
    public async Task Search_InvalidParameters_Return400WithField(string url, string field)
    {
        var res = await fixture.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var err = (await res.Content.ReadFromJsonAsync<ApiErrorResponse>())!;
        Assert.Equal("VALIDATION_ERROR", err.Code);
        Assert.Equal(field, err.Details.Single().Field);
    }

    [Fact]
    public async Task Search_TooLongQuery_Returns400()
    {
        var res = await fixture.Client.GetAsync($"/api/foods?q={new string('あ', 51)}");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
