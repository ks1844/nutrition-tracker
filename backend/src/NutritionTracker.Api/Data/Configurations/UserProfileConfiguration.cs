using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> b)
    {
        b.ToTable("Tユーザープロフィール", t =>
        {
            t.HasCheckConstraint("CK_ユーザープロフィール_性別", "`性別` IN ('male','female')");
            t.HasCheckConstraint("CK_ユーザープロフィール_目的", "`目的` IN ('skin','muscle','both')");
            t.HasCheckConstraint("CK_ユーザープロフィール_活動レベル", "`活動レベル` BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_ユーザープロフィール_身体", "`身長cm` > 0 AND `体重kg` > 0");
            t.HasCheckConstraint("CK_ユーザープロフィール_目標", "`エネルギー目標kcal` IS NULL OR `エネルギー目標kcal` > 0");
        });
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).HasColumnName("ユーザーID").HasColumnType("bigint").ValueGeneratedNever();
        b.Property(x => x.Sex).HasColumnName("性別").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.BirthDate).HasColumnName("生年月日").HasColumnType("date");
        b.Property(x => x.HeightCm).HasColumnName("身長cm").HasColumnType("decimal(4,1)");
        b.Property(x => x.WeightKg).HasColumnName("体重kg").HasColumnType("decimal(4,1)");
        b.Property(x => x.ActivityLevel).HasColumnName("活動レベル").HasColumnType("smallint");
        b.Property(x => x.Goal).HasColumnName("目的").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.EnergyTargetKcal).HasColumnName("エネルギー目標kcal").HasColumnType("int");
        b.HasOne(x => x.User).WithOne().HasForeignKey<UserProfile>(x => x.UserId)
            .HasConstraintName("FK_ユーザープロフィール_ユーザー").OnDelete(DeleteBehavior.Restrict);
        b.ConfigureAudit();
    }
}
