using System.Net;
using System.Net.Http.Json;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Tests.Infrastructure;
using NutritionTracker.Domain.Nutrition;

namespace NutritionTracker.Api.Tests.Nutrients;

/// <summary>
/// 栄養素一覧 API（GET /api/nutrients、API-03）のテストです（docs/09.API仕様.md §3）。
/// </summary>
[Collection(ApiCollection.Name)]
public class NutrientsApiTests(ApiFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// 各テストの前に、データを初期状態に戻します。
    /// </summary>
    /// <returns>完了を表すタスク。</returns>
    public Task InitializeAsync() => fixture.ResetAsync();

    /// <summary>
    /// 各テストの後の片づけはありません。
    /// </summary>
    /// <returns>完了済みのタスク。</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// 栄養素一覧が 24 件、表示順に返ることを確認します。
    /// タンパク質は「PROT・タンパク質・g・macro・参考値でない・2 番目」、アスタキサンチンは参考値です。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task GetNutrients_ReturnsAllTwentyFour_InDisplayOrder()
    {
        var res = await fixture.Client.GetAsync("/api/nutrients");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var list = await res.Content.ReadFromJsonAsync<List<NutrientDto>>();
        Assert.NotNull(list);
        Assert.Equal(NutrientCodes.All, list.Select(n => n.Code).ToList());
        Assert.Equal(Enumerable.Range(1, 24), list.Select(n => n.DisplayOrder));

        var protein = list.Single(n => n.Code == "PROT");
        Assert.Equal(new NutrientDto("PROT", "タンパク質", "g", "macro", false, 2), protein);
        Assert.True(list.Single(n => n.Code == "ASTX").IsReference); // 参考値（スコア外）
    }

    /// <summary>
    /// JSON の項目名が camelCase（nameJa、isReference、displayOrder）であることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task GetNutrients_JsonIsCamelCase()
    {
        var json = await fixture.Client.GetStringAsync("/api/nutrients");

        Assert.Contains("\"nameJa\"", json);
        Assert.Contains("\"isReference\"", json);
        Assert.Contains("\"displayOrder\"", json);
    }

    /// <summary>
    /// 存在しない API のパスを呼ぶと、404 の NOT_FOUND がエラー形式（details は空の配列）で返ることを確認します。
    /// </summary>
    /// <returns>テストの完了を表すタスク。</returns>
    [Fact]
    public async Task UnknownApiPath_ReturnsNotFoundInErrorFormat()
    {
        var res = await fixture.Client.GetAsync("/api/unknown");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
        var err = await res.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.Equal("NOT_FOUND", err!.Code);
        Assert.Empty(err.Details);
    }
}
