using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhieuNguoiLapTaiKhoan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NguoiLapUserId",
                table: "PhieuGhiNhan",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenNguoiLap",
                table: "PhieuGhiNhan",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            // Phiếu cũ: điền tên người lập theo hồ sơ nhân sự (cùng cơ sở).
            migrationBuilder.Sql(@"UPDATE p SET TenNguoiLap = s.HoTen
FROM PhieuGhiNhan p JOIN Staff s ON s.MaNhanSu = p.NguoiLap AND s.TenantId = p.TenantId
WHERE p.TenNguoiLap IS NULL AND p.NguoiLap IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NguoiLapUserId",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "TenNguoiLap",
                table: "PhieuGhiNhan");
        }
    }
}
