using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EMSDALLibrary.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityName = table.Column<string>(type: "text", nullable: false),
                    EntityKey = table.Column<string>(type: "text", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    Changes = table.Column<string>(type: "jsonb", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    UserRole = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogs_CreatedAt",
                table: "ChangeLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogs_EntityName_EntityKey",
                table: "ChangeLogs",
                columns: new[] { "EntityName", "EntityKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogs_UserId",
                table: "ChangeLogs",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeLogs");
        }
    }
}
