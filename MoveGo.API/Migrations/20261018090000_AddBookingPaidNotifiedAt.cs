using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MoveGo.API.Data;

#nullable disable

namespace MoveGo.API.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261018090000_AddBookingPaidNotifiedAt")]
    public partial class AddBookingPaidNotifiedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PaidNotifiedAt",
                table: "Bookings",
                type: "timestamp without time zone",
                nullable: true);

            // Đơn đã cọc từ trước khi có cột này: tin báo chủ xe đã được gửi lúc khách cọc → đánh dấu đã báo.
            migrationBuilder.Sql(
                "UPDATE \"Bookings\" SET \"PaidNotifiedAt\" = \"PaidAt\" WHERE \"Status\" IN ('Confirmed','Completed') AND \"PaidAt\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidNotifiedAt",
                table: "Bookings");
        }
    }
}
