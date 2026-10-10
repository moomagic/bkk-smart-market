using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoundsAndStallRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Rounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    EndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ApplyOpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApplyClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rounds_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StallRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    VendorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IneligibleReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CarryOverWeight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StallRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StallRequests_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StallRequests_Vendors_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Rounds",
                columns: new[] { "Id", "ApplyClosesAt", "ApplyOpensAt", "EndsOn", "SpaceId", "StartsOn", "Status" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 10, 31, 16, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 30, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2027, 4, 30), new Guid("20000000-0000-0000-0000-000000000001"), new DateOnly(2026, 11, 1), "Open" },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 10, 31, 16, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 30, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2027, 4, 30), new Guid("20000000-0000-0000-0000-000000000002"), new DateOnly(2026, 11, 1), "Open" },
                    { new Guid("50000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 10, 31, 16, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 30, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2027, 4, 30), new Guid("20000000-0000-0000-0000-000000000003"), new DateOnly(2026, 11, 1), "Open" },
                    { new Guid("50000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 10, 31, 16, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 30, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2026, 11, 30), new Guid("20000000-0000-0000-0000-000000000004"), new DateOnly(2026, 11, 1), "Open" },
                    { new Guid("50000000-0000-0000-0001-000000000001"), new DateTimeOffset(new DateTime(2026, 4, 30, 16, 59, 59, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 31, 17, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateOnly(2026, 10, 31), new Guid("20000000-0000-0000-0000-000000000001"), new DateOnly(2026, 5, 1), "Approved" }
                });

            migrationBuilder.InsertData(
                table: "StallRequests",
                columns: new[] { "Id", "CarryOverWeight", "Category", "IneligibleReason", "RoundId", "Status", "SubmittedAt", "VendorId" },
                values: new object[,]
                {
                    { new Guid("60000000-0000-0000-0000-000000000001"), 1, "Legacy", null, new Guid("50000000-0000-0000-0001-000000000001"), "Confirmed", new DateTimeOffset(new DateTime(2026, 4, 3, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000001") },
                    { new Guid("60000000-0000-0000-0000-000000000002"), 1, "Legacy", null, new Guid("50000000-0000-0000-0001-000000000001"), "Confirmed", new DateTimeOffset(new DateTime(2026, 4, 5, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000002") },
                    { new Guid("60000000-0000-0000-0001-000000000002"), 1, "Legacy", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 2, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000002") },
                    { new Guid("60000000-0000-0000-0001-000000000003"), 1, "Vulnerable", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 3, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000003") },
                    { new Guid("60000000-0000-0000-0001-000000000004"), 1, "Vulnerable", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 3, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000004") },
                    { new Guid("60000000-0000-0000-0001-000000000005"), 1, "Vulnerable", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 6, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000005") },
                    { new Guid("60000000-0000-0000-0001-000000000006"), 1, "New", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 7, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000006") },
                    { new Guid("60000000-0000-0000-0001-000000000007"), 1, "New", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 9, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000007") },
                    { new Guid("60000000-0000-0000-0001-000000000008"), 1, "New", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 12, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000008") },
                    { new Guid("60000000-0000-0000-0001-000000000009"), 1, "New", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 14, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000009") },
                    { new Guid("60000000-0000-0000-0001-000000000010"), 1, "New", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 18, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000010") },
                    { new Guid("60000000-0000-0000-0001-000000000011"), 1, "New", null, new Guid("50000000-0000-0000-0000-000000000001"), "Submitted", new DateTimeOffset(new DateTime(2026, 10, 20, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000011") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rounds_SpaceId_StartsOn",
                table: "Rounds",
                columns: new[] { "SpaceId", "StartsOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StallRequests_RoundId_VendorId",
                table: "StallRequests",
                columns: new[] { "RoundId", "VendorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StallRequests_VendorId",
                table: "StallRequests",
                column: "VendorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StallRequests");

            migrationBuilder.DropTable(
                name: "Rounds");
        }
    }
}
