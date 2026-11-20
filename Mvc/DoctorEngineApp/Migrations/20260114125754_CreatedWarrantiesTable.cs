using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    public partial class CreatedWarrantiesTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobCatalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DefaultWarrantyMonths = table.Column<int>(type: "int", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobCatalogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarrantyLogos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyLogos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Warranties",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Fuel = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Master = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Terms = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarrantyLogoId = table.Column<int>(type: "int", nullable: true),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warranties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warranties_WarrantyLogos_WarrantyLogoId",
                        column: x => x.WarrantyLogoId,
                        principalTable: "WarrantyLogos",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WarrantiesJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarrantyId = table.Column<int>(type: "int", nullable: false),
                    JobCatalogId = table.Column<int>(type: "int", nullable: false),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantiesJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantiesJobs_JobCatalogs_JobCatalogId",
                        column: x => x.JobCatalogId,
                        principalTable: "JobCatalogs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WarrantiesJobs_Warranties_WarrantyId",
                        column: x => x.WarrantyId,
                        principalTable: "Warranties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warranties_WarrantyLogoId",
                table: "Warranties",
                column: "WarrantyLogoId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantiesJobs_JobCatalogId",
                table: "WarrantiesJobs",
                column: "JobCatalogId");

            migrationBuilder.CreateIndex(
                name: "IX_WarrantiesJobs_WarrantyId",
                table: "WarrantiesJobs",
                column: "WarrantyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarrantiesJobs");

            migrationBuilder.DropTable(
                name: "JobCatalogs");

            migrationBuilder.DropTable(
                name: "Warranties");

            migrationBuilder.DropTable(
                name: "WarrantyLogos");
        }
    }
}
