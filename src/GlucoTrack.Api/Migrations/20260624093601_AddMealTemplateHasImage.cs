using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMealTemplateHasImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasImage",
                table: "MealTemplates",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HasImage",
                table: "MealTemplates");
        }
    }
}
