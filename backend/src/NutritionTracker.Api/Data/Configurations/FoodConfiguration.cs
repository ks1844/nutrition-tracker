using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「T食材」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class FoodConfiguration : IEntityTypeConfiguration<Food>
{
    /// <summary>
    /// 「T食材」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<Food> b)
    {
        b.ToTable("T食材", t => t.HasCheckConstraint("CK_食材_廃棄率", "`廃棄率` >= 0 AND `廃棄率` < 100"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.FoodCode).HasColumnName("食品コード").HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.Name).HasColumnName("名称").HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.OfficialName).HasColumnName("正式名称").HasColumnType("varchar(200)");
        b.Property(x => x.NameKana).HasColumnName("読み仮名").HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.FoodGroup).HasColumnName("食品群").HasColumnType("varchar(50)");
        b.Property(x => x.RefuseRatePct).HasColumnName("廃棄率").HasColumnType("decimal(4,1)").HasDefaultValue(0m);
        b.Property(x => x.SourceVersion).HasColumnName("出典版").HasColumnType("varchar(50)").IsRequired();
        b.Property(x => x.IsActive).HasColumnName("有効フラグ").HasDefaultValue(true);
        b.HasIndex(x => x.FoodCode).IsUnique().HasDatabaseName("UQ_食材_食品コード");
        b.HasIndex(x => x.NameKana).HasDatabaseName("IX_食材_読み仮名");
        b.HasIndex(x => x.Name).HasDatabaseName("IX_食材_名称");
        b.ConfigureAudit();
    }
}
