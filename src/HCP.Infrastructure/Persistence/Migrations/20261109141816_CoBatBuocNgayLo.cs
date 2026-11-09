using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoBatBuocNgayLo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BatBuocHanSuDung",
                table: "StandardFoodCategories",
                type: "bit",
                nullable: false,
                // v2.7.1: mặc định BẮT BUỘC cho dòng cũ - job đồng bộ danh mục chuẩn sẽ ghi lại
                // cờ thật từ HanoiCheck. Nếu để false thì lô thiếu ngày sẽ lọt qua rồi bị HnC 422.
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BatBuocNgaySanXuat",
                table: "StandardFoodCategories",
                type: "bit",
                nullable: false,
                // v2.7.1: mặc định BẮT BUỘC cho dòng cũ - job đồng bộ danh mục chuẩn sẽ ghi lại
                // cờ thật từ HanoiCheck. Nếu để false thì lô thiếu ngày sẽ lọt qua rồi bị HnC 422.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatBuocHanSuDung",
                table: "StandardFoodCategories");

            migrationBuilder.DropColumn(
                name: "BatBuocNgaySanXuat",
                table: "StandardFoodCategories");
        }
    }
}
