using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「T食事記録項目栄養素」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class MealRecordItemNutrientConfiguration : IEntityTypeConfiguration<MealRecordItemNutrient>
{
    /// <summary>
    /// 「T食事記録項目栄養素」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<MealRecordItemNutrient> b)
    {
        b.ToTable("T食事記録項目栄養素");
        b.HasKey(x => new { x.MealRecordItemId, x.NutrientId });
        b.Property(x => x.MealRecordItemId).HasColumnName("食事記録項目ID").HasColumnType("bigint");
        b.Property(x => x.NutrientId).HasColumnName("栄養素ID").HasColumnType("smallint");
        b.Property(x => x.Amount).HasColumnName("量").HasColumnType("decimal(14,4)");
        b.Property(x => x.IsIncomplete).HasColumnName("不完全フラグ").HasDefaultValue(false);
        b.HasOne(x => x.MealRecordItem).WithMany(i => i.Nutrients).HasForeignKey(x => x.MealRecordItemId)
            .HasConstraintName("FK_食事記録項目栄養素_食事記録項目").OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Nutrient).WithMany().HasForeignKey(x => x.NutrientId)
            .HasConstraintName("FK_食事記録項目栄養素_栄養素").OnDelete(DeleteBehavior.Restrict);
        b.ConfigureAudit();
    }
}
