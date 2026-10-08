using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhieuChuKy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChuKyAnh",
                table: "PhieuGhiNhan",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "KyLucUtc",
                table: "PhieuGhiNhan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaBamNoiDung",
                table: "PhieuGhiNhan",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MaTraCuu",
                table: "PhieuGhiNhan",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NguoiKyUserId",
                table: "PhieuGhiNhan",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenNguoiKy",
                table: "PhieuGhiNhan",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhieuGhiNhan_MaTraCuu",
                table: "PhieuGhiNhan",
                column: "MaTraCuu");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PhieuGhiNhan_MaTraCuu",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "ChuKyAnh",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "KyLucUtc",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "MaBamNoiDung",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "MaTraCuu",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "NguoiKyUserId",
                table: "PhieuGhiNhan");

            migrationBuilder.DropColumn(
                name: "TenNguoiKy",
                table: "PhieuGhiNhan");
        }
    }
}
