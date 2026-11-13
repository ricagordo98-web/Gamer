using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSteamPriceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LowestSeenAt",
                table: "SteamAppInfos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowestSeenPrice",
                table: "SteamAppInfos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PriceUpdatedAt",
                table: "SteamAppInfos",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LowestSeenAt",
                table: "SteamAppInfos");

            migrationBuilder.DropColumn(
                name: "LowestSeenPrice",
                table: "SteamAppInfos");

            migrationBuilder.DropColumn(
                name: "PriceUpdatedAt",
                table: "SteamAppInfos");
        }
    }
}
