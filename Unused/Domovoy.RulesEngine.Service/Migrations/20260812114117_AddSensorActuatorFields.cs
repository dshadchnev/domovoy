using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.RulesEngine.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddSensorActuatorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActuatorDeviceId",
                table: "Rules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SensorDeviceId",
                table: "Rules",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActuatorDeviceId",
                table: "Rules");

            migrationBuilder.DropColumn(
                name: "SensorDeviceId",
                table: "Rules");
        }
    }
}
