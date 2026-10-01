using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ChiMucTenDanhMucChuan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StandardFoodCategories_Name",
                table: "StandardFoodCategories",
                column: "Name")
                .Annotation("SqlServer:Include", new[] { "Code", "MeasureName", "CapNhatLucUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StandardFoodCategories_Name",
                table: "StandardFoodCategories");
        }
    }
}
