using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    /// <inheritdoc />
    public partial class ChatGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatGroupMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsExplicit = table.Column<bool>(type: "bit", nullable: false),
                    ReadId = table.Column<int>(type: "int", nullable: false),
                    DeliveredId = table.Column<int>(type: "int", nullable: false),
                    JoinedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatGroupMembers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatGroups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AllUsers = table.Column<bool>(type: "bit", nullable: false),
                    AdminsOnly = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastMessageId = table.Column<int>(type: "int", nullable: false),
                    LastMessageDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroupMembers_GroupId_UserName",
                table: "ChatGroupMembers",
                columns: new[] { "GroupId", "UserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroupMembers_UserName",
                table: "ChatGroupMembers",
                column: "UserName");

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_LastMessageId",
                table: "ChatGroups",
                column: "LastMessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatGroupMembers");

            migrationBuilder.DropTable(
                name: "ChatGroups");
        }
    }
}
