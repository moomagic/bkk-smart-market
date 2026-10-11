using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Assignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WaitlistPosition",
                table: "StallRequests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DecidedAt",
                table: "AllocationRuns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecidedBy",
                table: "AllocationRuns",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmBy = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assignments_Locks_LockId",
                        column: x => x.LockId,
                        principalTable: "Locks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignments_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Assignments_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000007"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000002"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000003"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000004"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000005"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000006"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000007"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000008"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000009"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000010"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000011"),
                column: "WaitlistPosition",
                value: null);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_LockId",
                table: "Assignments",
                column: "LockId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_RoundId_LockId",
                table: "Assignments",
                columns: new[] { "RoundId", "LockId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_RoundId_VendorId",
                table: "Assignments",
                columns: new[] { "RoundId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_VendorId",
                table: "Assignments",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assignments");

            migrationBuilder.DropColumn(
                name: "WaitlistPosition",
                table: "StallRequests");

            migrationBuilder.DropColumn(
                name: "DecidedAt",
                table: "AllocationRuns");

            migrationBuilder.DropColumn(
                name: "DecidedBy",
                table: "AllocationRuns");
        }
    }
}
