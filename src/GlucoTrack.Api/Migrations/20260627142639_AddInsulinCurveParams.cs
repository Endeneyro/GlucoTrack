using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GlucoTrack.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInsulinCurveParams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Brand",
                table: "UserInsulins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "DiaHours",
                table: "UserInsulins",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "PeakMinutes",
                table: "UserInsulins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Data-migration: проставить разумные значения существующим инсулинам.
            // Новые строки приходят с 75/4.0 из кода и DTO, поэтому SQL-дефолты на колонках не нужны.
            migrationBuilder.Sql(
                "UPDATE \"UserInsulins\" SET \"PeakMinutes\" = 75 WHERE \"PeakMinutes\" = 0;");
            migrationBuilder.Sql(
                "UPDATE \"UserInsulins\" SET \"DiaHours\" = 4.0 WHERE \"DiaHours\" <= 0;");
            // Перенести индивидуальный DIA из настроек на активный болюсный инсулин пользователя.
            migrationBuilder.Sql(@"
                UPDATE ""UserInsulins"" ui
                SET ""DiaHours"" = us.""DiaHours""
                FROM ""UserSettings"" us
                WHERE ui.""UserId"" = us.""UserId""
                  AND ui.""InsulinType"" = 0
                  AND ui.""IsActive"" = true
                  AND ui.""IsDeleted"" = false
                  AND us.""IsDeleted"" = false
                  AND us.""DiaHours"" > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Brand",
                table: "UserInsulins");

            migrationBuilder.DropColumn(
                name: "DiaHours",
                table: "UserInsulins");

            migrationBuilder.DropColumn(
                name: "PeakMinutes",
                table: "UserInsulins");
        }
    }
}
