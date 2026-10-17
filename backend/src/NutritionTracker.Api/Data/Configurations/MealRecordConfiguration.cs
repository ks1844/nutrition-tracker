using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class MealRecordConfiguration : IEntityTypeConfiguration<MealRecord>
{
    public void Configure(EntityTypeBuilder<MealRecord> b)
    {
        b.ToTable("T食事記録", t => t.HasCheckConstraint("CK_食事記録_食事区分", "`食事区分` IN ('breakfast','lunch','dinner','snack')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.UserId).HasColumnName("ユーザーID").HasColumnType("bigint");
        b.Property(x => x.EatenOn).HasColumnName("食事日").HasColumnType("date");
        b.Property(x => x.MealType).HasColumnName("食事区分").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.Memo).HasColumnName("メモ").HasColumnType("varchar(500)");
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .HasConstraintName("FK_食事記録_ユーザー").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.EatenOn }).HasDatabaseName("IX_食事記録_ユーザーID_食事日");
        b.ConfigureAudit();
    }
}
