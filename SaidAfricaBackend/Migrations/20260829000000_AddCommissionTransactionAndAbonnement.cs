using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    public partial class AddCommissionTransactionAndAbonnement : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Champs abonnement sur Users (idempotent) ──────────────────────
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `_lev_add_abonnement_cols`;");
            migrationBuilder.Sql(@"
CREATE PROCEDURE `_lev_add_abonnement_cols`()
BEGIN
    DECLARE CONTINUE HANDLER FOR 1060 BEGIN END;
    ALTER TABLE `Users` ADD COLUMN `DateDevenirPro`      DATETIME(6) NULL;
    ALTER TABLE `Users` ADD COLUMN `AbonnementExpireLe`  DATETIME(6) NULL;
END");
            migrationBuilder.Sql("CALL `_lev_add_abonnement_cols`();");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS `_lev_add_abonnement_cols`;");

            // ── Table CommissionTransactions ───────────────────────────────────
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS `CommissionTransactions` (
    `Id`                    INT NOT NULL AUTO_INCREMENT,
    `BienId`                INT NULL,
    `AgentId`               INT NOT NULL,
    `TypeTransaction`       LONGTEXT CHARACTER SET utf8mb4 NOT NULL,
    `MontantBrut`           DECIMAL(14,2) NOT NULL,
    `TauxTaxePct`           DECIMAL(5,2)  NOT NULL,
    `MontantTaxe`           DECIMAL(14,2) NOT NULL,
    `MontantNetApresImpots` DECIMAL(14,2) NOT NULL,
    `CommissionLevetimmo`   DECIMAL(14,2) NOT NULL,
    `CommissionAgent`       DECIMAL(14,2) NOT NULL,
    `GereParLevetimmo`      TINYINT(1)    NOT NULL DEFAULT 1,
    `Statut`                LONGTEXT CHARACTER SET utf8mb4 NOT NULL,
    `Notes`                 LONGTEXT CHARACTER SET utf8mb4 NULL,
    `CreatedAt`             DATETIME(6)   NOT NULL,
    CONSTRAINT `PK_CommissionTransactions` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_CommissionTransactions_Biens_BienId`
        FOREIGN KEY (`BienId`) REFERENCES `Biens` (`Id`) ON DELETE SET NULL,
    CONSTRAINT `FK_CommissionTransactions_Users_AgentId`
        FOREIGN KEY (`AgentId`) REFERENCES `Users` (`Id`) ON DELETE CASCADE,
    INDEX `IX_CommissionTransactions_BienId`  (`BienId`),
    INDEX `IX_CommissionTransactions_AgentId` (`AgentId`)
) CHARACTER SET=utf8mb4;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS `CommissionTransactions`;");
            migrationBuilder.DropColumn(name: "DateDevenirPro",     table: "Users");
            migrationBuilder.DropColumn(name: "AbonnementExpireLe", table: "Users");
        }
    }
}
