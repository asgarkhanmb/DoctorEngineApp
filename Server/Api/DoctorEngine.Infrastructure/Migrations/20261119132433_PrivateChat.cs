using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    /// <inheritdoc />
    public partial class PrivateChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConversationUserName",
                table: "ChatMessages",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyToId",
                table: "ChatMessages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ChatLastSeen",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ChatOnline",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ChatConversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LastMessageId = table.Column<int>(type: "int", nullable: false),
                    LastMessageDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserDeliveredId = table.Column<int>(type: "int", nullable: false),
                    UserReadId = table.Column<int>(type: "int", nullable: false),
                    AdminDeliveredId = table.Column<int>(type: "int", nullable: false),
                    AdminReadId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatConversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Emoji = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatReactions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ConversationUserName_Id",
                table: "ChatMessages",
                columns: new[] { "ConversationUserName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_LastMessageId",
                table: "ChatConversations",
                column: "LastMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatConversations_UserName",
                table: "ChatConversations",
                column: "UserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatReactions_MessageId_UserName",
                table: "ChatReactions",
                columns: new[] { "MessageId", "UserName" },
                unique: true);

            // ---------- Köhnə ortaq otaqdakı mesajlar → şəxsi yazışmalar ----------
            // Müştərinin mesajı — öz yazışmasına
            migrationBuilder.Sql(@"
UPDATE ChatMessages SET ConversationUserName = UserName WHERE SenderIsAdmin = 0;");

            // Ustanın mesajı — ondan əvvəl yazan son müştərinin yazışmasına (cavab verdiyi şəxs).
            // Əvvəl heç bir müştəri yazmayıbsa — yazışmasız qalır (görünmür).
            migrationBuilder.Sql(@"
UPDATE m SET ConversationUserName = (
    SELECT TOP 1 p.UserName FROM ChatMessages p
    WHERE p.SenderIsAdmin = 0 AND p.Id < m.Id
    ORDER BY p.Id DESC)
FROM ChatMessages m
WHERE m.SenderIsAdmin = 1;");

            // Yazışmaların vəziyyəti: köhnə mesajların hamısı çatmış və oxunmuş sayılır
            migrationBuilder.Sql(@"
INSERT INTO ChatConversations (UserName, LastMessageId, LastMessageDate, UserDeliveredId, UserReadId, AdminDeliveredId, AdminReadId)
SELECT g.ConversationUserName, g.LastId, m.CreatedDate, g.LastId, g.LastId, g.LastId, g.LastId
FROM (SELECT ConversationUserName, MAX(Id) AS LastId
      FROM ChatMessages
      WHERE ConversationUserName IS NOT NULL AND SoftDeleted = 0
      GROUP BY ConversationUserName) g
JOIN ChatMessages m ON m.Id = g.LastId;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatConversations");

            migrationBuilder.DropTable(
                name: "ChatReactions");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ConversationUserName_Id",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ConversationUserName",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ChatLastSeen",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ChatOnline",
                table: "AspNetUsers");
        }
    }
}
