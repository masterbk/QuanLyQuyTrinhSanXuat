using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangBanHnC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HnCCoThayDoi",
                table: "DonHangBan",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TrangThaiHnC",
                table: "DonHangBan",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBan_MaDonHnC",
                table: "DonHangBan",
                column: "MaDonHnC");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DonHangBan_MaDonHnC",
                table: "DonHangBan");

            migrationBuilder.DropColumn(
                name: "HnCCoThayDoi",
                table: "DonHangBan");

            migrationBuilder.DropColumn(
                name: "TrangThaiHnC",
                table: "DonHangBan");
        }
    }
}
