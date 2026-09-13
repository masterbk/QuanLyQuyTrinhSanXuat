using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangBan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderExports");

            migrationBuilder.DropTable(
                name: "OrderImages");

            migrationBuilder.DropTable(
                name: "OrderLines");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "MaTruongHnC",
                table: "KhachHangs",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DonHangBan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaDonHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaKhachHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NgayDat = table.Column<DateOnly>(type: "date", nullable: false),
                    NgayGiao = table.Column<DateOnly>(type: "date", nullable: true),
                    DiaChiGiao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaNguoiGiao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    Nguon = table.Column<int>(type: "int", nullable: false),
                    MaDonHnC = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LyDoHuy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ThoiGianXuatKhoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ThoiGianGiaoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ThoiGianHuyUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangBan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DonHangBanDong",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonHangBanId = table.Column<int>(type: "int", nullable: false),
                    MaThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DonGia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangBanDong", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonHangBanDong_DonHangBan_DonHangBanId",
                        column: x => x.DonHangBanId,
                        principalTable: "DonHangBan",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DonHangBanXuatLo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonHangBanDongId = table.Column<int>(type: "int", nullable: false),
                    MaLo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HanSuDung = table.Column<DateOnly>(type: "date", nullable: true),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonHangBanXuatLo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonHangBanXuatLo_DonHangBanDong_DonHangBanDongId",
                        column: x => x.DonHangBanDongId,
                        principalTable: "DonHangBanDong",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBan_MaDonHang",
                table: "DonHangBan",
                columns: new[] { "MaDonHang", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBan_MaKhachHang",
                table: "DonHangBan",
                column: "MaKhachHang");

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBanDong_DonHangBanId",
                table: "DonHangBanDong",
                column: "DonHangBanId");

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBanXuatLo_DonHangBanDongId",
                table: "DonHangBanXuatLo",
                column: "DonHangBanDongId");

            // ---- Chuyển phiếu xuất bán cũ sang đơn hàng bán (giữ lịch sử bán và lô đã xuất) ----
            // Phiếu "Hoàn thành" = đã trừ kho -> đơn "Đã giao"; phiếu "Mới tạo" -> "Chờ xác nhận". Đơn giá cũ không có -> 0.
            migrationBuilder.Sql(@"
INSERT INTO DonHangBan (MaDonHang, MaKhachHang, MaKho, NgayDat, TrangThai, Nguon, GhiChu,
                        ThoiGianXuatKhoUtc, ThoiGianGiaoUtc, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, TenantId)
SELECT p.MaPhieu, p.MaKhachHang, p.MaKho, p.NgayXuat, CASE WHEN p.TrangThai = 1 THEN 3 ELSE 0 END, 0, p.GhiChu,
       p.ThoiGianHoanThanhUtc, p.ThoiGianHoanThanhUtc, p.CreatedAtUtc, p.CreatedBy, p.UpdatedAtUtc, p.UpdatedBy, p.TenantId
FROM PhieuXuatBan p;

INSERT INTO DonHangBanDong (DonHangBanId, MaThanhPham, SoLuong, DonGia, CreatedAtUtc, CreatedBy, TenantId)
SELECT d.Id, c.MaThanhPham, c.SoLuong, 0, c.CreatedAtUtc, c.CreatedBy, c.TenantId
FROM PhieuXuatBanChiTiet c
JOIN PhieuXuatBan p ON p.Id = c.PhieuXuatBanId
JOIN DonHangBan d ON d.MaDonHang = p.MaPhieu AND d.TenantId = p.TenantId;

INSERT INTO DonHangBanXuatLo (DonHangBanDongId, MaLo, HanSuDung, SoLuong, CreatedAtUtc, TenantId)
SELECT dd.Id, g.MaLo, MAX(g.HanSuDung), -SUM(g.SoLuong), SYSUTCDATETIME(), dd.TenantId
FROM KhoGiaoDich g
JOIN DonHangBan d ON d.MaDonHang = g.ChungTu AND d.TenantId = g.TenantId
JOIN DonHangBanDong dd ON dd.DonHangBanId = d.Id AND dd.MaThanhPham = g.MaSanPham
WHERE g.Loai = 3
GROUP BY dd.Id, g.MaLo, dd.TenantId;

DELETE FROM SyncOutbox WHERE EntityType = 'Order';");

            migrationBuilder.DropTable(
                name: "PhieuXuatBanChiTiet");

            migrationBuilder.DropTable(
                name: "PhieuXuatBan");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonHangBanXuatLo");

            migrationBuilder.DropTable(
                name: "DonHangBanDong");

            migrationBuilder.DropTable(
                name: "DonHangBan");

            migrationBuilder.DropColumn(
                name: "MaTruongHnC",
                table: "KhachHangs");

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DiaChiNhan = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DiemGiao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LoaiDonHang = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaDonHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaNguoiGiao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaTruong = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NgayDonHang = table.Column<DateOnly>(type: "date", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TrangThai = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhieuXuatBan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaKhachHang = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaPhieu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NgayXuat = table.Column<DateOnly>(type: "date", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ThoiGianHoanThanhUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrangThai = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhieuXuatBan", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OrderExports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaLo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaLoaiSp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaSanPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaXuatKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderExports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderExports_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderImages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PathFile = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderImages_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaLoaiSp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaMonAn = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaSanPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    PathFile = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderLines_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PhieuXuatBanChiTiet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PhieuXuatBanId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                name: "IX_OrderExports_OrderId",
                table: "OrderExports",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderImages_OrderId_SortOrder",
                table: "OrderImages",
                columns: new[] { "OrderId", "SortOrder", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_MaDonHang",
                table: "Orders",
                columns: new[] { "MaDonHang", "TenantId" },
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
    }
}
