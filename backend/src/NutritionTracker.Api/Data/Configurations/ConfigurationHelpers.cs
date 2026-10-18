using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NutritionTracker.Api.Entities;

namespace NutritionTracker.Api.Data.Configurations;

/// <summary>
/// テーブル・列の日本語名と共通の列定義のための補助です（docs/05.テーブル定義書.md §1）。
/// </summary>
internal static class ConfigurationHelpers
{
    /// <summary>
    /// 全テーブル共通の「作成日時」「更新日時」の列を設定します。
    /// 値は DB の既定値（CURRENT_TIMESTAMP。更新日時は更新のたびに更新）に任せます。
    /// </summary>
    /// <param name="b">対象テーブルの設定ビルダー。</param>
    /// <typeparam name="T">監査列を持つエンティティ。</typeparam>
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> b) where T : AuditableEntity
    {
        b.Property(x => x.CreatedAt).HasColumnName("作成日時").HasColumnType("datetime")
            .HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAdd();
        b.Property(x => x.UpdatedAt).HasColumnName("更新日時").HasColumnType("datetime")
            .HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedOnAddOrUpdate();
    }
}
