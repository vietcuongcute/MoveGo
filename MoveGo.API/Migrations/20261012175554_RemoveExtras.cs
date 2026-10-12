using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MoveGo.API.Migrations
{
    /// <inheritdoc />
    public partial class RemoveExtras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtraCodes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExtrasAmount",
                table: "Bookings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtraCodes",
                table: "Bookings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "ExtrasAmount",
                table: "Bookings",
                type: "numeric(18,0)",
                precision: 18,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
