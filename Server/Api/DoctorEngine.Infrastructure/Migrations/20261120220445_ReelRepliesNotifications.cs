using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    /// <inheritdoc />
    public partial class ReelRepliesNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedDate",
                table: "ReelComments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ParentId",
                table: "ReelComments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplyToUserName",
                table: "ReelComments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sticker",
                table: "ReelComments",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ReelId = table.Column<int>(type: "int", nullable: true),
                    CommentId = table.Column<int>(type: "int", nullable: true),
                    Snippet = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReelComments_ParentId",
                table: "ReelComments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserName_Id",
                table: "Notifications",
                columns: new[] { "UserName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserName_IsRead",
                table: "Notifications",
                columns: new[] { "UserName", "IsRead" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_ReelComments_ParentId",
                table: "ReelComments");

            migrationBuilder.DropColumn(
                name: "EditedDate",
                table: "ReelComments");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ReelComments");

            migrationBuilder.DropColumn(
                name: "ReplyToUserName",
                table: "ReelComments");

            migrationBuilder.DropColumn(
                name: "Sticker",
                table: "ReelComments");
        }
    }
}
