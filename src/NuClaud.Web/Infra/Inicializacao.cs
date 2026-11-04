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
