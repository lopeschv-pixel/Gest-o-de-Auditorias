using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Servicos;

namespace NuClaud.Web.Infra;

public static class Inicializacao
{
    /// <summary>
    /// Aplica migrações (quando configurado), carga do catálogo de normas, administrador inicial e,
    /// somente em ambiente de demonstração, dados fictícios.
    /// </summary>
    public static async Task ExecutarAsync(WebApplication app)
    {
        var cfg = app.Configuration;
        var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("NuClaud.Inicializacao");
        await using var escopo = app.Services.CreateAsyncScope();
        var sp = escopo.ServiceProvider;
        var fabrica = sp.GetRequiredService<IDbContextFactory<NuClaudDbContext>>();
        var relogio = sp.GetRequiredService<IRelogio>();

        // Aguarda o SQL Server responder (ex.: servidor e banco reiniciados ao mesmo tempo).
        // Testa a conexão com o servidor (banco master), pois o banco do NUCLAUD pode ainda não existir.
        var csServidor = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cfg.GetConnectionString("NuClaud")) { InitialCatalog = "master", ConnectTimeout = 5 }.ConnectionString;
        for (int tentativa = 1; ; tentativa++)
        {
            try
            {
                await using var conexao = new Microsoft.Data.SqlClient.SqlConnection(csServidor);
                await conexao.OpenAsync();
                break;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (tentativa < 24)
            {
                log.LogWarning("SQL Server indisponível (erro {Numero}). Nova tentativa em 5 s.", ex.Number);
                await Task.Delay(5000);
            }
        }

        await using (var db = await fabrica.CreateDbContextAsync())
        {
            if (cfg.GetValue("BancoDeDados:AplicarMigracoesAoIniciar", false))
            {
                var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
                if (pendentes.Count > 0)
                {
                    log.LogInformation("Aplicando {Quantidade} migração(ões): {Lista}", pendentes.Count, string.Join(", ", pendentes));
                    await db.Database.MigrateAsync();
                }
            }
            else if ((await db.Database.GetPendingMigrationsAsync()).Any())
            {
                log.LogWarning("Há migrações pendentes. Aplique-as com o script SQL ou habilite BancoDeDados:AplicarMigracoesAoIniciar (ver docs/IMPLANTACAO.md).");
            }
            await SementeCatalogo.AplicarAsync(db, relogio.UtcAgora);
        }

        var usuarios = sp.GetRequiredService<UsuarioServico>();
        var criado = await usuarios.GarantirAdministradorInicialAsync(
            cfg["Inicializacao:AdminNomeRede"], cfg["Inicializacao:AdminNomeCompleto"], cfg["Inicializacao:AdminEmail"], cfg["Inicializacao:AdminSenhaInicial"]);
        if (criado) log.LogInformation("Administrador inicial criado (troca de senha obrigatória no primeiro acesso).");

        if (cfg.GetValue("Demonstracao:Habilitada", false))
        {
            var semente = new SementeDemonstracao(fabrica, relogio);
            if (await semente.AplicarAsync(cfg["Demonstracao:SenhaUsuarios"]))
                log.LogWarning("Dados FICTÍCIOS de demonstração carregados. Não use este banco em produção.");
        }
    }
}
