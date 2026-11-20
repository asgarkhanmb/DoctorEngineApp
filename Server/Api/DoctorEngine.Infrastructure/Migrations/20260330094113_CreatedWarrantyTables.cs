using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DoctorEngineApp.Migrations
{
    public partial class CreatedWarrantyTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Warranties_WarrantyLogos_WarrantyLogoId",
                table: "Warranties");

            migrationBuilder.DropTable(
                name: "WarrantiesJobs");

            migrationBuilder.DropTable(
                name: "WarrantyLogos");

            migrationBuilder.DropTable(
                name: "JobCatalogs");

            migrationBuilder.DropIndex(
                name: "IX_Warranties_WarrantyLogoId",
                table: "Warranties");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "Warranties");

            migrationBuilder.DropColumn(
                name: "CustomerName",
                table: "Warranties");

            migrationBuilder.DropColumn(
                name: "Fuel",
                table: "Warranties");

            migrationBuilder.DropColumn(
                name: "Master",
                table: "Warranties");

            migrationBuilder.DropColumn(
                name: "WarrantyLogoId",
                table: "Warranties");

            migrationBuilder.RenameColumn(
                name: "Year",
                table: "Warranties",
                newName: "VehicleYear");

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "Warranties",
                newName: "VehicleModel");

            migrationBuilder.RenameColumn(
                name: "Model",
                table: "Warranties",
                newName: "CustomerFullName");

            migrationBuilder.CreateTable(
                name: "WarrantyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PartName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarrantyMonth = table.Column<int>(type: "int", nullable: false),
                    WarrantyId = table.Column<int>(type: "int", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarrantyItems_Warranties_WarrantyId",
                        column: x => x.WarrantyId,
                        principalTable: "Warranties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarrantyItems_WarrantyId",
                table: "WarrantyItems",
                column: "WarrantyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarrantyItems");

            migrationBuilder.RenameColumn(
                name: "VehicleYear",
                table: "Warranties",
                newName: "Year");

            migrationBuilder.RenameColumn(
                name: "VehicleModel",
                table: "Warranties",
                newName: "Phone");

            migrationBuilder.RenameColumn(
                name: "CustomerFullName",
                table: "Warranties",
                newName: "Model");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "Warranties",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CustomerName",
                table: "Warranties",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Fuel",
                table: "Warranties",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Master",
                table: "Warranties",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "WarrantyLogoId",
                table: "Warranties",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobCatalogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DefaultWarrantyMonths = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false)
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
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarrantyLogos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarrantiesJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobCatalogId = table.Column<int>(type: "int", nullable: false),
                    WarrantyId = table.Column<int>(type: "int", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SoftDeleted = table.Column<bool>(type: "bit", nullable: false),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: false)
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

            migrationBuilder.AddForeignKey(
                name: "FK_Warranties_WarrantyLogos_WarrantyLogoId",
                table: "Warranties",
                column: "WarrantyLogoId",
                principalTable: "WarrantyLogos",
                principalColumn: "Id");
        }
    }
}
