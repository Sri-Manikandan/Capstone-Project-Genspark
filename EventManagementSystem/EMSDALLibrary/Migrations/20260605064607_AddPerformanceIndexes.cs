using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EMSDALLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_EventId",
                table: "SeatReservations",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatReservations_Status_ReservedUntil",
                table: "SeatReservations",
                columns: new[] { "Status", "ReservedUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_StripePaymentIntentId",
                table: "Payments",
                column: "StripePaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizerRequests_Status",
                table: "OrganizerRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Category",
                table: "Events",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Events_StartTime",
                table: "Events",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Events_Status",
                table: "Events",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BookingStatus_ExpiresAt",
                table: "Bookings",
                columns: new[] { "BookingStatus", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_EventId",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_SeatReservations_Status_ReservedUntil",
                table: "SeatReservations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_StripePaymentIntentId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_OrganizerRequests_Status",
                table: "OrganizerRequests");

            migrationBuilder.DropIndex(
                name: "IX_Events_Category",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_StartTime",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_Status",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BookingStatus_ExpiresAt",
                table: "Bookings");
        }
    }
}
