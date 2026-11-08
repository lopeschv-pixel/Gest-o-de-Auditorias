using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Tests;

public sealed class RelogioFixo : IRelogio
{
    public DateTime UtcAgora { get; set; } = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class FabricaTeste : IDbContextFactory<NuClaudDbContext>
{
    private readonly DbContextOptions<NuClaudDbContext> _opcoes;
    public FabricaTeste(DbContextOptions<NuClaudDbContext> opcoes) => _opcoes = opcoes;
    public NuClaudDbContext CreateDbContext() => new(_opcoes);
}

/// <summary>
/// Banco SQLite em memória (rápido e sem dependências) com o mesmo modelo do EF Core.
/// Os testes de integração com SQL Server ficam em IntegracaoSqlServerTestes.
/// </summary>
public sealed class Ambiente : IDisposable
{
    private readonly SqliteConnection _conexao;
    public FabricaTeste Fabrica { get; }
    public RelogioFixo Relogio { get; } = new();
    public Ator Admin { get; } = new(999, "admin", "Admin Teste", PerfilUsuario.Administrador, null);

    public CadastroServico Cadastro => new(Fabrica, Relogio);
    public NormaServico Normas => new(Fabrica, Relogio);
    public AuditorServico Auditores => new(Fabrica, Relogio);
    public UsuarioServico Usuarios => new(Fabrica, Relogio);
    public ProgramacaoServico Programacao => new(Fabrica, Relogio);
    public PlanoServico Planos => new(Fabrica, Relogio);
    public RelatorioServico Relatorios => new(Fabrica, Relogio);
    public ConsultasServico Consultas => new(Fabrica);

    public int Iso9001, Iso14001, Asme;
    public int AreaA, AreaB, Macro, ProcCompras, ProcRecebimento;
    public int AudLider1, AudLider2, AudAuditor, AudObservador, AudSemHab;

    public Ambiente()
    {
        _conexao = new SqliteConnection("DataSource=:memory:");
        _conexao.Open();
        Fabrica = new FabricaTeste(new DbContextOptionsBuilder<NuClaudDbContext>().UseSqlite(_conexao).Options);
        using var db = Fabrica.CreateDbContext();
        db.Database.EnsureCreated();
        SementeCatalogo.AplicarAsync(db, Relogio.UtcAgora).GetAwaiter().GetResult();
        Preparar().GetAwaiter().GetResult();
    }

    public int Req(int normaId, string codigo)
    {
        using var db = Fabrica.CreateDbContext();
        return db.Requisitos.Single(r => r.NormaId == normaId && r.Codigo == codigo).Id;
    }

    private async Task Preparar()
    {
        var normas = await Normas.ListarAsync();
        Iso9001 = normas.Single(n => n.Nome == "ISO 9001:2015").Id;
        Iso14001 = normas.Single(n => n.Nome == "ISO 14001").Id;
        Asme = normas.Single(n => n.Nome == "ASME III").Id;
        AreaA = await Cadastro.SalvarAreaAsync(Admin, new AreaEdicao { Nome = "Suprimentos" });
        AreaB = await Cadastro.SalvarAreaAsync(Admin, new AreaEdicao { Nome = "Engenharia" });
        Macro = await Cadastro.SalvarMacroprocessoAsync(Admin, new MacroprocessoEdicao { Nome = "Suprimentos" });
        ProcCompras = await Cadastro.SalvarProcessoAsync(Admin, new ProcessoEdicao
        {
            Nome = "Compras", MacroprocessoId = Macro, NormaIds = { Iso9001, Iso14001 },
            RequisitoIds = { Req(Iso9001, "8.4"), Req(Iso9001, "8.4.1"), Req(Iso14001, "8.1") }
        });
        ProcRecebimento = await Cadastro.SalvarProcessoAsync(Admin, new ProcessoEdicao
        {
            Nome = "Recebimento", MacroprocessoId = Macro, NormaIds = { Iso9001 },
            RequisitoIds = { Req(Iso9001, "8.4"), Req(Iso9001, "8.6") }
        });
        HabilitacaoEdicao H(int n, bool l, bool a, bool o) => new() { NormaId = n, PodeLiderar = l, PodeAuditar = a, PodeObservar = o };
        AudLider1 = await Auditores.SalvarAsync(Admin, new AuditorEdicao { NomeCompleto = "Líder Um", Habilitacoes = { H(Iso9001, true, true, false), H(Iso14001, true, true, false) } });
        AudLider2 = await Auditores.SalvarAsync(Admin, new AuditorEdicao { NomeCompleto = "Líder Dois", Habilitacoes = { H(Iso9001, true, true, false) } });
        AudAuditor = await Auditores.SalvarAsync(Admin, new AuditorEdicao { NomeCompleto = "Auditor Três", Habilitacoes = { H(Iso9001, false, true, false) } });
        AudObservador = await Auditores.SalvarAsync(Admin, new AuditorEdicao { NomeCompleto = "Observador Quatro", Habilitacoes = { H(Iso9001, false, false, true) } });
        AudSemHab = await Auditores.SalvarAsync(Admin, new AuditorEdicao { NomeCompleto = "Sem Habilitação" });
    }

    /// <summary>Usuário comum vinculado ao auditor informado.</summary>
    public async Task<Ator> UsuarioVinculado(string rede, int auditorId)
    {
        var id = await Usuarios.SalvarAsync(Admin, new UsuarioEdicao
        {
            NomeRede = rede, NomeCompleto = rede, Email = rede + "@exemplo.invalid", AuditorId = auditorId, SenhaInicial = "SenhaTeste123"
        });
        return new Ator(id, rede, rede, PerfilUsuario.Usuario, auditorId);
    }

    public ProgramacaoEdicao Interna(int ano = 2027, int? lider = null) => new()
    {
        Tipo = TipoAuditoria.Interna, Ano = ano, MesPrevisto = 5, NormaIds = { Iso9001 }, AreaIds = { AreaA },
        ProcessoIds = { ProcRecebimento }, Itens = { new(ProcRecebimento, Req(Iso9001, "8.4")), new(ProcRecebimento, Req(Iso9001, "8.6")) },
        LiderId = lider ?? AudLider1, AuditorIds = { AudAuditor }
    };

    public ProgramacaoEdicao Fornecedor(int ano = 2027) => new()
    {
        Tipo = TipoAuditoria.Fornecedor, Ano = ano, MesPrevisto = 6, NormaIds = { Iso9001 },
        FornecedorNome = "Fornecedor X", FornecedorCnpj = SementeDemonstracao.GerarCnpj("112223330001"),
        FornecedorEndereco = "Rua A, 1", EscopoFornecimento = "Chapas", Itens = { new(null, Req(Iso9001, "8.4")) },
        LiderId = AudLider1
    };

    public async Task<int> ComPlano(ProgramacaoEdicao p)
    {
        var id = await Programacao.SalvarAsync(Admin, p);
        await Planos.SalvarAsync(Admin, new PlanoEdicao
        {
            AuditoriaId = id,
            Periodos = { new() { Data = new DateOnly(p.Ano, 5, 10), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(12, 0) } }
        });
        return id;
    }

    public void Dispose() => _conexao.Dispose();
}
