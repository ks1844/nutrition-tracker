using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NutritionTracker.Api.Dtos;
using NutritionTracker.Api.Services.MasterImport;
using NutritionTracker.Api.Tests.Infrastructure;

namespace NutritionTracker.Api.Tests.MasterImport;

/// <summary>
/// マスタ CSV の取り込み（docs/07.マスタデータ仕様.md）。<b>CSV の値はテスト用のダミーで、実際の成分表・食事摂取基準の値ではない</b>。
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MasterImportTests(ApiFixture fixture) : IAsyncLifetime, IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("master-import-").FullName;

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private const string Header = "食品コード,名称,正式名称,読み仮名,食品群,廃棄率,出典版,有効フラグ\n";

    private const string FoodsCsv = Header
        + "F1,にんじん,,ニンジン,野菜類,10,テスト（ダミー）,\n"
        + "F2,\"豚こま肉, 国産\",,ぶたこまにく,肉類,0,テスト（ダミー）,1\n"
        + "F3,みそ,,みそ,調味料類,0.0,テスト（ダミー）,\n";

    private const string NutrientsCsv = "食品コード,栄養素コード,成分表表記,出典メモ\n"
        + "F1,ENERGY,35,\nF1,PROT,(1.0),推定値\nF1,FAT,Tr,\nF1,CHO,9.3,\nF1,VITC,10,\nF1,VITA_RAE,\"1,700\",\n"
        + "F2,ENERGY,120,\nF2,PROT,20,\nF2,FAT,-,\nF2,CHO,0.1,\n"
        + "F3,ENERGY,190,\nF3,PROT,12,\nF3,FAT,6,\nF3,CHO,21,\nF3,VITC,,\n";

    private const string UnitsCsv = "食品コード,単位名,1単位あたりグラム,既定フラグ,根拠メモ\n"
        + "F1,本,150,1,暫定\nF3,大さじ,18,1,\nF3,小さじ,6,0,\n";

    private const string AliasesCsv = "食品コード,別名\nF1,人参\nF1,ニンジン\nF3,味噌\n";

    private const string TargetsCsv = "栄養素コード,性別,年齢下限,年齢上限,種別,目標値,単位基準,出典メモ\n"
        + "CA,male,30,49,RDA,750,PER_DAY,テスト（ダミー）\nVITC,all,18,999,RDA,100,PER_DAY,\nMG,all,0,999,RDA,5,PER_KG_BW,\n";

    private void Write(string file, string content) => File.WriteAllText(Path.Combine(_dir, file), content);

    private void WriteAll()
    {
        Write(MasterImportService.FoodsFile, FoodsCsv);
        Write(MasterImportService.FoodNutrientsFile, NutrientsCsv);
        Write(MasterImportService.UnitsFile, UnitsCsv);
        Write(MasterImportService.AliasesFile, AliasesCsv);
        Write(MasterImportService.TargetsFile, TargetsCsv);
    }

    private Task<MasterImportResult> ImportAsync() =>
        fixture.WithDbAsync(db => new MasterImportService(db, NullLogger<MasterImportService>.Instance).ImportAsync(_dir));

    private async Task<MasterImportException> ImportFailsAsync()
    {
        var ex = await Assert.ThrowsAsync<MasterImportException>(ImportAsync);
        Assert.Equal(0, await fixture.WithDbAsync(db => db.Foods.CountAsync())); // 何も書き込まれない
        return ex;
    }

    // ---- 正常系 ----
    [Fact]
    public async Task Imports_AllFiles_WithNotationConversion()
    {
        WriteAll();

        var result = await ImportAsync();

        Assert.Equal((true, 3, 15, 3, 3, 3, 0), (result.Imported, result.Foods, result.FoodNutrients, result.Units, result.Aliases, result.Targets, result.Deactivated));
        Assert.Empty(result.Warnings);
        await fixture.WithDbAsync(async db =>
        {
            var carrot = await db.Foods.Include(f => f.Nutrients).ThenInclude(n => n.Nutrient).Include(f => f.Units).Include(f => f.Aliases).SingleAsync(f => f.FoodCode == "F1");
            Assert.Equal(("にんじん", "にんじん", "野菜類", 10.0m, "テスト（ダミー）", true), (carrot.Name, carrot.NameKana, carrot.FoodGroup, carrot.RefuseRatePct, carrot.SourceVersion, carrot.IsActive)); // 読み仮名はひらがなに正規化
            var n = carrot.Nutrients.ToDictionary(x => x.Nutrient!.Code);
            Assert.Equal((35m, false), (n["ENERGY"].AmountPer100g, n["ENERGY"].IsTrace));
            Assert.Equal((1.0m, false, "推定値"), (n["PROT"].AmountPer100g, n["PROT"].IsTrace, n["PROT"].SourceNote)); // (1.0) は括弧を除く
            Assert.Equal((0m, true), (n["FAT"].AmountPer100g, n["FAT"].IsTrace)); // Tr = 0 + 微量
            Assert.Equal(1700m, n["VITA_RAE"].AmountPer100g); // 桁区切りのカンマ
            Assert.False(n.ContainsKey("ASTX")); // 行の無い栄養素は行を作らない
            var unit = Assert.Single(carrot.Units);
            Assert.Equal(("本", 150.00m, true), (unit.UnitName, unit.GramsPerUnit, unit.IsDefault));
            Assert.Equal(["ニンジン", "人参"], carrot.Aliases.Select(a => a.Alias).Order().ToList());

            var pork = await db.Foods.Include(f => f.Nutrients).ThenInclude(x => x.Nutrient).SingleAsync(f => f.FoodCode == "F2");
            Assert.Equal("豚こま肉, 国産", pork.Name); // 引用符つきの値
            var fat = pork.Nutrients.Single(x => x.Nutrient!.Code == "FAT");
            Assert.Equal((null, false), (fat.AmountPer100g, fat.IsTrace)); // - = 未測定（0 ではない）

            var miso = await db.Foods.Include(f => f.Nutrients).ThenInclude(x => x.Nutrient).Include(f => f.Units).SingleAsync(f => f.FoodCode == "F3");
            Assert.Null(miso.Nutrients.Single(x => x.Nutrient!.Code == "VITC").AmountPer100g); // 空欄 = 未測定
            Assert.Equal("大さじ", miso.Units.Single(u => u.IsDefault).UnitName);

            var targets = await db.NutrientTargets.Include(t => t.Nutrient).OrderBy(t => t.Nutrient!.Code).ToListAsync();
            Assert.Equal(["CA", "MG", "VITC"], targets.Select(t => t.Nutrient!.Code).ToList());
            Assert.Equal(("male", (short)30, (short)49, "RDA", 750m, "PER_DAY"), (targets[0].Sex, targets[0].AgeMin, targets[0].AgeMax, targets[0].Kind, targets[0].Amount, targets[0].Basis));
            Assert.Equal("PER_KG_BW", targets[1].Basis);
        });
    }

    [Fact]
    public async Task Imported_Foods_AreSearchableByKanaAndAlias()
    {
        WriteAll();
        await ImportAsync();

        async Task<List<string>> Search(string q) => (await fixture.Client.GetFromJsonAsync<FoodSearchResultDto>($"/api/foods?q={Uri.EscapeDataString(q)}"))!.Items.Select(f => f.Name).ToList();

        Assert.Equal(["にんじん"], await Search("ニンジン")); // CSV はカタカナでも検索できる
        Assert.Equal(["にんじん"], await Search("人参")); // 別名
        Assert.Equal(["みそ"], await Search("味噌"));
    }

    [Fact]
    public async Task Import_IsIdempotent()
    {
        WriteAll();
        await ImportAsync();
        var first = await CountsAsync();

        var second = await ImportAsync();

        Assert.Equal(first, await CountsAsync());
        Assert.Equal((3, 15, 3, 3, 3), (second.Foods, second.FoodNutrients, second.Units, second.Aliases, second.Targets));
    }

    private Task<(int, int, int, int, int)> CountsAsync() => fixture.WithDbAsync(async db => (
        await db.Foods.CountAsync(), await db.FoodNutrients.CountAsync(), await db.FoodUnits.CountAsync(),
        await db.FoodAliases.CountAsync(), await db.NutrientTargets.CountAsync()));

    [Fact]
    public async Task Reimport_UpdatesValues_MovesDefaultUnit_AndDeactivatesRemovedFoods()
    {
        WriteAll();
        await ImportAsync();
        await fixture.Client.PostAsJsonAsync("/api/menus", new MenuInput("味噌汁", null, null, 500m, 2m, null, [new MenuItemInput(
            await fixture.WithDbAsync(db => db.Foods.Where(f => f.FoodCode == "F2").Select(f => f.Id).SingleAsync()), null, 100m)]));
        Write(MasterImportService.FoodsFile, Header
            + "F1,にんじん（改）,,にんじん,野菜類,15,テスト（ダミー）,\n"
            + "F3,みそ,,みそ,調味料類,0,テスト（ダミー）,\n"); // F2 を外す
        Write(MasterImportService.FoodNutrientsFile, "食品コード,栄養素コード,成分表表記\nF1,PROT,2.5\nF3,PROT,13\nF3,ENERGY,190\nF3,FAT,6\nF3,CHO,21\n");
        Write(MasterImportService.UnitsFile, "食品コード,単位名,1単位あたりグラム,既定フラグ\nF3,大さじ,18,0\nF3,小さじ,6,1\n"); // 既定を付け替える
        Write(MasterImportService.AliasesFile, "食品コード,別名\nF1,人参\n");
        Write(MasterImportService.TargetsFile, "栄養素コード,性別,年齢下限,年齢上限,種別,目標値,単位基準\nCA,male,30,49,RDA,800,PER_DAY\n");

        var result = await ImportAsync();

        Assert.Equal(1, result.Deactivated);
        await fixture.WithDbAsync(async db =>
        {
            var carrot = await db.Foods.Include(f => f.Nutrients).ThenInclude(n => n.Nutrient).SingleAsync(f => f.FoodCode == "F1");
            Assert.Equal(("にんじん（改）", 15.0m), (carrot.Name, carrot.RefuseRatePct));
            Assert.Equal(2.5m, carrot.Nutrients.Single(n => n.Nutrient!.Code == "PROT").AmountPer100g);
            Assert.Equal(35m, carrot.Nutrients.Single(n => n.Nutrient!.Code == "ENERGY").AmountPer100g); // CSV に無い行は削除しない
            var pork = await db.Foods.SingleAsync(f => f.FoodCode == "F2");
            Assert.False(pork.IsActive); // 削除せず無効にする（メニューが参照している）
            Assert.Equal("小さじ", (await db.FoodUnits.Where(u => u.IsDefault && u.Food!.FoodCode == "F3").SingleAsync()).UnitName);
            Assert.Equal(800m, (await db.NutrientTargets.SingleAsync(t => t.Nutrient!.Code == "CA")).Amount);
            Assert.Equal(3, await db.NutrientTargets.CountAsync()); // 目標も CSV に無い行は削除しない
        });
    }

    [Fact]
    public async Task OnlyFoodsCsv_LeavesExistingChildrenAndDefaultUnitsUntouched()
    {
        WriteAll();
        await ImportAsync();
        File.Delete(Path.Combine(_dir, MasterImportService.FoodNutrientsFile));
        File.Delete(Path.Combine(_dir, MasterImportService.UnitsFile));
        File.Delete(Path.Combine(_dir, MasterImportService.AliasesFile));
        File.Delete(Path.Combine(_dir, MasterImportService.TargetsFile));

        var result = await ImportAsync();

        Assert.Equal((true, 3, 0), (result.Imported, result.Foods, result.FoodNutrients));
        Assert.Equal((3, 15, 3, 3, 3), await CountsAsync());
        Assert.Equal(2, await fixture.WithDbAsync(db => db.FoodUnits.CountAsync(u => u.IsDefault))); // 既定単位は外れない
    }

    [Fact]
    public async Task NoCsvFiles_IsSkipped()
    {
        var result = await ImportAsync();

        Assert.False(result.Imported);
        Assert.Equal(0, await fixture.WithDbAsync(db => db.Foods.CountAsync()));
        Assert.False((await fixture.WithDbAsync(db => new MasterImportService(db, NullLogger<MasterImportService>.Instance).ImportAsync(Path.Combine(_dir, "none")))).Imported); // ディレクトリが無くても失敗しない
    }

    [Fact]
    public async Task Csv_WithBomAndCrlf_IsAccepted()
    {
        Write(MasterImportService.FoodsFile, "﻿" + FoodsCsv.Replace("\n", "\r\n"));

        var result = await ImportAsync();

        Assert.Equal(3, result.Foods);
        Assert.Equal("F1", (await fixture.WithDbAsync(db => db.Foods.OrderBy(f => f.Id).FirstAsync())).FoodCode);
    }

    [Fact]
    public async Task MissingCoreNutrientRows_AreWarnedNotRejected()
    {
        Write(MasterImportService.FoodsFile, FoodsCsv);
        Write(MasterImportService.FoodNutrientsFile, "食品コード,栄養素コード,成分表表記\nF1,ENERGY,35\nF1,PROT,1\nF1,FAT,0.1\nF1,CHO,9\nF2,ENERGY,120\n");

        var result = await ImportAsync();

        Assert.True(result.Imported);
        Assert.Contains(result.Warnings, w => w.Contains("F2") && w.Contains("PROT・FAT・CHO") && w.Contains("V-7"));
        Assert.Contains(result.Warnings, w => w.Contains("F3") && w.Contains("ENERGY・PROT・FAT・CHO"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("F1"));
    }

    // ---- 検証エラー（1 件でもあれば何も取り込まない） ----
    [Fact]
    public async Task InvalidFoods_AreReportedWithLineNumbers_AndNothingIsWritten()
    {
        Write(MasterImportService.FoodsFile, Header
            + "F1,にんじん,,にんじん,野菜類,10,テスト,\n" // 2 行目: 正常
            + "F1,にんじん2,,にんじん,野菜類,10,テスト,\n" // 3 行目: コード重複
            + "F4,ABC,,ABC,野菜類,100,,\n" // 4 行目: 読み仮名・廃棄率・出典版
            + "F5,かぼちゃ,,かぼちゃ,野菜類,-5,テスト,2\n" // 5 行目: 廃棄率が負・フラグ
            + ",名無し,,なし,,0,テスト,\n"); // 6 行目: コード空

        var ex = await ImportFailsAsync();

        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 3行目") && e.Contains("重複") && e.Contains("V-1"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 4行目") && e.Contains("読み仮名") && e.Contains("V-8"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 4行目") && e.Contains("廃棄率") && e.Contains("V-4"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 4行目") && e.Contains("出典版"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 5行目") && e.Contains("廃棄率"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 5行目") && e.Contains("有効フラグ"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv 6行目") && e.Contains("食品コード"));
        Assert.DoesNotContain(ex.Errors, e => e.Contains("2行目"));
    }

    [Fact]
    public async Task InvalidNutrientRows_AreReported()
    {
        Write(MasterImportService.FoodsFile, FoodsCsv);
        Write(MasterImportService.FoodNutrientsFile, "食品コード,栄養素コード,成分表表記\n"
            + "F9,PROT,1\n" // 2 行目: 食品が無い
            + "F1,XXX,1\n" // 3 行目: 栄養素が無い
            + "F1,PROT,abc\n" // 4 行目: 表記
            + "F1,FAT,-1\n" // 5 行目: 負
            + "F1,CHO,1\nF1,CHO,2\n"); // 7 行目: 重複

        var ex = await ImportFailsAsync();

        Assert.Contains(ex.Errors, e => e.Contains("食材栄養素.csv 2行目") && e.Contains("V-1"));
        Assert.Contains(ex.Errors, e => e.Contains("食材栄養素.csv 3行目") && e.Contains("V-2"));
        Assert.Contains(ex.Errors, e => e.Contains("食材栄養素.csv 4行目") && e.Contains("V-3"));
        Assert.Contains(ex.Errors, e => e.Contains("食材栄養素.csv 5行目") && e.Contains("V-3"));
        Assert.Contains(ex.Errors, e => e.Contains("食材栄養素.csv 7行目") && e.Contains("重複"));
    }

    [Fact]
    public async Task InvalidUnitsAliasesAndTargets_AreReported()
    {
        Write(MasterImportService.FoodsFile, FoodsCsv);
        Write(MasterImportService.UnitsFile, "食品コード,単位名,1単位あたりグラム,既定フラグ\n"
            + "F1,本,0,\nF1,個,10,1\nF1,個,10,\nF1,束,5,1\nF9,枚,1,\n");
        Write(MasterImportService.AliasesFile, "食品コード,別名\nF1,人参\nF1,人参\nF9,x\n");
        Write(MasterImportService.TargetsFile, "栄養素コード,性別,年齢下限,年齢上限,種別,目標値,単位基準\n"
            + "ENERGY,all,0,999,DG_MIN,2000,PER_DAY\n" // 2 行目
            + "CA,other,0,999,RDA,1,PER_DAY\n" // 3 行目
            + "CA,male,30,20,RDA,1,PER_DAY\n" // 4 行目
            + "CA,male,18,29,RDA,1,PER_WEEK\n" // 5 行目
            + "MG,male,18,29,RDA,-3,PER_DAY\n" // 6 行目
            + "FE,male,18,29,RDA,1,PER_DAY\nFE,male,18,29,RDA,2,PER_DAY\n" // 8 行目: 重複
            + "ZN,male,18,40,RDA,1,PER_DAY\nZN,all,30,49,RDA,2,PER_DAY\n"); // 範囲の重なり

        var ex = await ImportFailsAsync();

        Assert.Contains(ex.Errors, e => e.Contains("食材単位.csv 2行目") && e.Contains("V-5"));
        Assert.Contains(ex.Errors, e => e.Contains("食材単位.csv 4行目") && e.Contains("重複"));
        Assert.Contains(ex.Errors, e => e.Contains("食材単位.csv 5行目") && e.Contains("既定フラグ"));
        Assert.Contains(ex.Errors, e => e.Contains("食材単位.csv 6行目") && e.Contains("V-1"));
        Assert.Contains(ex.Errors, e => e.Contains("食材別名.csv 3行目") && e.Contains("重複"));
        Assert.Contains(ex.Errors, e => e.Contains("食材別名.csv 4行目") && e.Contains("V-1"));
        Assert.Contains(ex.Errors, e => e.Contains("栄養目標.csv 2行目") && e.Contains("ENERGY"));
        Assert.Contains(ex.Errors, e => e.Contains("栄養目標.csv 3行目") && e.Contains("性別"));
        Assert.Contains(ex.Errors, e => e.Contains("栄養目標.csv 4行目") && e.Contains("V-10"));
        Assert.Contains(ex.Errors, e => e.Contains("栄養目標.csv 5行目") && e.Contains("単位基準"));
        Assert.Contains(ex.Errors, e => e.Contains("栄養目標.csv 6行目") && e.Contains("V-9"));
        Assert.Contains(ex.Errors, e => e.Contains("栄養目標.csv 8行目") && e.Contains("重複"));
        Assert.Contains(ex.Errors, e => e.Contains("ZN") && e.Contains("範囲が重なる"));
        Assert.Equal(0, await fixture.WithDbAsync(db => db.NutrientTargets.CountAsync()));
    }

    [Fact]
    public async Task MissingRequiredColumn_IsReported()
    {
        Write(MasterImportService.FoodsFile, "食品コード,名称\nF1,にんじん\n");

        var ex = await ImportFailsAsync();

        Assert.Contains(ex.Errors, e => e.Contains("食材.csv") && e.Contains("読み仮名"));
        Assert.Contains(ex.Errors, e => e.Contains("食材.csv") && e.Contains("出典版"));
    }

    [Fact]
    public async Task ErrorInOneFile_StopsEveryFileFromBeingImported()
    {
        Write(MasterImportService.FoodsFile, FoodsCsv);
        Write(MasterImportService.TargetsFile, "栄養素コード,性別,年齢下限,年齢上限,種別,目標値,単位基準\nCA,other,0,999,RDA,1,PER_DAY\n");

        await ImportFailsAsync(); // 食材は正しくても、目標のエラーで全体を取り込まない

        Assert.Equal(0, await fixture.WithDbAsync(db => db.NutrientTargets.CountAsync()));
    }
}
