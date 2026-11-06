using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Gamer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLauncherPlatforms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Platforms",
                columns: new[] { "Id", "Family", "Format", "Name" },
                values: new object[,]
                {
                    { 17, "PC", 1, "Battle.net" },
                    { 18, "PC", 1, "Ubisoft" },
                    { 19, "PC", 1, "Rockstar" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Platforms",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Platforms",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Platforms",
                keyColumn: "Id",
                keyValue: 19);
        }
    }
}
