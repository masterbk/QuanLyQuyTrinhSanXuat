using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MonAnV26 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoaiApDung",
                table: "ProductionProcesses",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "food");

            // Quy trình đang gắn cho món ăn (và không gắn cho thực phẩm nào) chuyển sang loại "dish" - HanoiCheck v2.6
            // chỉ cho món ăn dùng quy trình loại dish.
            migrationBuilder.Sql(@"
UPDATE p SET LoaiApDung = 'dish'
FROM ProductionProcesses p
WHERE EXISTS (SELECT 1 FROM Dishes d WHERE d.TenantId = p.TenantId AND d.MaQuyTrinh = p.MaQuyTrinh)
  AND NOT EXISTS (SELECT 1 FROM Products x WHERE x.TenantId = p.TenantId AND x.MaQuyTrinh = p.MaQuyTrinh);");

            // Tệp món ăn trước đây có thể chỉ gắn mã bước SX: chuyển sang mã khâu của bước đó trước khi bỏ cột,
            // để không biến thành ảnh album của món (file trống mã khâu = ảnh món).
            migrationBuilder.Sql(@"
UPDATE f SET MaKhau = s.MaKhau
FROM DishFiles f
JOIN DishSteps s ON s.DishId = f.DishId AND s.MaBuocSx = f.MaBuocSx
WHERE (f.MaKhau IS NULL OR f.MaKhau = '') AND f.MaBuocSx IS NOT NULL AND f.MaBuocSx <> '';");

            migrationBuilder.DropColumn(
                name: "MaDanhMucThucPham",
                table: "ProductionProcesses");

            migrationBuilder.DropColumn(
                name: "MaBuocSx",
                table: "DishSteps");

            migrationBuilder.DropColumn(
                name: "MaNccDauVao",
                table: "DishSteps");

            migrationBuilder.DropColumn(
                name: "DonViTinhId",
                table: "DishIngredients");

            migrationBuilder.DropColumn(
                name: "MaBuocSx",
                table: "DishFiles");

            migrationBuilder.DropColumn(
                name: "MaCoSo",
                table: "Dishes");

            migrationBuilder.CreateTable(
                name: "NhomTuoiHnCs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CapNhatLucUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhomTuoiHnCs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhomTuoiHnCs");

            migrationBuilder.DropColumn(
                name: "LoaiApDung",
                table: "ProductionProcesses");

            migrationBuilder.AddColumn<int>(
                name: "MaDanhMucThucPham",
                table: "ProductionProcesses",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaBuocSx",
                table: "DishSteps",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaNccDauVao",
                table: "DishSteps",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DonViTinhId",
                table: "DishIngredients",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaBuocSx",
                table: "DishFiles",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaCoSo",
                table: "Dishes",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
