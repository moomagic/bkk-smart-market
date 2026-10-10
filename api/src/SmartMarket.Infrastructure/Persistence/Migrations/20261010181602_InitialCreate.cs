using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Actor = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Districts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Districts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaskedName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VulnerableReason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ProductType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FoodHygieneTrained = table.Column<bool>(type: "boolean", nullable: false),
                    BannedUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    ConsentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Spaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DistrictId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProductTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    OpenHours = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RulesRef = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Spaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Spaces_Districts_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "Districts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Locks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Locks_Spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalTable: "Spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Districts",
                columns: new[] { "Id", "Name" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), "บางพลัด" });

            migrationBuilder.InsertData(
                table: "Vendors",
                columns: new[] { "Id", "BannedUntil", "ConsentAt", "DisplayName", "FoodHygieneTrained", "MaskedName", "Phone", "ProductType", "VulnerableReason" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "สมศรี ใจดี", true, "สมศรี ใ.", "080-000-0001", "ข้าวแกง", null },
                    { new Guid("40000000-0000-0000-0000-000000000002"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "บุญมี รักไทย", true, "บุญมี ร.", "080-000-0002", "ก๋วยเตี๋ยว", null },
                    { new Guid("40000000-0000-0000-0000-000000000003"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "มาลี สุขใจ", true, "มาลี ส.", "080-000-0003", "ขนมไทย", "ผู้สูงอายุ" },
                    { new Guid("40000000-0000-0000-0000-000000000004"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "อนุชา กล้าหาญ", true, "อนุชา ก.", "080-000-0004", "น้ำผลไม้", "ผู้พิการ" },
                    { new Guid("40000000-0000-0000-0000-000000000005"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "วรรณา ทองดี", true, "วรรณา ท.", "080-000-0005", "ส้มตำ", "ผู้สูงอายุ" },
                    { new Guid("40000000-0000-0000-0000-000000000006"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ธนพล จันทร์เพ็ญ", true, "ธนพล จ.", "080-000-0006", "ข้าวมันไก่", null },
                    { new Guid("40000000-0000-0000-0000-000000000007"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ปิยะ นาคสวัสดิ์", true, "ปิยะ น.", "080-000-0007", "หมูปิ้ง", null },
                    { new Guid("40000000-0000-0000-0000-000000000008"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "สุดา พรหมมา", true, "สุดา พ.", "080-000-0008", "ข้าวเหนียวมะม่วง", null },
                    { new Guid("40000000-0000-0000-0000-000000000009"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "กิตติ วงศ์ใหญ่", true, "กิตติ ว.", "080-000-0009", "ลูกชิ้นทอด", null },
                    { new Guid("40000000-0000-0000-0000-000000000010"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ศิริ มั่นคง", true, "ศิริ ม.", "080-000-0010", "กาแฟโบราณ", null },
                    { new Guid("40000000-0000-0000-0000-000000000011"), null, new DateTimeOffset(new DateTime(2026, 9, 1, 2, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ชัย ดวงดี", true, "ชัย ด.", "080-000-0011", "ไก่ทอด", null }
                });

            migrationBuilder.InsertData(
                table: "Spaces",
                columns: new[] { "Id", "DistrictId", "IsActive", "Name", "OpenHours", "ProductTypes", "RulesRef", "Type" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001"), true, "ปากซอยจรัญฯ 79", "ทุกวัน 16:00–22:00", new[] { "อาหาร" }, "docs/rules", "Relaxation" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new Guid("10000000-0000-0000-0000-000000000001"), true, "หน้า MRT บางพลัด ทางออก 1", "ทุกวัน 06:00–10:00, 16:00–21:00", new[] { "อาหาร", "เครื่องดื่ม" }, "docs/rules", "Relaxation" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new Guid("10000000-0000-0000-0000-000000000001"), true, "ตลาดนัดเขตบางพลัด", "ศ–อา 15:00–22:00", new[] { "ทั่วไป" }, "docs/rules", "DistrictMarket" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new Guid("10000000-0000-0000-0000-000000000001"), true, "ลานใต้สะพานพระราม 8", "ส–อา 16:00–22:00", new[] { "ทั่วไป" }, "docs/rules", "TrialArea" }
                });

            migrationBuilder.InsertData(
                table: "Locks",
                columns: new[] { "Id", "Code", "SpaceId" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0001-000000000001"), "B1", new Guid("20000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0001-000000000002"), "B2", new Guid("20000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0001-000000000003"), "B3", new Guid("20000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0001-000000000004"), "B4", new Guid("20000000-0000-0000-0000-000000000001") },
                    { new Guid("30000000-0000-0000-0002-000000000001"), "B1", new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0002-000000000002"), "B2", new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0002-000000000003"), "B3", new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0002-000000000004"), "B4", new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0002-000000000005"), "B5", new Guid("20000000-0000-0000-0000-000000000002") },
                    { new Guid("30000000-0000-0000-0003-000000000001"), "B1", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000002"), "B2", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000003"), "B3", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000004"), "B4", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000005"), "B5", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000006"), "B6", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000007"), "B7", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000008"), "B8", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000009"), "B9", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000010"), "B10", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000011"), "B11", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000012"), "B12", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000013"), "B13", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000014"), "B14", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000015"), "B15", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000016"), "B16", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000017"), "B17", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000018"), "B18", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000019"), "B19", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0003-000000000020"), "B20", new Guid("20000000-0000-0000-0000-000000000003") },
                    { new Guid("30000000-0000-0000-0004-000000000001"), "B1", new Guid("20000000-0000-0000-0000-000000000004") },
                    { new Guid("30000000-0000-0000-0004-000000000002"), "B2", new Guid("20000000-0000-0000-0000-000000000004") },
                    { new Guid("30000000-0000-0000-0004-000000000003"), "B3", new Guid("20000000-0000-0000-0000-000000000004") },
                    { new Guid("30000000-0000-0000-0004-000000000004"), "B4", new Guid("20000000-0000-0000-0000-000000000004") },
                    { new Guid("30000000-0000-0000-0004-000000000005"), "B5", new Guid("20000000-0000-0000-0000-000000000004") },
                    { new Guid("30000000-0000-0000-0004-000000000006"), "B6", new Guid("20000000-0000-0000-0000-000000000004") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_At",
                table: "AuditEntries",
                column: "At");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_EntityType_EntityId",
                table: "AuditEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_Locks_SpaceId_Code",
                table: "Locks",
                columns: new[] { "SpaceId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Spaces_DistrictId",
                table: "Spaces",
                column: "DistrictId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEntries");

            migrationBuilder.DropTable(
                name: "Locks");

            migrationBuilder.DropTable(
                name: "Vendors");

            migrationBuilder.DropTable(
                name: "Spaces");

            migrationBuilder.DropTable(
                name: "Districts");
        }
    }
}
