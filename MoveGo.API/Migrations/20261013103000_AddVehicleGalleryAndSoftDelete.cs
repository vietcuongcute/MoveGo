using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoveGo.API.Data;

#nullable disable

namespace MoveGo.API.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261013103000_AddVehicleGalleryAndSoftDelete")]
    public partial class AddVehicleGalleryAndSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Gallery",
                table: "Vehicles",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Vehicles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Xe cũ đang có ImageUrl: đưa vào bộ ảnh để hiện đúng ngay sau khi nâng cấp.
            migrationBuilder.Sql("UPDATE \"Vehicles\" SET \"Gallery\" = \"ImageUrl\" WHERE \"ImageUrl\" IS NOT NULL AND \"ImageUrl\" <> '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Gallery",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Vehicles");
        }
    }
}
