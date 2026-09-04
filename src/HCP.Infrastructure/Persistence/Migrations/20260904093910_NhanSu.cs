using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NhanSu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Staff",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaNhanSu = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    HoTen = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ViTri = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    NgaySinh = table.Column<DateOnly>(type: "date", nullable: true),
                    DiaChi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DienThoai = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CccdEncrypted = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LaChuCoSo = table.Column<bool>(type: "bit", nullable: false),
                    LaNguoiCheBien = table.Column<bool>(type: "bit", nullable: false),
                    LaNguoiGiaoHang = table.Column<bool>(type: "bit", nullable: false),
                    PhuongTien = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    BienSo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TrangThai = table.Column<bool>(type: "bit", nullable: false),
                    KskSoGiay = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    KskNgayKham = table.Column<DateOnly>(type: "date", nullable: true),
                    KskNgayHetHan = table.Column<DateOnly>(type: "date", nullable: true),
                    KskNoiKham = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttpSoChungNhan = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttpNgayCap = table.Column<DateOnly>(type: "date", nullable: true),
                    AttpCoQuanCap = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Staff", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Staff_MaNhanSu",
                table: "Staff",
                columns: new[] { "MaNhanSu", "TenantId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Staff");
        }
    }
}
