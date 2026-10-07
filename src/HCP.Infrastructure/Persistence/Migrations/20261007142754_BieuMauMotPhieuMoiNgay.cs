using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BieuMauMotPhieuMoiNgay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "MotPhieuMoiNgay",
                table: "BieuMau",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotPhieuMoiNgay",
                table: "BieuMau");
        }
    }
}
