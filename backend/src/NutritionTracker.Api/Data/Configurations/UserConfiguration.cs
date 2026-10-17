using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Data.Seed;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「Tユーザー」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// 「Tユーザー」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("Tユーザー");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.ConfigureAudit();
        // 固定ユーザー（ID=1）。D-1
        b.HasData(new User { Id = User.FixedUserId, CreatedAt = NutrientSeed.SeededAt, UpdatedAt = NutrientSeed.SeededAt });
    }
}
