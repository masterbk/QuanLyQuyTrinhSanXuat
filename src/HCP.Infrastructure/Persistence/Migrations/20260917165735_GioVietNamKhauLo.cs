using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Thời gian khâu của Lô sản xuất (BatchSteps.ThoiGian) chuyển sang lưu GIỜ VIỆT NAM - đúng như người dùng nhập ở màn
    /// Lô sản xuất và như HanoiCheck hiểu trường thoi_gian. Trước đây lô sinh tự động từ lệnh sản xuất ghi giờ UTC nên
    /// lệch 7 tiếng: cộng 7 tiếng cho các khâu của những lô đó. Lô nhập tay chỉ có ngày, giữ nguyên.
    /// </summary>
    public partial class GioVietNamKhauLo : Migration
    {
        private const string LoTuLenh = @"
FROM BatchSteps bs
INNER JOIN Batches b ON b.Id = bs.BatchId
WHERE bs.ThoiGian IS NOT NULL AND b.GhiChu LIKE N'Tự động từ lệnh sản xuất%'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE bs SET ThoiGian = DATEADD(HOUR, 7, bs.ThoiGian)" + LoTuLenh);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE bs SET ThoiGian = DATEADD(HOUR, -7, bs.ThoiGian)" + LoTuLenh);
        }
    }
}
