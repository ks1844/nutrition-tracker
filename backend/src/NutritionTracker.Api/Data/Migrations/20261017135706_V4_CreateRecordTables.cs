using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NutritionTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V4_CreateRecordTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "T食事記録",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ユーザーID = table.Column<long>(type: "bigint", nullable: false),
                    食事日 = table.Column<DateOnly>(type: "date", nullable: false),
                    食事区分 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    メモ = table.Column<string>(type: "varchar(500)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食事記録", x => x.ID);
                    table.CheckConstraint("CK_食事記録_食事区分", "`食事区分` IN ('breakfast','lunch','dinner','snack')");
                    table.ForeignKey(
                        name: "FK_食事記録_ユーザー",
                        column: x => x.ユーザーID,
                        principalTable: "Tユーザー",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T食事記録項目",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    食事記録ID = table.Column<long>(type: "bigint", nullable: false),
                    メニューID = table.Column<long>(type: "bigint", nullable: true),
                    食材ID = table.Column<long>(type: "bigint", nullable: true),
                    食材単位ID = table.Column<long>(type: "bigint", nullable: true),
                    入力方式 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    入力値 = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    食べた割合 = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    重量g = table.Column<decimal>(type: "decimal(8,2)", nullable: true),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食事記録項目", x => x.ID);
                    table.CheckConstraint("CK_食事記録項目_メニュー", "`メニューID` IS NULL OR (`入力方式` IN ('RATIO','SERVING','GRAM') AND `食べた割合` IS NOT NULL AND `食べた割合` > 0)");
                    table.CheckConstraint("CK_食事記録項目_入力値", "`入力値` > 0");
                    table.CheckConstraint("CK_食事記録項目_入力方式", "`入力方式` IN ('RATIO','SERVING','GRAM','UNIT')");
                    table.CheckConstraint("CK_食事記録項目_対象", "(`メニューID` IS NULL) <> (`食材ID` IS NULL)");
                    table.CheckConstraint("CK_食事記録項目_食材", "`食材ID` IS NULL OR (`入力方式` IN ('GRAM','UNIT') AND `重量g` IS NOT NULL AND `重量g` > 0)");
                    table.ForeignKey(
                        name: "FK_食事記録項目_メニュー",
                        column: x => x.メニューID,
                        principalTable: "Tメニュー",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_食事記録項目_食事記録",
                        column: x => x.食事記録ID,
                        principalTable: "T食事記録",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_食事記録項目_食材",
                        column: x => x.食材ID,
                        principalTable: "T食材",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_食事記録項目_食材単位",
                        column: x => x.食材単位ID,
                        principalTable: "T食材単位",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T食事記録項目栄養素",
                columns: table => new
                {
                    食事記録項目ID = table.Column<long>(type: "bigint", nullable: false),
                    栄養素ID = table.Column<short>(type: "smallint", nullable: false),
                    量 = table.Column<decimal>(type: "decimal(14,4)", nullable: false),
                    不完全フラグ = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食事記録項目栄養素", x => new { x.食事記録項目ID, x.栄養素ID });
                    table.ForeignKey(
                        name: "FK_食事記録項目栄養素_栄養素",
                        column: x => x.栄養素ID,
                        principalTable: "T栄養素",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_食事記録項目栄養素_食事記録項目",
                        column: x => x.食事記録項目ID,
                        principalTable: "T食事記録項目",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_食事記録_ユーザーID_食事日",
                table: "T食事記録",
                columns: new[] { "ユーザーID", "食事日" });

            migrationBuilder.CreateIndex(
                name: "IX_T食事記録項目_食材ID",
                table: "T食事記録項目",
                column: "食材ID");

            migrationBuilder.CreateIndex(
                name: "IX_T食事記録項目_食材単位ID",
                table: "T食事記録項目",
                column: "食材単位ID");

            migrationBuilder.CreateIndex(
                name: "IX_食事記録項目_メニューID",
                table: "T食事記録項目",
                column: "メニューID");

            migrationBuilder.CreateIndex(
                name: "IX_食事記録項目_食事記録ID",
                table: "T食事記録項目",
                column: "食事記録ID");

            migrationBuilder.CreateIndex(
                name: "IX_T食事記録項目栄養素_栄養素ID",
                table: "T食事記録項目栄養素",
                column: "栄養素ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "T食事記録項目栄養素");

            migrationBuilder.DropTable(
                name: "T食事記録項目");

            migrationBuilder.DropTable(
                name: "T食事記録");
        }
    }
}
