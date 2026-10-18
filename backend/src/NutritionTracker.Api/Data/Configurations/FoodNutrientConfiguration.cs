using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「T食材栄養素」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class FoodNutrientConfiguration : IEntityTypeConfiguration<FoodNutrient>
{
    /// <summary>
    /// 「T食材栄養素」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<FoodNutrient> b)
    {
        b.ToTable("T食材栄養素", t =>
        {
            t.HasCheckConstraint("CK_食材栄養素_非負", "`100gあたり含有量` IS NULL OR `100gあたり含有量` >= 0");
            // NULL を通さないため COALESCE を使う（Tr は 0 + 微量フラグ）
            t.HasCheckConstraint("CK_食材栄養素_微量", "`微量フラグ` = 0 OR COALESCE(`100gあたり含有量`, -1) = 0");
        });
        b.HasKey(x => new { x.FoodId, x.NutrientId });
        b.Property(x => x.FoodId).HasColumnName("食材ID").HasColumnType("bigint");
        b.Property(x => x.NutrientId).HasColumnName("栄養素ID").HasColumnType("smallint");
        b.Property(x => x.AmountPer100g).HasColumnName("100gあたり含有量").HasColumnType("decimal(12,4)");
        b.Property(x => x.IsTrace).HasColumnName("微量フラグ").HasDefaultValue(false);
        b.Property(x => x.SourceNote).HasColumnName("出典メモ").HasColumnType("varchar(200)");
        b.HasOne(x => x.Food).WithMany(f => f.Nutrients).HasForeignKey(x => x.FoodId)
            .HasConstraintName("FK_食材栄養素_食材").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Nutrient).WithMany().HasForeignKey(x => x.NutrientId)
            .HasConstraintName("FK_食材栄養素_栄養素").OnDelete(DeleteBehavior.Restrict);
        b.ConfigureAudit();
    }
}
