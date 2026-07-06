using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMSDALLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Events_EventId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Events_EventId",
                table: "SeatReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketTypes_Events_EventId",
                table: "TicketTypes");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "TicketTypes",
                newName: "ScreeningId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketTypes_EventId",
                table: "TicketTypes",
                newName: "IX_TicketTypes_ScreeningId");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "SeatReservations",
                newName: "ScreeningId");

            migrationBuilder.RenameIndex(
                name: "IX_SeatReservations_EventId_SeatId",
                table: "SeatReservations",
                newName: "IX_SeatReservations_ScreeningId_SeatId");

            migrationBuilder.RenameIndex(
                name: "IX_SeatReservations_EventId",
                table: "SeatReservations",
                newName: "IX_SeatReservations_ScreeningId");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "Bookings",
                newName: "ScreeningId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_EventId",
                table: "Bookings",
                newName: "IX_Bookings_ScreeningId");

            migrationBuilder.CreateTable(
                name: "Screenings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<int>(type: "integer", nullable: false),
                    Screen = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Screenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Screenings_Events_EventId",
                        column: x => x.EventId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_EventId",
                table: "Screenings",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_Screenings_StartTime",
                table: "Screenings",
                column: "StartTime");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Screenings_ScreeningId",
                table: "Bookings",
                column: "ScreeningId",
                principalTable: "Screenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Screenings_ScreeningId",
                table: "SeatReservations",
                column: "ScreeningId",
                principalTable: "Screenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTypes_Screenings_ScreeningId",
                table: "TicketTypes",
                column: "ScreeningId",
                principalTable: "Screenings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Screenings_ScreeningId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_SeatReservations_Screenings_ScreeningId",
                table: "SeatReservations");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketTypes_Screenings_ScreeningId",
                table: "TicketTypes");

            migrationBuilder.DropTable(
                name: "Screenings");

            migrationBuilder.RenameColumn(
                name: "ScreeningId",
                table: "TicketTypes",
                newName: "EventId");

            migrationBuilder.RenameIndex(
                name: "IX_TicketTypes_ScreeningId",
                table: "TicketTypes",
                newName: "IX_TicketTypes_EventId");

            migrationBuilder.RenameColumn(
                name: "ScreeningId",
                table: "SeatReservations",
                newName: "EventId");

            migrationBuilder.RenameIndex(
                name: "IX_SeatReservations_ScreeningId_SeatId",
                table: "SeatReservations",
                newName: "IX_SeatReservations_EventId_SeatId");

            migrationBuilder.RenameIndex(
                name: "IX_SeatReservations_ScreeningId",
                table: "SeatReservations",
                newName: "IX_SeatReservations_EventId");

            migrationBuilder.RenameColumn(
                name: "ScreeningId",
                table: "Bookings",
                newName: "EventId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_ScreeningId",
                table: "Bookings",
                newName: "IX_Bookings_EventId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Events_EventId",
                table: "Bookings",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SeatReservations_Events_EventId",
                table: "SeatReservations",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketTypes_Events_EventId",
                table: "TicketTypes",
                column: "EventId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
