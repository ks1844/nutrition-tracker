using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「T食材別名」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class FoodAliasConfiguration : IEntityTypeConfiguration<FoodAlias>
{
    /// <summary>
    /// 「T食材別名」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<FoodAlias> b)
    {
        b.ToTable("T食材別名");
        b.HasKey(x => new { x.FoodId, x.Alias });
        b.Property(x => x.FoodId).HasColumnName("食材ID").HasColumnType("bigint");
        b.Property(x => x.Alias).HasColumnName("別名").HasColumnType("varchar(100)");
        b.HasOne(x => x.Food).WithMany(f => f.Aliases).HasForeignKey(x => x.FoodId)
            .HasConstraintName("FK_食材別名_食材").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Alias).HasDatabaseName("IX_食材別名_別名");
        b.ConfigureAudit();
    }
}
