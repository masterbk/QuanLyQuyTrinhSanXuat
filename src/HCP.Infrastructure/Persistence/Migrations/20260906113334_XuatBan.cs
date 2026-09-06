using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class XuatBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TonToiThieu",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KhachHangs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaKhachHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenKhachHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Loai = table.Column<int>(type: "int", nullable: false),
                    DienThoai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KhachHangs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhieuXuatBan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaPhieu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaKhachHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NgayXuat = table.Column<DateOnly>(type: "date", nullable: false),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    ThoiGianHoanThanhUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuXuatBan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhieuXuatBanChiTiet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhieuXuatBanId = table.Column<int>(type: "int", nullable: false),
                    MaThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuXuatBanChiTiet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhieuXuatBanChiTiet_PhieuXuatBan_PhieuXuatBanId",
                        column: x => x.PhieuXuatBanId,
                        principalTable: "PhieuXuatBan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KhachHangs_MaKhachHang",
                table: "KhachHangs",
                columns: new[] { "MaKhachHang", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuXuatBan_MaPhieu",
                table: "PhieuXuatBan",
                columns: new[] { "MaPhieu", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuXuatBanChiTiet_PhieuXuatBanId_MaThanhPham",
                table: "PhieuXuatBanChiTiet",
                columns: new[] { "PhieuXuatBanId", "MaThanhPham", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KhachHangs");

            migrationBuilder.DropTable(
                name: "PhieuXuatBanChiTiet");

            migrationBuilder.DropTable(
                name: "PhieuXuatBan");

            migrationBuilder.DropColumn(
                name: "TonToiThieu",
                table: "Products");
        }
    }
}
