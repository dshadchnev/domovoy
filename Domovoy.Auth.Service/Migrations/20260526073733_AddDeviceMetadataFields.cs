using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.Auth.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceMetadataFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "DeviceCredentials",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DeviceCredentials",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "DeviceCredentials");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DeviceCredentials");
        }
    }
}
