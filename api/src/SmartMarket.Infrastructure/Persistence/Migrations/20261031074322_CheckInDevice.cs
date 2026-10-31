using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartMarket.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeviceHash",
                table: "CheckIns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeviceHash",
                table: "CheckIns");
        }
    }
}
