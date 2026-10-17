using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

internal class NutrientTargetConfiguration : IEntityTypeConfiguration<NutrientTarget>
{
    public void Configure(EntityTypeBuilder<NutrientTarget> b)
    {
        b.ToTable("T栄養目標", t =>
        {
            t.HasCheckConstraint("CK_栄養目標_性別", "`性別` IN ('male','female','all')");
            t.HasCheckConstraint("CK_栄養目標_種別", "`種別` IN ('RDA','AI','DG_MIN','DG_MAX','UL')");
            t.HasCheckConstraint("CK_栄養目標_単位基準", "`単位基準` IN ('PER_DAY','PER_KG_BW','PCT_ENERGY')");
            t.HasCheckConstraint("CK_栄養目標_年齢", "`年齢下限` <= `年齢上限`");
            t.HasCheckConstraint("CK_栄養目標_非負", "`目標値` >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("ID").HasColumnType("bigint").ValueGeneratedOnAdd();
        b.Property(x => x.NutrientId).HasColumnName("栄養素ID").HasColumnType("smallint");
        b.Property(x => x.Sex).HasColumnName("性別").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.AgeMin).HasColumnName("年齢下限").HasColumnType("smallint");
        b.Property(x => x.AgeMax).HasColumnName("年齢上限").HasColumnType("smallint");
        b.Property(x => x.Kind).HasColumnName("種別").HasColumnType("varchar(10)").IsRequired();
        b.Property(x => x.Amount).HasColumnName("目標値").HasColumnType("decimal(12,4)");
        b.Property(x => x.Basis).HasColumnName("単位基準").HasColumnType("varchar(15)").IsRequired();
        b.Property(x => x.SourceNote).HasColumnName("出典メモ").HasColumnType("varchar(200)");
        b.HasOne(x => x.Nutrient).WithMany().HasForeignKey(x => x.NutrientId)
            .HasConstraintName("FK_栄養目標_栄養素").OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.NutrientId, x.Sex, x.AgeMin, x.AgeMax, x.Kind }).IsUnique().HasDatabaseName("UQ_栄養目標_条件");
        b.ConfigureAudit();
    }
}
