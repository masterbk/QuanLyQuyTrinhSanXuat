using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HCP.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BieuMauDayVaoKho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DayVaoKho",
                table: "BieuMau",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DayVaoKho",
                table: "BieuMau");
        }
    }
}
