using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckIns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    WithinGeofence = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckIns_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_AssignmentId_Day",
                table: "CheckIns",
                columns: new[] { "AssignmentId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CheckIns");
        }
    }
}
