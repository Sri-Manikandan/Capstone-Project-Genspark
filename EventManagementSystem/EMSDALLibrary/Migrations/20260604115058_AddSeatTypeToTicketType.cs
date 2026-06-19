using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMSDALLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatTypeToTicketType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SeatType",
                table: "TicketTypes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SeatType",
                table: "TicketTypes");
        }
    }
}
