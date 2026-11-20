using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    /// <inheritdoc />
    public partial class Reels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReelComments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReelId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReelComments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReelLikes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReelId = table.Column<int>(type: "int", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReelLikes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VideoPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(2200)", maxLength: 2200, nullable: true),
                    AuthorUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    LikeCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reels", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReelComments_ReelId_Id",
                table: "ReelComments",
                columns: new[] { "ReelId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ReelLikes_ReelId_UserName",
                table: "ReelLikes",
                columns: new[] { "ReelId", "UserName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reels_SoftDeleted_Id",
                table: "Reels",
                columns: new[] { "SoftDeleted", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReelComments");

            migrationBuilder.DropTable(
                name: "ReelLikes");

            migrationBuilder.DropTable(
                name: "Reels");
        }
    }
}
