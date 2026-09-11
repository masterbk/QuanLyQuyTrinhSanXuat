using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangNhanChiTietThat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaPhieuXuat",
                table: "DonHangNhanPhanBo",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenKho",
                table: "DonHangNhanPhanBo",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenLo",
                table: "DonHangNhanPhanBo",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DonViTinh",
                table: "DonHangNhanDong",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileUrl",
                table: "DonHangNhanDong",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            // Chi tiết đơn đã lưu trước đây được đọc bằng tên trường ĐOÁN SAI (số lượng, lô, kho về null).
            // Đánh dấu chưa lấy chi tiết để job kéo lại - kể cả đơn đã chốt vốn không bao giờ kéo lại.
            migrationBuilder.Sql("UPDATE [DonHangNhan] SET [DaLayChiTiet] = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaPhieuXuat",
                table: "DonHangNhanPhanBo");

            migrationBuilder.DropColumn(
                name: "TenKho",
                table: "DonHangNhanPhanBo");

            migrationBuilder.DropColumn(
                name: "TenLo",
                table: "DonHangNhanPhanBo");

            migrationBuilder.DropColumn(
                name: "DonViTinh",
                table: "DonHangNhanDong");

            migrationBuilder.DropColumn(
                name: "FileUrl",
                table: "DonHangNhanDong");
        }
    }
}
