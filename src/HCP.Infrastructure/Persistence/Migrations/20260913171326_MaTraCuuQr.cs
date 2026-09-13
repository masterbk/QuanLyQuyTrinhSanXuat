using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MaTraCuuQr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaTraCuu",
                table: "DonHangBan",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaTraCuu",
                table: "Batches",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DonHangBan_MaTraCuu",
                table: "DonHangBan",
                column: "MaTraCuu");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_MaTraCuu",
                table: "Batches",
                column: "MaTraCuu");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DonHangBan_MaTraCuu",
                table: "DonHangBan");

            migrationBuilder.DropIndex(
                name: "IX_Batches_MaTraCuu",
                table: "Batches");

            migrationBuilder.DropColumn(
                name: "MaTraCuu",
                table: "DonHangBan");

            migrationBuilder.DropColumn(
                name: "MaTraCuu",
                table: "Batches");
        }
    }
}
