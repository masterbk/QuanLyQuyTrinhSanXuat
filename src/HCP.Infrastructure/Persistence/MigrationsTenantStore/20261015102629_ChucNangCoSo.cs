using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.MigrationsTenantStore
{
    /// <inheritdoc />
    public partial class ChucNangCoSo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChucNangCsv",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            // Cơ sở đã có trước khi phân quyền chức năng: mở đủ mọi module để không ai mất menu.
            // Cơ sở đăng ký sau này mặc định rỗng = chỉ danh mục cơ bản.
            migrationBuilder.Sql(
                "UPDATE Tenants SET ChucNangCsv = 'LoSanXuat,SuatAn,KhoSanXuat,BanHang,HanoiCheck,KiemSoatAttp'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChucNangCsv",
                table: "Tenants");
        }
    }
}
