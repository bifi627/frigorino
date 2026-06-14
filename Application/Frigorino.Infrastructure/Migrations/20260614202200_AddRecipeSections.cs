using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // (1) Drop the old per-recipe unique index — rank becomes per-section below.
            migrationBuilder.DropIndex(
                name: "UX_RecipeItems_RecipeId_Rank_Active",
                table: "RecipeItems");

            // (2) Create the RecipeSections table (and its own indexes) first, so the backfill can
            //     insert default-section rows before we point items at them.
            migrationBuilder.CreateTable(
                name: "RecipeSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RecipeId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Rank = table.Column<string>(type: "text", nullable: false, collation: "C"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeSections_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSections_IsActive",
                table: "RecipeSections",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSections_RecipeId",
                table: "RecipeSections",
                column: "RecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeSections_RecipeId_IsActive",
                table: "RecipeSections",
                columns: new[] { "RecipeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_RecipeSections_RecipeId_Rank_Active",
                table: "RecipeSections",
                columns: new[] { "RecipeId", "Rank" },
                unique: true,
                filter: "\"IsActive\"");

            // (3) Add SectionId as NULLABLE first (scaffold emitted NOT NULL DEFAULT 0).
            migrationBuilder.AddColumn<int>(
                name: "SectionId",
                table: "RecipeItems",
                type: "integer",
                nullable: true);

            // (4) Backfill: one default section per recipe, then point its items at it.
            //     'a0' is FractionalIndex.GenerateKeyBetween(null, null) — the initial rank.
            //     Raw SQL because the column is about to become required (LINQ would mis-handle the
            //     IS NULL on a required-mapped column — see reference_ef_isnull_pruned_on_required_column).
            //     A recipe with zero items still gets a default section row (INSERT is per-recipe).
            migrationBuilder.Sql("""
                WITH inserted AS (
                    INSERT INTO "RecipeSections" ("RecipeId", "Name", "Description", "Rank", "CreatedAt", "UpdatedAt", "IsActive")
                    SELECT r."Id", NULL, NULL, 'a0', now(), now(), TRUE
                    FROM "Recipes" r
                    RETURNING "Id", "RecipeId"
                )
                UPDATE "RecipeItems" ri
                SET "SectionId" = i."Id"
                FROM inserted i
                WHERE ri."RecipeId" = i."RecipeId";
                """);

            // (5) Now enforce NOT NULL.
            migrationBuilder.AlterColumn<int>(
                name: "SectionId",
                table: "RecipeItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // (6) Create the per-section item indexes + FK (after the backfill so no orphan SectionId).
            migrationBuilder.CreateIndex(
                name: "IX_RecipeItems_SectionId",
                table: "RecipeItems",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItems_SectionId_IsActive",
                table: "RecipeItems",
                columns: new[] { "SectionId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_RecipeItems_SectionId_Rank_Active",
                table: "RecipeItems",
                columns: new[] { "SectionId", "Rank" },
                unique: true,
                filter: "\"IsActive\"");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipeItems_RecipeSections_SectionId",
                table: "RecipeItems",
                column: "SectionId",
                principalTable: "RecipeSections",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipeItems_RecipeSections_SectionId",
                table: "RecipeItems");

            migrationBuilder.DropTable(
                name: "RecipeSections");

            migrationBuilder.DropIndex(
                name: "IX_RecipeItems_SectionId",
                table: "RecipeItems");

            migrationBuilder.DropIndex(
                name: "IX_RecipeItems_SectionId_IsActive",
                table: "RecipeItems");

            migrationBuilder.DropIndex(
                name: "UX_RecipeItems_SectionId_Rank_Active",
                table: "RecipeItems");

            migrationBuilder.DropColumn(
                name: "SectionId",
                table: "RecipeItems");

            migrationBuilder.CreateIndex(
                name: "UX_RecipeItems_RecipeId_Rank_Active",
                table: "RecipeItems",
                columns: new[] { "RecipeId", "Rank" },
                unique: true,
                filter: "\"IsActive\"");
        }
    }
}
