using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSteamStats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastPlayedAt",
                table: "SteamLibrary",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecentMinutes",
                table: "SteamLibrary",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SteamAppInfos",
                columns: table => new
                {
                    AppId = table.Column<long>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "TEXT", nullable: true),
                    NormalPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    DiscountPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    Metacritic = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewPercent = table.Column<int>(type: "INTEGER", nullable: true),
                    ReviewCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewSummary = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Genres = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ReleaseDate = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Owners = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    AverageMinutes = table.Column<int>(type: "INTEGER", nullable: true),
                    LowestPrice = table.Column<decimal>(type: "TEXT", nullable: true),
                    LowestPriceShop = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SteamAppInfos", x => x.AppId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SteamAppInfos");

            migrationBuilder.DropColumn(
                name: "LastPlayedAt",
                table: "SteamLibrary");

            migrationBuilder.DropColumn(
                name: "RecentMinutes",
                table: "SteamLibrary");
        }
    }
}
