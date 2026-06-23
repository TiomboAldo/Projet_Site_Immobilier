using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaidAfricaBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRolesOwnershipAndValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstValide",
                table: "Users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "Users",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "ProprietaireId",
                table: "Biens",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Biens_ProprietaireId",
                table: "Biens",
                column: "ProprietaireId");

            migrationBuilder.AddForeignKey(
                name: "FK_Biens_Users_ProprietaireId",
                table: "Biens",
                column: "ProprietaireId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Biens_Users_ProprietaireId",
                table: "Biens");

            migrationBuilder.DropIndex(
                name: "IX_Biens_ProprietaireId",
                table: "Biens");

            migrationBuilder.DropColumn(
                name: "EstValide",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProprietaireId",
                table: "Biens");
        }
    }
}
