using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddModerationBien : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotifsRejet",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "StatutPublication",
                table: "Biens",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TitreFoncierPath",
                table: "Biens",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "ValideLe",
                table: "Biens",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ValideParAdminId",
                table: "Biens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Biens_ValideParAdminId",
                table: "Biens",
                column: "ValideParAdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_Biens_Users_ValideParAdminId",
                table: "Biens",
                column: "ValideParAdminId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Biens déjà publiés avant la modération → pré-validés
            migrationBuilder.Sql("UPDATE `Biens` SET `StatutPublication` = 'Validée' WHERE `EstDisponible` = 1");
            migrationBuilder.Sql("UPDATE `Biens` SET `StatutPublication` = 'En attente' WHERE `EstDisponible` = 0 AND `StatutPublication` = ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Biens_Users_ValideParAdminId",
                table: "Biens");

            migrationBuilder.DropIndex(
                name: "IX_Biens_ValideParAdminId",
                table: "Biens");

            migrationBuilder.DropColumn(
                name: "MotifsRejet",
                table: "Biens");

            migrationBuilder.DropColumn(
                name: "StatutPublication",
                table: "Biens");

            migrationBuilder.DropColumn(
                name: "TitreFoncierPath",
                table: "Biens");

            migrationBuilder.DropColumn(
                name: "ValideLe",
                table: "Biens");

            migrationBuilder.DropColumn(
                name: "ValideParAdminId",
                table: "Biens");
        }
    }
}
