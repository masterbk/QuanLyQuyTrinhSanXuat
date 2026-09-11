using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DonHangNhanThongTinGiao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "MaNguoiGiao",
                table: "DonHangNhan",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DiaChiGiao",
                table: "DonHangNhan",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BienSoXe",
                table: "DonHangNhan",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiemTruong",
                table: "DonHangNhan",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GhiChu",
                table: "DonHangNhan",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KhoXuat",
                table: "DonHangNhan",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkTruyXuat",
                table: "DonHangNhan",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LoaiDon",
                table: "DonHangNhan",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NgayTaoTrenHnC",
                table: "DonHangNhan",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhuongTienGiao",
                table: "DonHangNhan",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SdtNguoiGiao",
                table: "DonHangNhan",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenNguoiGiao",
                table: "DonHangNhan",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BienSoXe",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "DiemTruong",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "GhiChu",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "KhoXuat",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "LinkTruyXuat",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "LoaiDon",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "NgayTaoTrenHnC",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "PhuongTienGiao",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "SdtNguoiGiao",
                table: "DonHangNhan");

            migrationBuilder.DropColumn(
                name: "TenNguoiGiao",
                table: "DonHangNhan");

            migrationBuilder.AlterColumn<string>(
                name: "MaNguoiGiao",
                table: "DonHangNhan",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DiaChiGiao",
                table: "DonHangNhan",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
