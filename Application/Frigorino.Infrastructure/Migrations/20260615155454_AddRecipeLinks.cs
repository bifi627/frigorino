using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Rank = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeLinks_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLinks_IsActive",
                table: "RecipeLinks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLinks_RecipeId",
                table: "RecipeLinks",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeLinks_RecipeId_IsActive",
                table: "RecipeLinks",
                columns: new[] { "RecipeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_RecipeLinks_RecipeId_Rank_Active",
                table: "RecipeLinks",
                columns: new[] { "RecipeId", "Rank" },
                unique: true,
                filter: "\"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeLinks");
        }
    }
}
