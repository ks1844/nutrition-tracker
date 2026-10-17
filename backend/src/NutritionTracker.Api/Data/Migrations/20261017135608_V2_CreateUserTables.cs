using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V2_CreateUserTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tユーザー",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tユーザー", x => x.ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T栄養目標",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    栄養素ID = table.Column<short>(type: "smallint", nullable: false),
                    性別 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    年齢下限 = table.Column<short>(type: "smallint", nullable: false),
                    年齢上限 = table.Column<short>(type: "smallint", nullable: false),
                    種別 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    目標値 = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    単位基準 = table.Column<string>(type: "varchar(15)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    出典メモ = table.Column<string>(type: "varchar(200)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T栄養目標", x => x.ID);
                    table.CheckConstraint("CK_栄養目標_単位基準", "`単位基準` IN ('PER_DAY','PER_KG_BW','PCT_ENERGY')");
                    table.CheckConstraint("CK_栄養目標_年齢", "`年齢下限` <= `年齢上限`");
                    table.CheckConstraint("CK_栄養目標_性別", "`性別` IN ('male','female','all')");
                    table.CheckConstraint("CK_栄養目標_種別", "`種別` IN ('RDA','AI','DG_MIN','DG_MAX','UL')");
                    table.CheckConstraint("CK_栄養目標_非負", "`目標値` >= 0");
                    table.ForeignKey(
                        name: "FK_栄養目標_栄養素",
                        column: x => x.栄養素ID,
                        principalTable: "T栄養素",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tユーザープロフィール",
                columns: table => new
                {
                    ユーザーID = table.Column<long>(type: "bigint", nullable: false),
                    性別 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    生年月日 = table.Column<DateOnly>(type: "date", nullable: false),
                    身長cm = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    体重kg = table.Column<decimal>(type: "decimal(4,1)", nullable: false),
                    活動レベル = table.Column<short>(type: "smallint", nullable: false),
                    目的 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    エネルギー目標kcal = table.Column<int>(type: "int", nullable: true),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tユーザープロフィール", x => x.ユーザーID);
                    table.CheckConstraint("CK_ユーザープロフィール_性別", "`性別` IN ('male','female')");
                    table.CheckConstraint("CK_ユーザープロフィール_活動レベル", "`活動レベル` BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_ユーザープロフィール_目標", "`エネルギー目標kcal` IS NULL OR `エネルギー目標kcal` > 0");
                    table.CheckConstraint("CK_ユーザープロフィール_目的", "`目的` IN ('skin','muscle','both')");
                    table.CheckConstraint("CK_ユーザープロフィール_身体", "`身長cm` > 0 AND `体重kg` > 0");
                    table.ForeignKey(
                        name: "FK_ユーザープロフィール_ユーザー",
                        column: x => x.ユーザーID,
                        principalTable: "Tユーザー",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Tユーザー",
                columns: new[] { "ID", "作成日時" },
                values: new object[] { 1L, new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "UQ_栄養目標_条件",
                table: "T栄養目標",
                columns: new[] { "栄養素ID", "性別", "年齢下限", "年齢上限", "種別" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tユーザープロフィール");

            migrationBuilder.DropTable(
                name: "T栄養目標");

            migrationBuilder.DropTable(
                name: "Tユーザー");
        }
    }
}
