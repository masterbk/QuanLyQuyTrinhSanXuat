using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Lệnh sản xuất nhiều sản phẩm: tách phần thành phẩm của lệnh ra bảng LenhSanXuatSanPham (mỗi dòng
    /// một lô, có quy trình + khâu), tiêu hao và ảnh chuyển sang gắn với dòng sản phẩm.
    ///
    /// Dữ liệu cũ được CHUYỂN, không bỏ: mỗi lệnh cũ thành đúng một dòng sản phẩm (quy trình lấy theo
    /// danh mục thành phẩm), tiêu hao/ảnh trỏ sang dòng đó. Lệnh cũ không có dữ liệu khâu - lệnh còn
    /// "Mới tạo" phải mở Sửa để khai người thực hiện trước khi Hoàn thành.
    /// </summary>
    public partial class LenhSanXuatNhieuSanPham : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LenhSanXuatAnh_LenhSanXuat_LenhSanXuatId",
                table: "LenhSanXuatAnh");

            migrationBuilder.DropForeignKey(
                name: "FK_LenhSanXuatTieuHao_LenhSanXuat_LenhSanXuatId",
                table: "LenhSanXuatTieuHao");

            migrationBuilder.CreateTable(
                name: "LenhSanXuatSanPham",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LenhSanXuatId = table.Column<int>(type: "int", nullable: false),
                    MaThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    SoLuong = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    MaLoThanhPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HanSuDung = table.Column<DateOnly>(type: "date", nullable: true),
                    MaQuyTrinh = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaLoDaTao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LenhSanXuatSanPham", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LenhSanXuatSanPham_LenhSanXuat_LenhSanXuatId",
                        column: x => x.LenhSanXuatId,
                        principalTable: "LenhSanXuat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---- Chuyển dữ liệu: mỗi lệnh cũ -> một dòng sản phẩm ----
            migrationBuilder.Sql(@"
INSERT INTO LenhSanXuatSanPham
    (LenhSanXuatId, MaThanhPham, SoLuong, MaLoThanhPham, HanSuDung, MaQuyTrinh, MaLoDaTao,
     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy, TenantId)
SELECT l.Id, l.MaThanhPham, l.SoLuong, l.MaLoThanhPham, l.HanSuDungThanhPham,
       COALESCE((SELECT TOP 1 p.MaQuyTrinh FROM Products p
                 WHERE p.TenantId = l.TenantId AND p.MaSanPham = l.MaThanhPham), N''),
       l.MaLoDaTao, l.CreatedAtUtc, l.CreatedBy, l.UpdatedAtUtc, l.UpdatedBy, l.TenantId
FROM LenhSanXuat l;");

            // Tiêu hao + ảnh: đổi giá trị khoá từ Id lệnh sang Id dòng sản phẩm (quan hệ 1-1 lúc chuyển).
            migrationBuilder.Sql(@"
UPDATE t SET t.LenhSanXuatId = s.Id
FROM LenhSanXuatTieuHao t JOIN LenhSanXuatSanPham s ON s.LenhSanXuatId = t.LenhSanXuatId;

UPDATE a SET a.LenhSanXuatId = s.Id
FROM LenhSanXuatAnh a JOIN LenhSanXuatSanPham s ON s.LenhSanXuatId = a.LenhSanXuatId;");

            migrationBuilder.DropColumn(
                name: "HanSuDungThanhPham",
                table: "LenhSanXuat");

            migrationBuilder.DropColumn(
                name: "MaLoDaTao",
                table: "LenhSanXuat");

            migrationBuilder.DropColumn(
                name: "MaLoThanhPham",
                table: "LenhSanXuat");

            migrationBuilder.DropColumn(
                name: "MaThanhPham",
                table: "LenhSanXuat");

            migrationBuilder.DropColumn(
                name: "SoLuong",
                table: "LenhSanXuat");

            migrationBuilder.RenameColumn(
                name: "LenhSanXuatId",
                table: "LenhSanXuatTieuHao",
                newName: "LenhSanXuatSanPhamId");

            migrationBuilder.RenameIndex(
                name: "IX_LenhSanXuatTieuHao_LenhSanXuatId",
                table: "LenhSanXuatTieuHao",
                newName: "IX_LenhSanXuatTieuHao_LenhSanXuatSanPhamId");

            migrationBuilder.RenameColumn(
                name: "LenhSanXuatId",
                table: "LenhSanXuatAnh",
                newName: "LenhSanXuatSanPhamId");

            migrationBuilder.RenameIndex(
                name: "IX_LenhSanXuatAnh_LenhSanXuatId",
                table: "LenhSanXuatAnh",
                newName: "IX_LenhSanXuatAnh_LenhSanXuatSanPhamId");

            migrationBuilder.CreateTable(
                name: "LenhSanXuatKhau",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LenhSanXuatSanPhamId = table.Column<int>(type: "int", nullable: false),
                    MaKhau = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    MaCoSo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NguoiThucHienCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LenhSanXuatKhau", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LenhSanXuatKhau_LenhSanXuatSanPham_LenhSanXuatSanPhamId",
                        column: x => x.LenhSanXuatSanPhamId,
                        principalTable: "LenhSanXuatSanPham",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LenhSanXuatKhau_LenhSanXuatSanPhamId",
                table: "LenhSanXuatKhau",
                column: "LenhSanXuatSanPhamId");

            migrationBuilder.CreateIndex(
                name: "IX_LenhSanXuatSanPham_LenhSanXuatId",
                table: "LenhSanXuatSanPham",
                column: "LenhSanXuatId");

            migrationBuilder.AddForeignKey(
                name: "FK_LenhSanXuatAnh_LenhSanXuatSanPham_LenhSanXuatSanPhamId",
                table: "LenhSanXuatAnh",
                column: "LenhSanXuatSanPhamId",
                principalTable: "LenhSanXuatSanPham",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LenhSanXuatTieuHao_LenhSanXuatSanPham_LenhSanXuatSanPhamId",
                table: "LenhSanXuatTieuHao",
                column: "LenhSanXuatSanPhamId",
                principalTable: "LenhSanXuatSanPham",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Quay lui được với lệnh MỘT sản phẩm (lấy dòng đầu). Lệnh nhiều sản phẩm sẽ mất các dòng sau
            // cùng tiêu hao/ảnh của chúng - mô hình cũ không chứa nổi.
            migrationBuilder.DropForeignKey(
                name: "FK_LenhSanXuatAnh_LenhSanXuatSanPham_LenhSanXuatSanPhamId",
                table: "LenhSanXuatAnh");

            migrationBuilder.DropForeignKey(
                name: "FK_LenhSanXuatTieuHao_LenhSanXuatSanPham_LenhSanXuatSanPhamId",
                table: "LenhSanXuatTieuHao");

            migrationBuilder.DropTable(
                name: "LenhSanXuatKhau");

            migrationBuilder.AddColumn<DateOnly>(
                name: "HanSuDungThanhPham",
                table: "LenhSanXuat",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaLoDaTao",
                table: "LenhSanXuat",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaLoThanhPham",
                table: "LenhSanXuat",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MaThanhPham",
                table: "LenhSanXuat",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "SoLuong",
                table: "LenhSanXuat",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
;WITH dau AS (
    SELECT s.*, ROW_NUMBER() OVER (PARTITION BY s.LenhSanXuatId ORDER BY s.Id) AS rn
    FROM LenhSanXuatSanPham s)
UPDATE l SET l.MaThanhPham = d.MaThanhPham, l.SoLuong = d.SoLuong, l.MaLoThanhPham = d.MaLoThanhPham,
             l.HanSuDungThanhPham = d.HanSuDung, l.MaLoDaTao = d.MaLoDaTao
FROM LenhSanXuat l JOIN dau d ON d.LenhSanXuatId = l.Id AND d.rn = 1;

DELETE t FROM LenhSanXuatTieuHao t
WHERE t.LenhSanXuatSanPhamId NOT IN (SELECT MIN(Id) FROM LenhSanXuatSanPham GROUP BY LenhSanXuatId);
DELETE a FROM LenhSanXuatAnh a
WHERE a.LenhSanXuatSanPhamId NOT IN (SELECT MIN(Id) FROM LenhSanXuatSanPham GROUP BY LenhSanXuatId);

UPDATE t SET t.LenhSanXuatSanPhamId = s.LenhSanXuatId
FROM LenhSanXuatTieuHao t JOIN LenhSanXuatSanPham s ON s.Id = t.LenhSanXuatSanPhamId;
UPDATE a SET a.LenhSanXuatSanPhamId = s.LenhSanXuatId
FROM LenhSanXuatAnh a JOIN LenhSanXuatSanPham s ON s.Id = a.LenhSanXuatSanPhamId;");

            migrationBuilder.DropTable(
                name: "LenhSanXuatSanPham");

            migrationBuilder.RenameColumn(
                name: "LenhSanXuatSanPhamId",
                table: "LenhSanXuatTieuHao",
                newName: "LenhSanXuatId");

            migrationBuilder.RenameIndex(
                name: "IX_LenhSanXuatTieuHao_LenhSanXuatSanPhamId",
                table: "LenhSanXuatTieuHao",
                newName: "IX_LenhSanXuatTieuHao_LenhSanXuatId");

            migrationBuilder.RenameColumn(
                name: "LenhSanXuatSanPhamId",
                table: "LenhSanXuatAnh",
                newName: "LenhSanXuatId");

            migrationBuilder.RenameIndex(
                name: "IX_LenhSanXuatAnh_LenhSanXuatSanPhamId",
                table: "LenhSanXuatAnh",
                newName: "IX_LenhSanXuatAnh_LenhSanXuatId");

            migrationBuilder.AddForeignKey(
                name: "FK_LenhSanXuatAnh_LenhSanXuat_LenhSanXuatId",
                table: "LenhSanXuatAnh",
                column: "LenhSanXuatId",
                principalTable: "LenhSanXuat",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LenhSanXuatTieuHao_LenhSanXuat_LenhSanXuatId",
                table: "LenhSanXuatTieuHao",
                column: "LenhSanXuatId",
                principalTable: "LenhSanXuat",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
