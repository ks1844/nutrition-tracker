using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class FoodUnitConfiguration : IEntityTypeConfiguration<FoodUnit>
{
    /// <summary>MySQL は部分インデックスに対応しないため、生成列＋UNIQUE で「食材ごとの既定単位は 1 件」を表す（docs/05.テーブル定義書.md §3.5）。</summary>
    public const string DefaultKeyColumn = "既定一意キー";

    public void Configure(EntityTypeBuilder<FoodUnit> b)
    {
        b.ToTable("T食材単位", t => t.HasCheckConstraint("CK_食材単位_グラム", "`1単位あたりグラム` > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.FoodId).HasColumnName("食材ID").HasColumnType("bigint");
        b.Property(x => x.UnitName).HasColumnName("単位名").HasColumnType("varchar(30)").IsRequired();
        b.Property(x => x.GramsPerUnit).HasColumnName("1単位あたりグラム").HasColumnType("decimal(8,2)");
        b.Property(x => x.IsDefault).HasColumnName("既定フラグ").HasDefaultValue(false);
        b.Property<long?>(DefaultKeyColumn).HasColumnType("bigint")
            .HasComputedColumnSql("IF(`既定フラグ` = 1, `食材ID`, NULL)", stored: true);
        b.HasOne(x => x.Food).WithMany(f => f.Units).HasForeignKey(x => x.FoodId)
            .HasConstraintName("FK_食材単位_食材").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.FoodId, x.UnitName }).IsUnique().HasDatabaseName("UQ_食材単位_食材ID_単位名");
        b.HasIndex(DefaultKeyColumn).IsUnique().HasDatabaseName("UQ_食材単位_既定一意キー");
        b.ConfigureAudit();
    }
}
