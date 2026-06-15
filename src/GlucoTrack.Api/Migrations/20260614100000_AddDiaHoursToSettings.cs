using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    public partial class AddDiaHoursToSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DiaHours",
                table: "UserSettings",
                type: "double precision",
                nullable: false,
                defaultValue: 4.0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiaHours",
                table: "UserSettings");
        }
    }
}
