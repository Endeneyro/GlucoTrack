using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductHideAndClone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClonedFromProductId",
                table: "Products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductHides",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductHides", x => new { x.UserId, x.ProductId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_ClonedFromProductId",
                table: "Products",
                column: "ClonedFromProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductHides");

            migrationBuilder.DropIndex(
                name: "IX_Products_ClonedFromProductId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ClonedFromProductId",
                table: "Products");
        }
    }
}
