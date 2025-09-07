using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Article_Classifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassificationId",
                table: "ListItems",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArticleClassifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OriginalName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    ExpirationDuration = table.Column<int>(type: "integer", nullable: false),
                    HintCategory = table.Column<string>(type: "text", nullable: false),
                    HintEstimation = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArticleClassifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ListItems_ClassificationId",
                table: "ListItems",
                column: "ClassificationId");

            migrationBuilder.CreateIndex(
                name: "IX_ArticleClassifications_OriginalName",
                table: "ArticleClassifications",
                column: "OriginalName");

            migrationBuilder.AddForeignKey(
                name: "FK_ListItems_ArticleClassifications_ClassificationId",
                table: "ListItems",
                column: "ClassificationId",
                principalTable: "ArticleClassifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ListItems_ArticleClassifications_ClassificationId",
                table: "ListItems");

            migrationBuilder.DropTable(
                name: "ArticleClassifications");

            migrationBuilder.DropIndex(
                name: "IX_ListItems_ClassificationId",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "ClassificationId",
                table: "ListItems");
        }
    }
}
