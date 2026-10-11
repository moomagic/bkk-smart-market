using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllocationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AllocationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceNo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RulesSnapshot = table.Column<string>(type: "jsonb", nullable: false),
                    RanAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RanBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RejectReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RejectCountsTowardLimit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AllocationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AllocationRuns_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRuns_ReferenceNo",
                table: "AllocationRuns",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AllocationRuns_RoundId",
                table: "AllocationRuns",
                column: "RoundId",
                unique: true,
                filter: "\"Status\" = 'Draft'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AllocationRuns");
        }
    }
}
