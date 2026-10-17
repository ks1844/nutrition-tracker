using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class MealRecordItemConfiguration : IEntityTypeConfiguration<MealRecordItem>
{
    public void Configure(EntityTypeBuilder<MealRecordItem> b)
    {
        b.ToTable("T食事記録項目", t =>
        {
            t.HasCheckConstraint("CK_食事記録項目_対象", "(`メニューID` IS NULL) <> (`食材ID` IS NULL)");
            t.HasCheckConstraint("CK_食事記録項目_入力方式", "`入力方式` IN ('RATIO','SERVING','GRAM','UNIT')");
            t.HasCheckConstraint("CK_食事記録項目_入力値", "`入力値` > 0");
            t.HasCheckConstraint("CK_食事記録項目_メニュー",
                "`メニューID` IS NULL OR (`入力方式` IN ('RATIO','SERVING','GRAM') AND `食べた割合` IS NOT NULL AND `食べた割合` > 0)");
            t.HasCheckConstraint("CK_食事記録項目_食材",
                "`食材ID` IS NULL OR (`入力方式` IN ('GRAM','UNIT') AND `重量g` IS NOT NULL AND `重量g` > 0)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.MealRecordId).HasColumnName("食事記録ID").HasColumnType("bigint");
        b.Property(x => x.MenuId).HasColumnName("メニューID").HasColumnType("bigint");
        b.Property(x => x.FoodId).HasColumnName("食材ID").HasColumnType("bigint");
        b.Property(x => x.UnitId).HasColumnName("食材単位ID").HasColumnType("bigint");
        b.Property(x => x.InputMode).HasColumnName("入力方式").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.InputValue).HasColumnName("入力値").HasColumnType("decimal(10,3)");
        b.Property(x => x.PortionRatio).HasColumnName("食べた割合").HasColumnType("decimal(9,6)");
        b.Property(x => x.AmountG).HasColumnName("重量g").HasColumnType("decimal(8,2)");
        b.HasOne(x => x.MealRecord).WithMany(r => r.Items).HasForeignKey(x => x.MealRecordId)
            .HasConstraintName("FK_食事記録項目_食事記録").OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Menu).WithMany().HasForeignKey(x => x.MenuId)
            .HasConstraintName("FK_食事記録項目_メニュー").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Food).WithMany().HasForeignKey(x => x.FoodId)
            .HasConstraintName("FK_食事記録項目_食材").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("FK_食事記録項目_食材単位").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MealRecordId).HasDatabaseName("IX_食事記録項目_食事記録ID");
        b.HasIndex(x => x.MenuId).HasDatabaseName("IX_食事記録項目_メニューID");
        b.ConfigureAudit();
    }
}
