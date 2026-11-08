using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuClaud.Core.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class RequisitosPorProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditoriaRequisitos_AuditoriaId_RequisitoId",
                table: "AuditoriaRequisitos");

            migrationBuilder.AddColumn<string>(
                name: "NomeProcesso",
                table: "AuditoriaRequisitos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessoId",
                table: "AuditoriaRequisitos",
                type: "int",
                nullable: true);

            // Preserva dados existentes: requisitos que vieram de um único processo passam a pertencer a esse processo.
            // EXEC: compilado na execução (o script idempotente roda num único lote com a criação da coluna).
            migrationBuilder.Sql(@"EXEC(N'UPDATE ar SET ar.ProcessoId = ap.ProcessoId, ar.NomeProcesso = ap.NomeProcesso
FROM AuditoriaRequisitos ar
JOIN AuditoriaProcessos ap ON ap.AuditoriaId = ar.AuditoriaId AND ap.NomeProcesso = ar.Origem
WHERE ar.Obrigatorio = 1 AND ar.ProcessoId IS NULL;');");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaRequisitos_AuditoriaId_RequisitoId_ProcessoId",
                table: "AuditoriaRequisitos",
                columns: new[] { "AuditoriaId", "RequisitoId", "ProcessoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaRequisitos_ProcessoId",
                table: "AuditoriaRequisitos",
                column: "ProcessoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AuditoriaRequisitos_Processos_ProcessoId",
                table: "AuditoriaRequisitos",
                column: "ProcessoId",
                principalTable: "Processos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditoriaRequisitos_Processos_ProcessoId",
                table: "AuditoriaRequisitos");

            migrationBuilder.DropIndex(
                name: "IX_AuditoriaRequisitos_AuditoriaId_RequisitoId_ProcessoId",
                table: "AuditoriaRequisitos");

            migrationBuilder.DropIndex(
                name: "IX_AuditoriaRequisitos_ProcessoId",
                table: "AuditoriaRequisitos");

            migrationBuilder.DropColumn(
                name: "NomeProcesso",
                table: "AuditoriaRequisitos");

            migrationBuilder.DropColumn(
                name: "ProcessoId",
                table: "AuditoriaRequisitos");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaRequisitos_AuditoriaId_RequisitoId",
                table: "AuditoriaRequisitos",
                columns: new[] { "AuditoriaId", "RequisitoId" },
                unique: true);
        }
    }
}
