using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    public partial class AddTechnicianBio : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Bio",
                table: "Technicians",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bio",
                table: "Technicians");
        }
    }
}
