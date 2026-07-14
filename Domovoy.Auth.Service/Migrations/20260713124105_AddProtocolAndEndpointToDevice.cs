using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.Auth.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddProtocolAndEndpointToDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Endpoint",
                table: "DeviceCredentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Protocol",
                table: "DeviceCredentials",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Endpoint",
                table: "DeviceCredentials");

            migrationBuilder.DropColumn(
                name: "Protocol",
                table: "DeviceCredentials");
        }
    }
}
