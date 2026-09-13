using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DongBoHnCTuyChon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Mặc định true: mọi bản ghi đã có tiếp tục đồng bộ như trước; cơ sở đã có cấu hình kết nối thì công tắc tổng bật
            // (cơ sở chưa có cấu hình = chưa có dòng TenantHnCCredentials = tắt).
            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BatDongBo",
                table: "TenantHnCCredentials",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "SubSuppliers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "Staff",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "ProductionSteps",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "ProductionProcesses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "Facilities",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "Dishes",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "DongBoHnC",
                table: "Batches",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "BatDongBo",
                table: "TenantHnCCredentials");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "SubSuppliers");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "Staff");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "ProductionSteps");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "ProductionProcesses");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "Dishes");

            migrationBuilder.DropColumn(
                name: "DongBoHnC",
                table: "Batches");
        }
    }
}
