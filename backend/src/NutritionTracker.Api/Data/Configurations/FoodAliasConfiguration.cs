using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class FoodAliasConfiguration : IEntityTypeConfiguration<FoodAlias>
{
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
