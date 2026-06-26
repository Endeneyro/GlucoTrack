using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlannedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlannedEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "text", nullable: true),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDone = table.Column<bool>(type: "boolean", nullable: false),
                    InsulinSubtype = table.Column<int>(type: "integer", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlannedEventMealItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlannedEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    Grams = table.Column<double>(type: "double precision", nullable: false),
                    MeasureType = table.Column<int>(type: "integer", nullable: false),
                    PieceWeightG = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlannedEventMealItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlannedEventMealItems_PlannedEvents_PlannedEventId",
                        column: x => x.PlannedEventId,
                        principalTable: "PlannedEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlannedEventMealItems_PlannedEventId",
                table: "PlannedEventMealItems",
                column: "PlannedEventId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlannedEventMealItems");

            migrationBuilder.DropTable(
                name: "PlannedEvents");
        }
    }
}
