using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    public partial class AddReservationTrackingCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingCode",
                table: "Reservations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_TrackingCode",
                table: "Reservations",
                column: "TrackingCode",
                unique: true,
                filter: "[TrackingCode] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reservations_TrackingCode",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "TrackingCode",
                table: "Reservations");
        }
    }
}
