using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConfirmAndForfeit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_RoundId_LockId",
                table: "Assignments");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForfeitedAt",
                table: "Assignments",
                type: "timestamp with time zone",
                nullable: true);

            // Runs approved before this migration start their current result as their draw result.
            migrationBuilder.AddColumn<string>(
                name: "CurrentResultJson",
                table: "AllocationRuns",
                type: "jsonb",
                nullable: true);
            migrationBuilder.Sql("UPDATE \"AllocationRuns\" SET \"CurrentResultJson\" = \"ResultJson\";");
            migrationBuilder.AlterColumn<string>(
                name: "CurrentResultJson",
                table: "AllocationRuns",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_RoundId_LockId",
                table: "Assignments",
                columns: new[] { "RoundId", "LockId" },
                unique: true,
                filter: "\"ForfeitedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assignments_RoundId_LockId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "ForfeitedAt",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "CurrentResultJson",
                table: "AllocationRuns");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_RoundId_LockId",
                table: "Assignments",
                columns: new[] { "RoundId", "LockId" },
                unique: true);
        }
    }
}
