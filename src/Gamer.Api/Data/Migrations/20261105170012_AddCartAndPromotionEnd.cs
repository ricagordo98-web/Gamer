using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gamer.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCartAndPromotionEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EndsAt",
                table: "Promotions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CartItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Store = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "TEXT", nullable: false),
                    OriginalPrice = table.Column<decimal>(type: "TEXT", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    AddedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Games = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_Kind_Url",
                table: "CartItems",
                columns: new[] { "Kind", "Url" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CartItems");

            migrationBuilder.DropColumn(
                name: "EndsAt",
                table: "Promotions");
        }
    }
}
