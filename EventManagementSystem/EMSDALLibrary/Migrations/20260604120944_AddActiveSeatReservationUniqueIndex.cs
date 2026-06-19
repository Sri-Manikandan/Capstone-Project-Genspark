using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMSDALLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddActiveSeatReservationUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_EventId",
                table: "SeatReservations");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_EventId_SeatId",
                table: "SeatReservations",
                columns: new[] { "EventId", "SeatId" },
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_EventId_SeatId",
                table: "SeatReservations");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_EventId",
                table: "SeatReservations",
                column: "EventId");
        }
    }
}
