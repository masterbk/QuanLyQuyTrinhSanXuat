using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangNhan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonHangNhan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    MaDonHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenTruong = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NgayGiao = table.Column<DateOnly>(type: "date", nullable: true),
                    LanDongBoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangNhan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonHangNhanDong",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DonHangNhanId = table.Column<int>(type: "int", nullable: false),
                    MaSanPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenSanPham = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangNhanDong", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonHangNhanDong_DonHangNhan_DonHangNhanId",
                        column: x => x.DonHangNhanId,
                        principalTable: "DonHangNhan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonHangNhan_TenantId_MaDonHang",
                table: "DonHangNhan",
                columns: new[] { "TenantId", "MaDonHang" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonHangNhanDong_DonHangNhanId",
                table: "DonHangNhanDong",
                column: "DonHangNhanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonHangNhanDong");

            migrationBuilder.DropTable(
                name: "DonHangNhan");
        }
    }
}
