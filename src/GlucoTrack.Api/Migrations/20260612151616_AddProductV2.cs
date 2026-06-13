using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "DefaultServingG",
                table: "Products",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "DislikesCount",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsComposite",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsVerified",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LikesCount",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MeasureType",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "OwnerType",
                table: "Products",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "PieceWeightG",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TotalYieldG",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProductIngredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompositeProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Grams = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductIngredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Products_CompositeProductId",
                        column: x => x.CompositeProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductIngredients_Products_IngredientProductId",
                        column: x => x.IngredientProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductReactions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reaction = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReactions", x => new { x.UserId, x.ProductId });
                });

            migrationBuilder.CreateTable(
                name: "ProductUsages",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductUsages", x => new { x.UserId, x.ProductId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_OwnerType",
                table: "Products",
                column: "OwnerType");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_CompositeProductId",
                table: "ProductIngredients",
                column: "CompositeProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductIngredients_IngredientProductId",
                table: "ProductIngredients",
                column: "IngredientProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductIngredients");

            migrationBuilder.DropTable(
                name: "ProductReactions");

            migrationBuilder.DropTable(
                name: "ProductUsages");

            migrationBuilder.DropIndex(
                name: "IX_Products_OwnerType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DefaultServingG",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DislikesCount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsComposite",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsVerified",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "LikesCount",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MeasureType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PieceWeightG",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TotalYieldG",
                table: "Products");
        }
    }
}
