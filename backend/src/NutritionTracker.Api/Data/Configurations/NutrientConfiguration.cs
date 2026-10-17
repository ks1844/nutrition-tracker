using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Data.Seed;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「T栄養素」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class NutrientConfiguration : IEntityTypeConfiguration<Nutrient>
{
    /// <summary>
    /// 「T栄養素」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<Nutrient> b)
    {
        b.ToTable("T栄養素", t => t.HasCheckConstraint("CK_栄養素_区分", "`区分` IN ('macro','vitamin','mineral','antioxidant')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("smallint").ValueGeneratedOnAdd();
        b.Property(x => x.Code).HasColumnName("栄養素コード").HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.Name).HasColumnName("名称").HasColumnType("varchar(50)").IsRequired();
        b.Property(x => x.Unit).HasColumnName("単位").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.Category).HasColumnName("区分").HasColumnType("varchar(20)").IsRequired();
        b.Property(x => x.IsReference).HasColumnName("参考値フラグ").HasDefaultValue(false);
        b.Property(x => x.DisplayOrder).HasColumnName("表示順").HasColumnType("smallint");
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_栄養素_栄養素コード");
        b.ConfigureAudit();
        b.HasData(NutrientSeed.All());
    }
}
