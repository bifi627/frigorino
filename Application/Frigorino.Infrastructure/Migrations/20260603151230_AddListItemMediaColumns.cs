using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListItemMediaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "ListItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "ListItems",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "ListItems",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageKey",
                table: "ListItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStorageKey",
                table: "ListItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "ListItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "StorageKey",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "ThumbnailStorageKey",
                table: "ListItems");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "ListItems");
        }
    }
}
