using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Domovoy.CommandDispatcher.Service.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommandLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Command = table.Column<string>(type: "text", nullable: false),
                    Params = table.Column<string>(type: "text", nullable: true),
                    SourceRuleId = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Protocol = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Endpoint = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_DeviceId_CreatedAt",
                table: "CommandLogs",
                columns: new[] { "DeviceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandLogs_Status",
                table: "CommandLogs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandLogs");
        }
    }
}
