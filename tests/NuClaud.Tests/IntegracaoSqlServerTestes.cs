using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Tests;

/// <summary>
/// Testes contra SQL Server real (migrações versionadas e concorrência na geração de códigos).
/// Executados somente quando NUCLAUD_TESTE_SQLSERVER estiver definida com uma conexão que possa criar bancos,
/// por exemplo: Server=localhost,1433;User Id=...;Password=...;TrustServerCertificate=true
/// </summary>
public class IntegracaoSqlServerTestes
{
    private static readonly string? Conexao = Environment.GetEnvironmentVariable("NUCLAUD_TESTE_SQLSERVER");

    [Fact]
    public async Task Migracoes_e_codigos_unicos_com_criacoes_simultaneas()
    {
        if (string.IsNullOrWhiteSpace(Conexao)) return; // ambiente sem SQL Server: teste não aplicável
        var banco = "NuClaud_TesteInt_" + Guid.NewGuid().ToString("N")[..8];
        var cs = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(Conexao) { InitialCatalog = banco }.ConnectionString;
        var fabrica = new FabricaTeste(new DbContextOptionsBuilder<NuClaudDbContext>().UseSqlServer(cs).Options);
        var relogio = new RelogioFixo();
        try
        {
            await using (var db = fabrica.CreateDbContext())
            {
                await db.Database.MigrateAsync();
                Assert.Empty(await db.Database.GetPendingMigrationsAsync());
                await SementeCatalogo.AplicarAsync(db, relogio.UtcAgora);
                await SementeCatalogo.AplicarAsync(db, relogio.UtcAgora); // idempotente
                Assert.Equal(6, await db.Normas.CountAsync());
            }
            var admin = new Ator(999, "admin", "Admin", PerfilUsuario.Administrador, null);
            var cad = new CadastroServico(fabrica, relogio);
            var normas = await new NormaServico(fabrica, relogio).ListarAsync(incluirRequisitos: true);
            var iso = normas.Single(n => n.Nome == "ISO 9001:2015");
            var area = await cad.SalvarAreaAsync(admin, new AreaEdicao { Nome = "Área" });
            var lider = await new AuditorServico(fabrica, relogio).SalvarAsync(admin, new AuditorEdicao
            {
                NomeCompleto = "Líder", Habilitacoes = { new() { NormaId = iso.Id, PodeLiderar = true, PodeAuditar = true } }
            });

            var tarefas = Enumerable.Range(0, 20).Select(_ => Task.Run(() => new ProgramacaoServico(fabrica, relogio).SalvarAsync(admin, new ProgramacaoEdicao
            {
                Tipo = TipoAuditoria.Interna, Ano = 2027, MesPrevisto = 3, NormaIds = { iso.Id }, AreaIds = { area },
                RequisitoIds = { iso.Requisitos.First().Id }, LiderId = lider
            })));
            var ids = await Task.WhenAll(tarefas);
            await using var db2 = fabrica.CreateDbContext();
            var codigos = await db2.Auditorias.Select(a => a.Codigo).ToListAsync();
            Assert.Equal(20, ids.Distinct().Count());
            Assert.Equal(20, codigos.Distinct().Count());
            Assert.Equal(Enumerable.Range(1, 20).Select(i => $"AUDI-I-{i:D3}-2027").OrderBy(x => x), codigos.OrderBy(x => x));
        }
        finally
        {
            await using var db = fabrica.CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
