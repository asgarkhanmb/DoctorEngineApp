using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    public partial class CreatedContactInfoTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContactInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    YouTube = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Facebook = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TikTok = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WhatsApp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Weekdays = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Weekend = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactInfos", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContactInfos");
        }
    }
}
