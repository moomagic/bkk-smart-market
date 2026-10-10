using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EligibilityDemoSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StallRequests",
                columns: new[] { "Id", "CarryOverWeight", "Category", "IneligibleReason", "RoundId", "Status", "SubmittedAt", "VendorId" },
                values: new object[] { new Guid("60000000-0000-0000-0000-000000000007"), 1, "New", null, new Guid("50000000-0000-0000-0001-000000000001"), "NotSelected", new DateTimeOffset(new DateTime(2026, 4, 8, 3, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("40000000-0000-0000-0000-000000000007") });

            migrationBuilder.UpdateData(
                table: "Vendors",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000011"),
                column: "FoodHygieneTrained",
                value: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000007"));

            migrationBuilder.UpdateData(
                table: "Vendors",
                keyColumn: "Id",
                keyValue: new Guid("40000000-0000-0000-0000-000000000011"),
                column: "FoodHygieneTrained",
                value: true);
        }
    }
}
