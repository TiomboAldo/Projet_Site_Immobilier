using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    public partial class FixBienDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — fonctionne même si les colonnes existent déjà (déploiement partiel précédent)
            migrationBuilder.Sql(@"
                ALTER TABLE `Biens`
                    ADD COLUMN IF NOT EXISTS `CertificatPropriete`   LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `StatutCivil`            LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `RegimeMatrimonial`      LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `ADesEnfants`            TINYINT(1) NULL,
                    ADD COLUMN IF NOT EXISTS `DossierTechnique`       LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `PermisBatir`            LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `PlanBatiment`           LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `DossierCalculTechnique` LONGTEXT CHARACTER SET utf8mb4 NULL,
                    ADD COLUMN IF NOT EXISTS `DocumentsVerifies`      LONGTEXT CHARACTER SET utf8mb4 NULL;
            ");
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
