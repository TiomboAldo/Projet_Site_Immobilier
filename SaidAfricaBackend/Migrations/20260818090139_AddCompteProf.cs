using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddCompteProf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NIU",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NomAgence",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TypeCompteProf",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "CNIVersoPath",
                table: "DemandesProprietaire",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NIU",
                table: "DemandesProprietaire",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "NomAgence",
                table: "DemandesProprietaire",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TypeCompteProf",
                table: "DemandesProprietaire",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NIU",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NomAgence",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TypeCompteProf",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CNIVersoPath",
                table: "DemandesProprietaire");

            migrationBuilder.DropColumn(
                name: "NIU",
                table: "DemandesProprietaire");

            migrationBuilder.DropColumn(
                name: "NomAgence",
                table: "DemandesProprietaire");

            migrationBuilder.DropColumn(
                name: "TypeCompteProf",
                table: "DemandesProprietaire");
        }
    }
}
