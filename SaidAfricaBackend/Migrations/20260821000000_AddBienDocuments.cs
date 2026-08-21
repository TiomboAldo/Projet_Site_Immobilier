using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    public partial class AddBienDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Documents spécifiques Terrain
            migrationBuilder.AddColumn<string>(
                name: "CertificatPropriete",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StatutCivil",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "RegimeMatrimonial",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool?>(
                name: "ADesEnfants",
                table: "Biens",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DossierTechnique",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // Documents spécifiques Immeuble
            migrationBuilder.AddColumn<string>(
                name: "PermisBatir",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "PlanBatiment",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DossierCalculTechnique",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "DocumentsVerifies",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CertificatPropriete",    table: "Biens");
            migrationBuilder.DropColumn(name: "StatutCivil",             table: "Biens");
            migrationBuilder.DropColumn(name: "RegimeMatrimonial",       table: "Biens");
            migrationBuilder.DropColumn(name: "ADesEnfants",             table: "Biens");
            migrationBuilder.DropColumn(name: "DossierTechnique",        table: "Biens");
            migrationBuilder.DropColumn(name: "PermisBatir",             table: "Biens");
            migrationBuilder.DropColumn(name: "PlanBatiment",            table: "Biens");
            migrationBuilder.DropColumn(name: "DossierCalculTechnique",  table: "Biens");
            migrationBuilder.DropColumn(name: "DocumentsVerifies",       table: "Biens");
        }
    }
}
