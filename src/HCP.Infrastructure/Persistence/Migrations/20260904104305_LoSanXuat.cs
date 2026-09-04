using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LoSanXuat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaSanPham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaLo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenLo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    NgayNhap = table.Column<DateOnly>(type: "date", nullable: false),
                    NgaySanXuat = table.Column<DateOnly>(type: "date", nullable: true),
                    HanSuDung = table.Column<DateOnly>(type: "date", nullable: true),
                    DiaChiThuMua = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    MaCoSo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaNccDauVao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BatchFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    MaFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DuongDan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Loai = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MaKhau = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaBuocSx = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchFiles_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatchSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    MaBuocSx = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaKhau = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ThuTu = table.Column<int>(type: "int", nullable: false),
                    MaLoNhap = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaLoNguyenLieu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaLoSanXuat = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ThoiGian = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NguoiThucHienCsv = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TrangThai = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaQrTruyVet = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    GhiChu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaCoSo = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    MaNccDauVao = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchSteps_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BatchWarehouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchId = table.Column<int>(type: "int", nullable: false),
                    MaKho = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BatchWarehouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BatchWarehouses_Batches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "Batches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Batches_MaLo",
                table: "Batches",
                columns: new[] { "MaLo", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchFiles_BatchId_MaFile",
                table: "BatchFiles",
                columns: new[] { "BatchId", "MaFile", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchSteps_BatchId_MaBuocSx",
                table: "BatchSteps",
                columns: new[] { "BatchId", "MaBuocSx", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BatchWarehouses_BatchId_MaKho",
                table: "BatchWarehouses",
                columns: new[] { "BatchId", "MaKho", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BatchFiles");

            migrationBuilder.DropTable(
                name: "BatchSteps");

            migrationBuilder.DropTable(
                name: "BatchWarehouses");

            migrationBuilder.DropTable(
                name: "Batches");
        }
    }
}
