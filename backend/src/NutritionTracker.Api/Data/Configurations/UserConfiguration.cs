using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Data.Seed;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
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
