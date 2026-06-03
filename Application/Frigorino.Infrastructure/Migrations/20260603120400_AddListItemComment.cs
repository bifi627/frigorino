using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListItemComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "ListItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Comment",
                table: "ListItems");
        }
    }
}
