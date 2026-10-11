using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Reviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CorrectedByReview",
                table: "StallRequests",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "EligibilityCheckedAt",
                table: "StallRequests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StallRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Response = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reviews_StallRequests_StallRequestId",
                        column: x => x.StallRequestId,
                        principalTable: "StallRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000001"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000002"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0000-000000000007"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000002"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000003"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000004"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000005"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000006"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000007"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000008"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000009"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000010"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.UpdateData(
                table: "StallRequests",
                keyColumn: "Id",
                keyValue: new Guid("60000000-0000-0000-0001-000000000011"),
                columns: new[] { "CorrectedByReview", "EligibilityCheckedAt" },
                values: new object[] { false, null });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_StallRequestId",
                table: "Reviews",
                column: "StallRequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropColumn(
                name: "CorrectedByReview",
                table: "StallRequests");

            migrationBuilder.DropColumn(
                name: "EligibilityCheckedAt",
                table: "StallRequests");
        }
    }
}
