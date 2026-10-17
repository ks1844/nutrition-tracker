using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V3_CreateMenuTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tメニュー",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ユーザーID = table.Column<long>(type: "bigint", nullable: false),
                    名称 = table.Column<string>(type: "varchar(100)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    完成後重量g = table.Column<decimal>(type: "decimal(8,1)", nullable: false),
                    杯数 = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    杯ラベル = table.Column<string>(type: "varchar(20)", nullable: false, defaultValue: "お椀")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    調理日 = table.Column<DateOnly>(type: "date", nullable: true),
                    メモ = table.Column<string>(type: "varchar(500)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    削除日時 = table.Column<DateTime>(type: "datetime", nullable: true),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tメニュー", x => x.ID);
                    table.CheckConstraint("CK_メニュー_完成後重量", "`完成後重量g` > 0");
                    table.CheckConstraint("CK_メニュー_杯数", "`杯数` > 0");
                    table.ForeignKey(
                        name: "FK_メニュー_ユーザー",
                        column: x => x.ユーザーID,
                        principalTable: "Tユーザー",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Tメニュー食材",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    メニューID = table.Column<long>(type: "bigint", nullable: false),
                    食材ID = table.Column<long>(type: "bigint", nullable: false),
                    食材単位ID = table.Column<long>(type: "bigint", nullable: true),
                    数量 = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    重量g = table.Column<decimal>(type: "decimal(8,2)", nullable: false),
                    並び順 = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tメニュー食材", x => x.ID);
                    table.CheckConstraint("CK_メニュー食材_換算", "`食材単位ID` IS NOT NULL OR `重量g` = `数量`");
                    table.CheckConstraint("CK_メニュー食材_数量", "`数量` > 0");
                    table.CheckConstraint("CK_メニュー食材_重量", "`重量g` > 0");
                    table.ForeignKey(
                        name: "FK_メニュー食材_メニュー",
                        column: x => x.メニューID,
                        principalTable: "Tメニュー",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_メニュー食材_食材",
                        column: x => x.食材ID,
                        principalTable: "T食材",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_メニュー食材_食材単位",
                        column: x => x.食材単位ID,
                        principalTable: "T食材単位",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_メニュー_ユーザーID_削除日時",
                table: "Tメニュー",
                columns: new[] { "ユーザーID", "削除日時" });

            migrationBuilder.CreateIndex(
                name: "IX_Tメニュー食材_食材ID",
                table: "Tメニュー食材",
                column: "食材ID");

            migrationBuilder.CreateIndex(
                name: "IX_Tメニュー食材_食材単位ID",
                table: "Tメニュー食材",
                column: "食材単位ID");

            migrationBuilder.CreateIndex(
                name: "IX_メニュー食材_メニューID",
                table: "Tメニュー食材",
                column: "メニューID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tメニュー食材");

            migrationBuilder.DropTable(
                name: "Tメニュー");
        }
    }
}
