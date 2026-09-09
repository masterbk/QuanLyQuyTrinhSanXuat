using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangDongMaSanPham : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaSanPham",
                table: "OrderLines",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaSanPham",
                table: "OrderLines");
        }
    }
}
