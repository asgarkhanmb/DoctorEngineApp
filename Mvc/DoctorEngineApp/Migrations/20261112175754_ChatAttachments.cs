using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    public partial class ChatAttachments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "ChatMessages",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "ChatMessages",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "AttachmentType",
                table: "ChatMessages",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentUrl",
                table: "ChatMessages",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EditedDate",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SenderIsAdmin",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Mövcud mesajlar: göndərən admin idisə "Usta" nişanı
            migrationBuilder.Sql(@"
                UPDATE cm SET SenderIsAdmin = 1
                FROM ChatMessages cm
                JOIN AspNetUsers u ON u.UserName = cm.UserName
                JOIN AspNetUserRoles ur ON ur.UserId = u.Id
                JOIN AspNetRoles r ON r.Id = ur.RoleId
                WHERE r.Name = 'Admin';");
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachmentType",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "AttachmentUrl",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "EditedDate",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "SenderIsAdmin",
                table: "ChatMessages");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<string>(
                name: "Message",
                table: "ChatMessages",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
