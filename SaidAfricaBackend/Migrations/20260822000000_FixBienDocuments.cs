using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    public partial class FixBienDocuments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CONTINUE HANDLER FOR 1060 = ignore "duplicate column" si déjà créée (idempotent, MySQL pur)
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `_lev_fix_bien_cols`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `_lev_fix_bien_cols`()
BEGIN
    DECLARE CONTINUE HANDLER FOR 1060 BEGIN END;
    ALTER TABLE `Biens` ADD COLUMN `CertificatPropriete`   LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `StatutCivil`            LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `RegimeMatrimonial`      LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `ADesEnfants`            TINYINT(1) NULL;
    ALTER TABLE `Biens` ADD COLUMN `DossierTechnique`       LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `PermisBatir`            LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `PlanBatiment`           LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `DossierCalculTechnique` LONGTEXT CHARACTER SET utf8mb4 NULL;
    ALTER TABLE `Biens` ADD COLUMN `DocumentsVerifies`      LONGTEXT CHARACTER SET utf8mb4 NULL;
END");
            migrationBuilder.Sql("CALL `_lev_fix_bien_cols`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `_lev_fix_bien_cols`;");
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
