using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangNhanChiTiet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaThucDon",
                table: "DonHangNhanDong",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaTruyVet",
                table: "DonHangNhanDong",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SoLuong",
                table: "DonHangNhanDong",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DaLayChiTiet",
                table: "DonHangNhan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DiaChiGiao",
                table: "DonHangNhan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaNguoiGiao",
                table: "DonHangNhan",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DonHangNhanPhanBo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DonHangNhanDongId = table.Column<int>(type: "int", nullable: false),
                    MaThucPhamNcc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaLo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangNhanPhanBo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonHangNhanPhanBo_DonHangNhanDong_DonHangNhanDongId",
                        column: x => x.DonHangNhanDongId,
                        principalTable: "DonHangNhanDong",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonHangNhanPhanBo_DonHangNhanDongId",
                table: "DonHangNhanPhanBo",
                column: "DonHangNhanDongId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonHangNhanPhanBo");

            migrationBuilder.DropColumn(
                name: "MaThucDon",
                table: "DonHangNhanDong");

            migrationBuilder.DropColumn(
                name: "MaTruyVet",
                table: "DonHangNhanDong");

            migrationBuilder.DropColumn(
                name: "SoLuong",
                table: "DonHangNhanDong");

            migrationBuilder.DropColumn(
                name: "DaLayChiTiet",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "DiaChiGiao",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "MaNguoiGiao",
                table: "DonHangNhan");
        }
    }
}
