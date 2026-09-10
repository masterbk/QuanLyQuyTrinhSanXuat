using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LenhSanXuatAnh : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LenhSanXuatAnh",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LenhSanXuatId = table.Column<int>(type: "int", nullable: false),
                    MaFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    TenFile = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DuongDan = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ThoiGianUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LenhSanXuatAnh", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LenhSanXuatAnh_LenhSanXuat_LenhSanXuatId",
                        column: x => x.LenhSanXuatId,
                        principalTable: "LenhSanXuat",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LenhSanXuatAnh_LenhSanXuatId",
                table: "LenhSanXuatAnh",
                column: "LenhSanXuatId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LenhSanXuatAnh");
        }
    }
}
