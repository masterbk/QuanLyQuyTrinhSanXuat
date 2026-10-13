using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonViTinhHnC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DonViTinhHnCId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeSoThaiBoGam",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeSoThaiBoPhanTram",
                table: "Products",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuyDoiGam",
                table: "Products",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DonViTinhHnCs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Ten = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    CapNhatLucUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonViTinhHnCs", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DonViTinhHnCs");

            migrationBuilder.DropColumn(
                name: "DonViTinhHnCId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HeSoThaiBoGam",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "HeSoThaiBoPhanTram",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "QuyDoiGam",
                table: "Products");
        }
    }
}
