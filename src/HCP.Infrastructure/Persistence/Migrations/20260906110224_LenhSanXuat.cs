using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LenhSanXuat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LenhSanXuat",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaLenh = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaLoThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HanSuDungThanhPham = table.Column<DateOnly>(type: "date", nullable: true),
                    NgaySanXuat = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_LenhSanXuat", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LenhSanXuatTieuHao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LenhSanXuatId = table.Column<int>(type: "int", nullable: false),
                    MaNguyenLieu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaLo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LenhSanXuatTieuHao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LenhSanXuatTieuHao_LenhSanXuat_LenhSanXuatId",
                        column: x => x.LenhSanXuatId,
                        principalTable: "LenhSanXuat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LenhSanXuat_MaLenh",
                table: "LenhSanXuat",
                columns: new[] { "MaLenh", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LenhSanXuatTieuHao_LenhSanXuatId",
                table: "LenhSanXuatTieuHao",
                column: "LenhSanXuatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LenhSanXuatTieuHao");

            migrationBuilder.DropTable(
                name: "LenhSanXuat");
        }
    }
}
