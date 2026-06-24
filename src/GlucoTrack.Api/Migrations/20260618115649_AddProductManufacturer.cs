using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductManufacturer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserInsulins_UserId",
                table: "UserInsulins");

            migrationBuilder.AlterColumn<double>(
                name: "DiaHours",
                table: "UserSettings",
                type: "double precision",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "double precision",
                oldDefaultValue: 4.0);

            migrationBuilder.AddColumn<string>(
                name: "Manufacturer",
                table: "Products",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Manufacturer",
                table: "Products");

            migrationBuilder.AlterColumn<double>(
                name: "DiaHours",
                table: "UserSettings",
                type: "double precision",
                nullable: false,
                defaultValue: 4.0,
                oldClrType: typeof(double),
                oldType: "double precision");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_UserId",
                table: "UserProfiles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserInsulins_UserId",
                table: "UserInsulins",
                column: "UserId");
        }
    }
}
