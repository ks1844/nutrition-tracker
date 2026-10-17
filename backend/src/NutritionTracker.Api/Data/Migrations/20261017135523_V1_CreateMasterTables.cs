using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NutritionTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class V1_CreateMasterTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T栄養素",
                columns: table => new
                {
                    ID = table.Column<short>(type: "smallint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    栄養素コード = table.Column<string>(type: "varchar(20)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    名称 = table.Column<string>(type: "varchar(50)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    単位 = table.Column<string>(type: "varchar(10)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    区分 = table.Column<string>(type: "varchar(20)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    参考値フラグ = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    表示順 = table.Column<short>(type: "smallint", nullable: false),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T栄養素", x => x.ID);
                    table.CheckConstraint("CK_栄養素_区分", "`区分` IN ('macro','vitamin','mineral','antioxidant')");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T食材",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    食品コード = table.Column<string>(type: "varchar(20)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    名称 = table.Column<string>(type: "varchar(100)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    正式名称 = table.Column<string>(type: "varchar(200)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    読み仮名 = table.Column<string>(type: "varchar(100)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    食品群 = table.Column<string>(type: "varchar(50)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    廃棄率 = table.Column<decimal>(type: "decimal(4,1)", nullable: false, defaultValue: 0m),
                    出典版 = table.Column<string>(type: "varchar(50)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    有効フラグ = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食材", x => x.ID);
                    table.CheckConstraint("CK_食材_廃棄率", "`廃棄率` >= 0 AND `廃棄率` < 100");
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T食材別名",
                columns: table => new
                {
                    食材ID = table.Column<long>(type: "bigint", nullable: false),
                    別名 = table.Column<string>(type: "varchar(100)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食材別名", x => new { x.食材ID, x.別名 });
                    table.ForeignKey(
                        name: "FK_食材別名_食材",
                        column: x => x.食材ID,
                        principalTable: "T食材",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T食材単位",
                columns: table => new
                {
                    ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    食材ID = table.Column<long>(type: "bigint", nullable: false),
                    単位名 = table.Column<string>(type: "varchar(30)", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    _1単位あたりグラム = table.Column<decimal>(name: "1単位あたりグラム", type: "decimal(8,2)", nullable: false),
                    既定フラグ = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    既定一意キー = table.Column<long>(type: "bigint", nullable: true, computedColumnSql: "IF(`既定フラグ` = 1, `食材ID`, NULL)", stored: true),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食材単位", x => x.ID);
                    table.CheckConstraint("CK_食材単位_グラム", "`1単位あたりグラム` > 0");
                    table.ForeignKey(
                        name: "FK_食材単位_食材",
                        column: x => x.食材ID,
                        principalTable: "T食材",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "T食材栄養素",
                columns: table => new
                {
                    食材ID = table.Column<long>(type: "bigint", nullable: false),
                    栄養素ID = table.Column<short>(type: "smallint", nullable: false),
                    _100gあたり含有量 = table.Column<decimal>(name: "100gあたり含有量", type: "decimal(12,4)", nullable: true),
                    微量フラグ = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    出典メモ = table.Column<string>(type: "varchar(200)", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    作成日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    更新日時 = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_T食材栄養素", x => new { x.食材ID, x.栄養素ID });
                    table.CheckConstraint("CK_食材栄養素_微量", "`微量フラグ` = 0 OR COALESCE(`100gあたり含有量`, -1) = 0");
                    table.CheckConstraint("CK_食材栄養素_非負", "`100gあたり含有量` IS NULL OR `100gあたり含有量` >= 0");
                    table.ForeignKey(
                        name: "FK_食材栄養素_栄養素",
                        column: x => x.栄養素ID,
                        principalTable: "T栄養素",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_食材栄養素_食材",
                        column: x => x.食材ID,
                        principalTable: "T食材",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "T栄養素",
                columns: new[] { "ID", "区分", "栄養素コード", "作成日時", "表示順", "名称", "単位" },
                values: new object[,]
                {
                    { (short)1, "macro", "ENERGY", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)1, "エネルギー", "kcal" },
                    { (short)2, "macro", "PROT", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)2, "タンパク質", "g" },
                    { (short)3, "macro", "FAT", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)3, "脂質", "g" },
                    { (short)4, "macro", "CHO", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)4, "炭水化物", "g" },
                    { (short)5, "macro", "FIBTG", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)5, "食物繊維総量", "g" },
                    { (short)6, "mineral", "NACL_EQ", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)6, "食塩相当量", "g" },
                    { (short)7, "mineral", "K", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)7, "カリウム", "mg" },
                    { (short)8, "mineral", "CA", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)8, "カルシウム", "mg" },
                    { (short)9, "mineral", "MG", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)9, "マグネシウム", "mg" },
                    { (short)10, "mineral", "FE", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)10, "鉄", "mg" },
                    { (short)11, "mineral", "ZN", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)11, "亜鉛", "mg" },
                    { (short)12, "vitamin", "VITA_RAE", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)12, "ビタミンA", "μg" },
                    { (short)13, "vitamin", "CARTB_EQ", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)13, "β-カロテン当量", "μg" },
                    { (short)14, "vitamin", "VITD", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)14, "ビタミンD", "μg" },
                    { (short)15, "vitamin", "VITE_ATOC", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)15, "ビタミンE", "mg" },
                    { (short)16, "vitamin", "VITK", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)16, "ビタミンK", "μg" },
                    { (short)17, "vitamin", "VITB1", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)17, "ビタミンB1", "mg" },
                    { (short)18, "vitamin", "VITB2", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)18, "ビタミンB2", "mg" },
                    { (short)19, "vitamin", "NIA", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)19, "ナイアシン", "mg" },
                    { (short)20, "vitamin", "VITB6", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)20, "ビタミンB6", "mg" },
                    { (short)21, "vitamin", "VITB12", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)21, "ビタミンB12", "μg" },
                    { (short)22, "vitamin", "FOL", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)22, "葉酸", "μg" },
                    { (short)23, "vitamin", "VITC", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)23, "ビタミンC", "mg" }
                });

            migrationBuilder.InsertData(
                table: "T栄養素",
                columns: new[] { "ID", "区分", "栄養素コード", "作成日時", "表示順", "参考値フラグ", "名称", "単位" },
                values: new object[] { (short)24, "antioxidant", "ASTX", new DateTime(2026, 10, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), (short)24, true, "アスタキサンチン", "mg" });

            migrationBuilder.CreateIndex(
                name: "UQ_栄養素_栄養素コード",
                table: "T栄養素",
                column: "栄養素コード",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_食材_名称",
                table: "T食材",
                column: "名称");

            migrationBuilder.CreateIndex(
                name: "IX_食材_読み仮名",
                table: "T食材",
                column: "読み仮名");

            migrationBuilder.CreateIndex(
                name: "UQ_食材_食品コード",
                table: "T食材",
                column: "食品コード",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_食材別名_別名",
                table: "T食材別名",
                column: "別名");

            migrationBuilder.CreateIndex(
                name: "UQ_食材単位_既定一意キー",
                table: "T食材単位",
                column: "既定一意キー",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_食材単位_食材ID_単位名",
                table: "T食材単位",
                columns: new[] { "食材ID", "単位名" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_T食材栄養素_栄養素ID",
                table: "T食材栄養素",
                column: "栄養素ID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "T食材別名");

            migrationBuilder.DropTable(
                name: "T食材単位");

            migrationBuilder.DropTable(
                name: "T食材栄養素");

            migrationBuilder.DropTable(
                name: "T栄養素");

            migrationBuilder.DropTable(
                name: "T食材");
        }
    }
}
