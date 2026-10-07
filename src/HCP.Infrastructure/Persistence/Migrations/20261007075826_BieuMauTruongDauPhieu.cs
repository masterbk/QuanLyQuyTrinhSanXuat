using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BieuMauTruongDauPhieu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Ca",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "KhuVuc",
                table: "PhieuGhiNhan");

            migrationBuilder.AddColumn<bool>(
                name: "LaDauPhieu",
                table: "TruongBieuMau",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "GiaTriDauJson",
                table: "PhieuGhiNhan",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LaDauPhieu",
                table: "TruongBieuMau");

            migrationBuilder.DropColumn(
                name: "GiaTriDauJson",
                table: "PhieuGhiNhan");

            migrationBuilder.AddColumn<string>(
                name: "Ca",
                table: "PhieuGhiNhan",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KhuVuc",
                table: "PhieuGhiNhan",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
