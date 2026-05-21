using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Frigorino.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Persist_Last_Active_Household : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastActiveHouseholdId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_LastActiveHouseholdId",
                table: "Users",
                column: "LastActiveHouseholdId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Households_LastActiveHouseholdId",
                table: "Users",
                column: "LastActiveHouseholdId",
                principalTable: "Households",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Households_LastActiveHouseholdId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_LastActiveHouseholdId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastActiveHouseholdId",
                table: "Users");
        }
    }
}
