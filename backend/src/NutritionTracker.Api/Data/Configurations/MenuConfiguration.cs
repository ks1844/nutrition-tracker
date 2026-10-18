using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// 「Tメニュー」テーブルの列名・型・制約・索引・外部キーの対応づけです（docs/05.テーブル定義書.md）。
/// </summary>
internal class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    /// <summary>
    /// 「Tメニュー」テーブルの定義（日本語の列名、型、NOT NULL、CHECK 制約、索引、外部キー、監査列）を設定します。
    /// </summary>
    /// <param name="b">テーブルの設定ビルダー。</param>
    public void Configure(EntityTypeBuilder<Menu> b)
    {
        b.ToTable("Tメニュー", t =>
        {
            t.HasCheckConstraint("CK_メニュー_完成後重量", "`完成後重量g` > 0");
            t.HasCheckConstraint("CK_メニュー_杯数", "`杯数` > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.UserId).HasColumnName("ユーザーID").HasColumnType("bigint");
        b.Property(x => x.Name).HasColumnName("名称").HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.CookedTotalWeightG).HasColumnName("完成後重量g").HasColumnType("decimal(8,1)");
        b.Property(x => x.Servings).HasColumnName("杯数").HasColumnType("decimal(5,1)");
        b.Property(x => x.ServingLabel).HasColumnName("杯ラベル").HasColumnType("varchar(20)").HasDefaultValue("お椀");
        b.Property(x => x.CookedOn).HasColumnName("調理日").HasColumnType("date");
        b.Property(x => x.Memo).HasColumnName("メモ").HasColumnType("varchar(500)");
        b.Property(x => x.DeletedAt).HasColumnName("削除日時").HasColumnType("datetime");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_メニュー_ユーザー").OnDelete(DeleteBehavior.Restrict);
        // 有効メニューの一覧用（MySQL は部分インデックス非対応のため、削除日時を含む通常索引）
        b.HasIndex(x => new { x.UserId, x.DeletedAt }).HasDatabaseName("IX_メニュー_ユーザーID_削除日時");
        b.ConfigureAudit();
    }
}
