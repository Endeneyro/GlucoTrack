using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddVitaminsAndMinerals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Folate",
                table: "Products",
                newName: "VitaminK");

            migrationBuilder.AddColumn<double>(
                name: "Chlorine",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Phosphorus",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Selenium",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Sodium",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Sulfur",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB1",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB2",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB3",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB4",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB5",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB6",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminB9",
                table: "Products",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "VitaminE",
                table: "Products",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Chlorine",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Phosphorus",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Selenium",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sodium",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sulfur",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB1",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB2",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB3",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB4",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB5",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB6",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminB9",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "VitaminE",
                table: "Products");

            migrationBuilder.RenameColumn(
                name: "VitaminK",
                table: "Products",
                newName: "Folate");
        }
    }
}
