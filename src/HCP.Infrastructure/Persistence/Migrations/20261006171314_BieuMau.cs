using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BieuMau : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BieuMau",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaHieu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    BoCuc = table.Column<int>(type: "int", nullable: false),
                    TanSuat = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NhomQuyen = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    GhiChuChan = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KichHoat = table.Column<bool>(type: "bit", nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BieuMau", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HangMucBieuMau",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BieuMauId = table.Column<int>(type: "int", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DienGiai = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TanSuat = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HangMucBieuMau", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HangMucBieuMau_BieuMau_BieuMauId",
                        column: x => x.BieuMauId,
                        principalTable: "BieuMau",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhieuGhiNhan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BieuMauId = table.Column<int>(type: "int", nullable: false),
                    Ngay = table.Column<DateOnly>(type: "date", nullable: false),
                    Ca = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    KhuVuc = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NguoiLap = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NguoiThamTra = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ThoiGianThamTraUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KetQuaThamTra = table.Column<bool>(type: "bit", nullable: true),
                    ThoiGianUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuGhiNhan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhieuGhiNhan_BieuMau_BieuMauId",
                        column: x => x.BieuMauId,
                        principalTable: "BieuMau",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TruongBieuMau",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BieuMauId = table.Column<int>(type: "int", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Ma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kieu = table.Column<int>(type: "int", nullable: false),
                    BatBuoc = table.Column<bool>(type: "bit", nullable: false),
                    DonVi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    GiaTriChuan = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TuyChonCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Nhom = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TruongBieuMau", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TruongBieuMau_BieuMau_BieuMauId",
                        column: x => x.BieuMauId,
                        principalTable: "BieuMau",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DongGhiNhan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhieuGhiNhanId = table.Column<int>(type: "int", nullable: false),
                    HangMucBieuMauId = table.Column<int>(type: "int", nullable: true),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    GiaTriJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DongGhiNhan", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DongGhiNhan_PhieuGhiNhan_PhieuGhiNhanId",
                        column: x => x.PhieuGhiNhanId,
                        principalTable: "PhieuGhiNhan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BieuMau_MaHieu",
                table: "BieuMau",
                columns: new[] { "MaHieu", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DongGhiNhan_PhieuGhiNhanId",
                table: "DongGhiNhan",
                column: "PhieuGhiNhanId");

            migrationBuilder.CreateIndex(
                name: "IX_HangMucBieuMau_BieuMauId",
                table: "HangMucBieuMau",
                column: "BieuMauId");

            migrationBuilder.CreateIndex(
                name: "IX_PhieuGhiNhan_BieuMauId_Ngay",
                table: "PhieuGhiNhan",
                columns: new[] { "BieuMauId", "Ngay" });

            migrationBuilder.CreateIndex(
                name: "IX_TruongBieuMau_BieuMauId",
                table: "TruongBieuMau",
                column: "BieuMauId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DongGhiNhan");

            migrationBuilder.DropTable(
                name: "HangMucBieuMau");

            migrationBuilder.DropTable(
                name: "TruongBieuMau");

            migrationBuilder.DropTable(
                name: "PhieuGhiNhan");

            migrationBuilder.DropTable(
                name: "BieuMau");
        }
    }
}
