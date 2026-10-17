using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> b)
    {
        b.ToTable("Tメニュー食材", t =>
        {
            t.HasCheckConstraint("CK_メニュー食材_数量", "`数量` > 0");
            t.HasCheckConstraint("CK_メニュー食材_重量", "`重量g` > 0");
            t.HasCheckConstraint("CK_メニュー食材_換算", "`食材単位ID` IS NOT NULL OR `重量g` = `数量`");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.MenuId).HasColumnName("メニューID").HasColumnType("bigint");
        b.Property(x => x.FoodId).HasColumnName("食材ID").HasColumnType("bigint");
        b.Property(x => x.UnitId).HasColumnName("食材単位ID").HasColumnType("bigint");
        b.Property(x => x.Quantity).HasColumnName("数量").HasColumnType("decimal(8,2)");
        b.Property(x => x.AmountG).HasColumnName("重量g").HasColumnType("decimal(8,2)");
        b.Property(x => x.SortOrder).HasColumnName("並び順").HasColumnType("int").HasDefaultValue(0);
        b.HasOne(x => x.Menu).WithMany(m => m.Items).HasForeignKey(x => x.MenuId)
            .HasConstraintName("FK_メニュー食材_メニュー").OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Food).WithMany().HasForeignKey(x => x.FoodId)
            .HasConstraintName("FK_メニュー食材_食材").OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Unit).WithMany().HasForeignKey(x => x.UnitId)
            .HasConstraintName("FK_メニュー食材_食材単位").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.MenuId).HasDatabaseName("IX_メニュー食材_メニューID");
        b.ConfigureAudit();
    }
}
