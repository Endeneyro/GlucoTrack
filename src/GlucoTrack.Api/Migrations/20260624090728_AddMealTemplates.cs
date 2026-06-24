using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MealTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MealTemplateItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MealTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Grams = table.Column<double>(type: "double precision", nullable: false),
                    MeasureType = table.Column<int>(type: "integer", nullable: false),
                    PieceWeightG = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MealTemplateItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MealTemplateItems_MealTemplates_MealTemplateId",
                        column: x => x.MealTemplateId,
                        principalTable: "MealTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MealTemplateItems_MealTemplateId",
                table: "MealTemplateItems",
                column: "MealTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MealTemplateItems");

            migrationBuilder.DropTable(
                name: "MealTemplates");
        }
    }
}
