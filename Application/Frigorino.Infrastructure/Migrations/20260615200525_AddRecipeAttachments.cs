using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeAttachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ThumbnailStorageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Caption = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    Rank = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeAttachments_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAttachments_IsActive",
                table: "RecipeAttachments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAttachments_RecipeId",
                table: "RecipeAttachments",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeAttachments_RecipeId_IsActive",
                table: "RecipeAttachments",
                columns: new[] { "RecipeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_RecipeAttachments_RecipeId_Rank_Active",
                table: "RecipeAttachments",
                columns: new[] { "RecipeId", "Rank" },
                unique: true,
                filter: "\"IsActive\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecipeAttachments");
        }
    }
}
