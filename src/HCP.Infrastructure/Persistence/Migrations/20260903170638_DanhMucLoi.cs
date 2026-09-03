using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DanhMucLoi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Facilities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaCoSo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenCoSo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DiaChi = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Facilities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaQuyTrinh = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenQuyTrinh = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaDanhMucThucPham = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionProcesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductionSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaKhau = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenKhau = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionSteps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StandardFoodCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MeasureName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CapNhatLucUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StandardFoodCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubSuppliers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNccDauVao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaSoThue = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DienThoai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AttpSoGiay = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttpNgayCap = table.Column<DateOnly>(type: "date", nullable: true),
                    AttpNgayHetHan = table.Column<DateOnly>(type: "date", nullable: true),
                    HopDongSo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    HopDongNgayKy = table.Column<DateOnly>(type: "date", nullable: true),
                    HopDongNgayHetHan = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubSuppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessStepLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionProcessId = table.Column<int>(type: "int", nullable: false),
                    MaKhau = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessStepLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessStepLines_ProductionProcesses_ProductionProcessId",
                        column: x => x.ProductionProcessId,
                        principalTable: "ProductionProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubSupplierFoodGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SubSupplierId = table.Column<int>(type: "int", nullable: false),
                    MaNhom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubSupplierFoodGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubSupplierFoodGroups_SubSuppliers_SubSupplierId",
                        column: x => x.SubSupplierId,
                        principalTable: "SubSuppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_MaCoSo",
                table: "Facilities",
                columns: new[] { "MaCoSo", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessStepLines_ProductionProcessId_ThuTu",
                table: "ProcessStepLines",
                columns: new[] { "ProductionProcessId", "ThuTu" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionProcesses_MaQuyTrinh",
                table: "ProductionProcesses",
                columns: new[] { "MaQuyTrinh", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionSteps_MaKhau",
                table: "ProductionSteps",
                columns: new[] { "MaKhau", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StandardFoodCategories_Code",
                table: "StandardFoodCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubSupplierFoodGroups_SubSupplierId_MaNhom",
                table: "SubSupplierFoodGroups",
                columns: new[] { "SubSupplierId", "MaNhom", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubSuppliers_MaNccDauVao",
                table: "SubSuppliers",
                columns: new[] { "MaNccDauVao", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Facilities");

            migrationBuilder.DropTable(
                name: "ProcessStepLines");

            migrationBuilder.DropTable(
                name: "ProductionSteps");

            migrationBuilder.DropTable(
                name: "StandardFoodCategories");

            migrationBuilder.DropTable(
                name: "SubSupplierFoodGroups");

            migrationBuilder.DropTable(
                name: "ProductionProcesses");

            migrationBuilder.DropTable(
                name: "SubSuppliers");
        }
    }
}
