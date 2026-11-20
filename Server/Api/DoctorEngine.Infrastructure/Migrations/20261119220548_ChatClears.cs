using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    /// <inheritdoc />
    public partial class ChatClears : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatClears",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Conversation = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ClearedId = table.Column<int>(type: "int", nullable: false),
                    ClearedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatClears", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatClears_UserName_Conversation",
                table: "ChatClears",
                columns: new[] { "UserName", "Conversation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatClears");
        }
    }
}
