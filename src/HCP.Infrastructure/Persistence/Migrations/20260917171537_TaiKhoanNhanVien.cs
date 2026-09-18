using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaiKhoanNhanVien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NhanSuId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_TenantId_NhanSuId",
                table: "AspNetUsers",
                columns: new[] { "TenantId", "NhanSuId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_TenantId_NhanSuId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NhanSuId",
                table: "AspNetUsers");
        }
    }
}
