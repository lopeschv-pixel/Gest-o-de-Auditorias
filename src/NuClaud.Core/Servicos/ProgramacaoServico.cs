using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed class ProgramacaoEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public TipoAuditoria Tipo { get; set; } = TipoAuditoria.Interna;
    public int Ano { get; set; }
    public int MesPrevisto { get; set; }
    public HashSet<int> NormaIds { get; set; } = new();
    public HashSet<int> AreaIds { get; set; } = new();
    public HashSet<int> MacroprocessoIds { get; set; } = new();
    public HashSet<int> ProcessoIds { get; set; } = new();
    /// <summary>Requisitos a auditar: em um processo (ProcessoId) ou gerais (ProcessoId nulo).</summary>
    public HashSet<ItemRequisito> Itens { get; set; } = new();
    public int? LiderId { get; set; }
    public HashSet<int> AuditorIds { get; set; } = new();
    public HashSet<int> ObservadorIds { get; set; } = new();
    public string? FornecedorNome { get; set; }
    public string? FornecedorCnpj { get; set; }
    public string? FornecedorEndereco { get; set; }
    public string? EscopoFornecimento { get; set; }
}

/// <summary>Requisito a auditar em um processo específico (ProcessoId) ou como requisito geral (nulo).</summary>
public sealed record ItemRequisito(int? ProcessoId, int RequisitoId);

public sealed record RequisitoOpcao(int Id, int NormaId, string Codigo, string Titulo, bool Ativo, string Chave)
{
    public string Exibicao => OrdenacaoRequisitos.Exibicao(Codigo, Titulo);
}

public sealed class DadosFormularioProgramacao
{
    public List<Norma> Normas { get; init; } = new();
    public List<Area> Areas { get; init; } = new();
    public List<Macroprocesso> Macroprocessos { get; init; } = new();
    public List<ProcessoInfo> Processos { get; init; } = new();
    public List<RequisitoOpcao> Requisitos { get; init; } = new();
    public List<AuditorInfo> Auditores { get; init; } = new();
}

public sealed class SituacaoEdicaoProgramacao
{
    public ProgramacaoEdicao Dados { get; init; } = new();
    public string Codigo { get; init; } = "";
    public bool TemPlano { get; init; }
    public bool TemRelatorio { get; init; }
    public SituacaoRelatorio? SituacaoRelatorio { get; init; }
    /// <summary>Mensagem quando a estrutura (normas, requisitos, equipe, áreas...) não pode ser alterada.</summary>
    public string? BloqueioEstrutura { get; init; }
    /// <summary>Itens já classificados em constatações do relatório (não podem ser retirados).</summary>
    public HashSet<ItemRequisito> ItensEmConstatacoes { get; init; } = new();
}

public sealed record AuditoriaResumo(int Id, string Codigo, TipoAuditoria Tipo, int Ano, int MesPrevisto, string Identificacao,
    IReadOnlyList<string> Normas, string? Lider, SituacaoAuditoria Situacao);

public class ProgramacaoServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public ProgramacaoServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public static string? MotivoBloqueioEstrutura(Auditoria a) => a.Relatorio?.Situacao switch
    {
        SituacaoRelatorio.EmElaboracao => "O relatório desta auditoria já foi iniciado. Normas, requisitos, equipe, áreas, processos e dados do fornecedor " +
                                           "ficam bloqueados para não tornar o relatório incoerente. Apenas o mês previsto pode ser alterado.",
        SituacaoRelatorio.Concluido => "O relatório desta auditoria está concluído. A programação fica bloqueada; para corrigir, reabra o relatório para revisão.",
        _ => null
    };

    // ------------------------------------------------------------------ Consultas

    public async Task<DadosFormularioProgramacao> ObterDadosFormularioAsync(Ator ator, int? auditoriaId = null)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var usados = auditoriaId is int id
            ? await db.Auditorias.AsNoTracking().Where(a => a.Id == id).Select(a => new
            {
                Normas = a.Normas.Select(n => n.NormaId).ToList(),
                Areas = a.Areas.Select(n => n.AreaId).ToList(),
                Macros = a.Macroprocessos.Select(n => n.MacroprocessoId).ToList(),
                Processos = a.Processos.Select(n => n.ProcessoId).ToList(),
                Requisitos = a.Requisitos.Select(n => n.RequisitoId).ToList(),
            }).FirstOrDefaultAsync()
            : null;
        var normasUsadas = usados?.Normas ?? new();
        var areasUsadas = usados?.Areas ?? new();
        var macrosUsados = usados?.Macros ?? new();
        var procUsados = usados?.Processos ?? new();
        var reqUsados = usados?.Requisitos ?? new();

        var normas = await db.Normas.AsNoTracking().Where(n => n.Ativo || normasUsadas.Contains(n.Id)).OrderBy(n => n.Ordem).ThenBy(n => n.Nome).ToListAsync();
        var areas = await db.Areas.AsNoTracking().Where(n => n.Ativo || areasUsadas.Contains(n.Id)).OrderBy(n => n.Nome).ToListAsync();
        var macros = await db.Macroprocessos.AsNoTracking().Where(n => n.Ativo || macrosUsados.Contains(n.Id)).OrderBy(n => n.Nome).ToListAsync();
        var processos = await db.Processos.AsNoTracking().Include(p => p.Normas).Include(p => p.RequisitosObrigatorios).ThenInclude(r => r.Requisito)
            .Where(p => p.Ativo || procUsados.Contains(p.Id)).OrderBy(p => p.Nome).AsSplitQuery().ToListAsync();
        var requisitos = await db.Requisitos.AsNoTracking().Where(r => r.Ativo || reqUsados.Contains(r.Id))
            .OrderBy(r => r.NormaId).ThenBy(r => r.ChaveOrdenacao).ToListAsync();
        var auditores = await db.Auditores.AsNoTracking().Include(a => a.Habilitacoes).OrderBy(a => a.NomeCompleto).ToListAsync();

        return new DadosFormularioProgramacao
        {
            Normas = normas,
            Areas = areas,
            Macroprocessos = macros,
            Processos = processos.Select(ParaInfo).ToList(),
            Requisitos = requisitos.Select(r => new RequisitoOpcao(r.Id, r.NormaId, r.Codigo, r.Titulo, r.Ativo, r.ChaveOrdenacao)).ToList(),
            Auditores = auditores.Select(ParaInfo).ToList()
        };
    }

    public static ProcessoInfo ParaInfo(Processo p) => new(p.Id, p.Nome, p.MacroprocessoId,
        p.Normas.Select(n => n.NormaId).ToList(),
        p.RequisitosObrigatorios.Where(r => r.Requisito != null)
            .Select(r => new RequisitoProcessoInfo(r.RequisitoId, r.Requisito!.NormaId, r.Requisito.Ativo)).ToList());

    public static AuditorInfo ParaInfo(Auditor a) => new(a.Id, a.NomeCompleto, a.Ativo,
        a.Habilitacoes.Select(h => new HabilitacaoInfo(h.NormaId, h.PodeLiderar, h.PodeAuditar, h.PodeObservar)).ToList());

    public async Task<SituacaoEdicaoProgramacao> ObterParaEdicaoAsync(Ator ator, int id)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var a = await CarregarCompletaAsync(db, id, rastrear: false) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        var emConstatacoes = a.Relatorio == null ? new HashSet<ItemRequisito>() : a.Relatorio.Constatacoes.SelectMany(c => c.Requisitos)
            .Join(a.Requisitos, cr => cr.AuditoriaRequisitoId, ar => ar.Id, (cr, ar) => new ItemRequisito(ar.ProcessoId, ar.RequisitoId)).ToHashSet();
        return new SituacaoEdicaoProgramacao
        {
            Codigo = a.Codigo,
            TemPlano = a.Plano != null,
            TemRelatorio = a.Relatorio != null,
            SituacaoRelatorio = a.Relatorio?.Situacao,
            BloqueioEstrutura = MotivoBloqueioEstrutura(a),
            ItensEmConstatacoes = emConstatacoes,
            Dados = new ProgramacaoEdicao
            {
                Id = a.Id,
                Versao = a.Versao,
                Tipo = a.Tipo,
                Ano = a.Ano,
                MesPrevisto = a.MesPrevisto,
                NormaIds = a.Normas.Select(n => n.NormaId).ToHashSet(),
                AreaIds = a.Areas.Select(n => n.AreaId).ToHashSet(),
                MacroprocessoIds = a.Macroprocessos.Select(n => n.MacroprocessoId).ToHashSet(),
                ProcessoIds = a.Processos.Select(n => n.ProcessoId).ToHashSet(),
                Itens = a.Requisitos.Select(n => new ItemRequisito(n.ProcessoId, n.RequisitoId)).ToHashSet(),
                LiderId = a.Participantes.FirstOrDefault(p => p.Funcao == FuncaoAuditoria.Lider)?.AuditorId,
                AuditorIds = a.Participantes.Where(p => p.Funcao == FuncaoAuditoria.Auditor).Select(p => p.AuditorId).ToHashSet(),
                ObservadorIds = a.Participantes.Where(p => p.Funcao == FuncaoAuditoria.Observador).Select(p => p.AuditorId).ToHashSet(),
                FornecedorNome = a.FornecedorNome,
                FornecedorCnpj = a.FornecedorCnpj,
                FornecedorEndereco = a.FornecedorEndereco,
                EscopoFornecimento = a.EscopoFornecimento
            }
        };
    }

    internal static Task<Auditoria?> CarregarCompletaAsync(NuClaudDbContext db, int id, bool rastrear)
    {
        IQueryable<Auditoria> q = db.Auditorias;
        if (!rastrear) q = q.AsNoTracking();
        return q.Include(a => a.Areas).Include(a => a.Normas).Include(a => a.Macroprocessos).Include(a => a.Processos)
            .Include(a => a.Participantes).Include(a => a.Requisitos)
            .Include(a => a.Plano!).ThenInclude(p => p.Periodos)
            .Include(a => a.Relatorio!).ThenInclude(r => r.Constatacoes).ThenInclude(c => c.Requisitos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>Auditoria completa, somente se o ator puder visualizá-la (proteção contra troca de identificadores).</summary>
    public async Task<Auditoria> ObterDetalheAsync(Ator ator, int id)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        var a = await CarregarCompletaAsync(db, id, rastrear: false) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        if (!Permissoes.PodeVisualizar(ator, a)) throw new AcessoNegadoException("Você não tem acesso a esta auditoria.");
        Ordenar(a);
        return a;
    }

    internal static void Ordenar(Auditoria a)
    {
        a.Requisitos = a.Requisitos.OrderBy(r => r.NomeNorma).ThenBy(r => r.ChaveOrdenacao, StringComparer.Ordinal).ThenBy(r => r.NomeProcesso ?? "").ToList();
        a.Participantes = a.Participantes.OrderBy(p => p.Funcao).ThenBy(p => p.NomeAuditor).ToList();
        a.Areas = a.Areas.OrderBy(x => x.NomeArea).ToList();
        a.Normas = a.Normas.OrderBy(x => x.NomeNorma).ToList();
        a.Processos = a.Processos.OrderBy(x => x.NomeMacroprocesso).ThenBy(x => x.NomeProcesso).ToList();
        a.Macroprocessos = a.Macroprocessos.OrderBy(x => x.NomeMacroprocesso).ToList();
        if (a.Plano != null) a.Plano.Periodos = a.Plano.Periodos.OrderBy(p => p.Data).ThenBy(p => p.HoraInicio).ToList();
        if (a.Relatorio != null) a.Relatorio.Constatacoes = a.Relatorio.Constatacoes.OrderBy(c => c.Numero).ToList();
    }

    public async Task<List<AuditoriaResumo>> ListarAsync(Ator ator, int ano)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var lista = await db.Auditorias.AsNoTracking().Where(a => a.Ano == ano)
            .Include(a => a.Areas).Include(a => a.Normas).Include(a => a.Participantes).Include(a => a.Plano).Include(a => a.Relatorio)
            .AsSplitQuery()
            .OrderBy(a => a.MesPrevisto).ThenBy(a => a.Tipo).ThenBy(a => a.Sequencia).ToListAsync();
        return lista.Select(Resumir).ToList();
    }

    public static string Identificacao(Auditoria a) => a.Tipo == TipoAuditoria.Interna
        ? string.Join(", ", a.Areas.Select(x => x.NomeArea).OrderBy(x => x))
        : a.FornecedorNome ?? "";

    public static AuditoriaResumo Resumir(Auditoria a) => new(a.Id, a.Codigo, a.Tipo, a.Ano, a.MesPrevisto, Identificacao(a),
        a.Normas.Select(n => n.NomeNorma).OrderBy(x => x).ToList(), a.Lider?.NomeAuditor, a.Situacao);

    // ------------------------------------------------------------------ Salvar

    public async Task<int> SalvarAsync(Ator ator, ProgramacaoEdicao dto)
    {
        if (!Permissoes.PodeAdministrarProgramacao(ator))
            throw new AcessoNegadoException("Somente administradores podem programar ou alterar auditorias.");
        dto = Historico.Clonar(dto);
        Normalizar(dto);
        ValidarBasico(dto);
        const int tentativas = 6;
        for (int i = 1; ; i++)
        {
            try { return await SalvarInternoAsync(ator, dto); }
            catch (DbUpdateException ex) when (dto.Id == null && i < tentativas && Historico.EhViolacaoUnicidade(ex))
            {
                // Outra programação simultânea obteve a mesma sequência: tenta novamente com a próxima.
                await Task.Delay(Random.Shared.Next(5, 40));
            }
        }
    }

    /// <summary>Remove valores de campos que não se aplicam ao tipo (evita salvar dados ocultos).</summary>
    public static void Normalizar(ProgramacaoEdicao dto)
    {
        if (dto.Tipo == TipoAuditoria.Fornecedor)
        {
            dto.AreaIds.Clear();
            dto.MacroprocessoIds.Clear();
            dto.ProcessoIds.Clear();
            dto.Itens = dto.Itens.Select(i => i with { ProcessoId = null }).ToHashSet();
            dto.FornecedorNome = Validacoes.Limpar(dto.FornecedorNome);
            dto.FornecedorCnpj = Validacoes.FormatarCnpj(Validacoes.Limpar(dto.FornecedorCnpj));
            dto.FornecedorEndereco = Validacoes.Limpar(dto.FornecedorEndereco);
            dto.EscopoFornecimento = Validacoes.Limpar(dto.EscopoFornecimento);
        }
        else
        {
            dto.FornecedorNome = dto.FornecedorCnpj = dto.FornecedorEndereco = dto.EscopoFornecimento = null;
        }
        if (dto.LiderId is int l)
        {
            dto.AuditorIds.Remove(l);
            dto.ObservadorIds.Remove(l);
        }
    }

    private static void ValidarBasico(ProgramacaoEdicao dto)
    {
        var e = new List<string>();
        if (!Enum.IsDefined(dto.Tipo)) e.Add("Tipo de auditoria inválido.");
        if (dto.Ano is < 2000 or > 2100) e.Add("Informe um ano válido.");
        if (dto.MesPrevisto is < 1 or > 12) e.Add("Selecione o mês previsto.");
        if (dto.NormaIds.Count == 0) e.Add("Selecione ao menos uma norma.");
        if (dto.Tipo == TipoAuditoria.Interna && dto.AreaIds.Count == 0) e.Add("Selecione ao menos uma área.");
        if (dto.Tipo == TipoAuditoria.Fornecedor)
        {
            if (dto.FornecedorNome == null) e.Add("Informe o nome do fornecedor.");
            else if (dto.FornecedorNome.Length > 200) e.Add("O nome do fornecedor deve ter até 200 caracteres.");
            if (!Validacoes.CnpjValido(dto.FornecedorCnpj)) e.Add("Informe um CNPJ válido para o fornecedor.");
            if (dto.FornecedorEndereco == null) e.Add("Informe o endereço do fornecedor.");
            else if (dto.FornecedorEndereco.Length > 400) e.Add("O endereço deve ter até 400 caracteres.");
            if (dto.EscopoFornecimento == null) e.Add("Informe o escopo de fornecimento.");
            else if (dto.EscopoFornecimento.Length > 2000) e.Add("O escopo de fornecimento deve ter até 2000 caracteres.");
        }
        if (dto.LiderId == null) e.Add("Selecione o auditor líder.");
        if (dto.AuditorIds.Overlaps(dto.ObservadorIds)) e.Add("Uma mesma pessoa não pode ser auditor e observador na mesma auditoria.");
        if (e.Count > 0) throw new RegraNegocioException(e);
    }

    private async Task<int> SalvarInternoAsync(Ator ator, ProgramacaoEdicao dto)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var agora = _relogio.UtcAgora;
        var erros = new List<string>();

        Auditoria? a = null;
        if (dto.Id is int id)
        {
            a = await CarregarCompletaAsync(db, id, rastrear: true) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
            if (dto.Versao != a.Versao) throw new ConcorrenciaException();
            if (a.Tipo != dto.Tipo || a.Ano != dto.Ano)
                throw new RegraNegocioException("O tipo e o ano não podem ser alterados depois da programação, pois definem o código da auditoria. " +
                                                "Se necessário, exclua esta programação e programe uma nova.");
        }

        // ---- Catálogo
        var normas = await db.Normas.AsNoTracking().Where(n => dto.NormaIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id);
        var areas = await db.Areas.AsNoTracking().Where(n => dto.AreaIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id);
        var processos = await db.Processos.AsNoTracking().Include(p => p.Macroprocesso).Include(p => p.Normas)
            .Include(p => p.RequisitosObrigatorios).ThenInclude(r => r.Requisito)
            .Where(p => dto.ProcessoIds.Contains(p.Id)).AsSplitQuery().ToDictionaryAsync(p => p.Id);
        foreach (var p in processos.Values) dto.MacroprocessoIds.Add(p.MacroprocessoId);
        var macros = await db.Macroprocessos.AsNoTracking().Where(n => dto.MacroprocessoIds.Contains(n.Id)).ToDictionaryAsync(n => n.Id);
        var participantesIds = dto.AuditorIds.Concat(dto.ObservadorIds).Append(dto.LiderId!.Value).ToHashSet();
        var auditores = await db.Auditores.AsNoTracking().Include(x => x.Habilitacoes).Where(x => participantesIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var reqIds = dto.Itens.Select(i => i.RequisitoId).ToHashSet();
        var requisitos = await db.Requisitos.AsNoTracking().Where(r => reqIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);

        bool JaUsado<T>(IEnumerable<T>? atuais, Func<T, int> chave, int idItem) => atuais?.Any(x => chave(x) == idItem) == true;

        if (normas.Count != dto.NormaIds.Count) erros.Add("Norma inválida.");
        foreach (var n in normas.Values.Where(n => !n.Ativo && !JaUsado(a?.Normas, x => x.NormaId, n.Id)))
            erros.Add($"A norma {n.Nome} está inativa.");
        if (areas.Count != dto.AreaIds.Count) erros.Add("Área inválida.");
        foreach (var n in areas.Values.Where(n => !n.Ativo && !JaUsado(a?.Areas, x => x.AreaId, n.Id)))
            erros.Add($"A área {n.Nome} está inativa.");
        if (processos.Count != dto.ProcessoIds.Count) erros.Add("Processo inválido.");
        foreach (var n in processos.Values.Where(n => !n.Ativo && !JaUsado(a?.Processos, x => x.ProcessoId, n.Id)))
            erros.Add($"O processo {n.Nome} está inativo.");
        if (macros.Count != dto.MacroprocessoIds.Count) erros.Add("Macroprocesso inválido.");
        foreach (var n in macros.Values.Where(n => !n.Ativo && !JaUsado(a?.Macroprocessos, x => x.MacroprocessoId, n.Id)))
            erros.Add($"O macroprocesso {n.Nome} está inativo.");
        if (requisitos.Count != reqIds.Count) erros.Add("Requisito inválido.");
        if (auditores.Count != participantesIds.Count) erros.Add("Auditor inválido.");
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        // ---- Requisitos por processo
        // Cada processo selecionado precisa ter os requisitos que serão auditados nele; os itens gerais (sem processo) são opcionais.
        bool estruturaMesma = a != null &&
                              a.Normas.Select(x => x.NormaId).ToHashSet().SetEquals(dto.NormaIds) &&
                              a.Processos.Select(x => x.ProcessoId).ToHashSet().SetEquals(dto.ProcessoIds);
        var itensAtuais = a?.Requisitos.Select(r => new ItemRequisito(r.ProcessoId, r.RequisitoId)).ToHashSet() ?? new HashSet<ItemRequisito>();
        foreach (var item in dto.Itens)
        {
            if (!requisitos.TryGetValue(item.RequisitoId, out var r)) continue;
            if (!dto.NormaIds.Contains(r.NormaId)) { erros.Add($"O requisito {r.Codigo} não pertence às normas selecionadas."); continue; }
            if (!r.Ativo && !itensAtuais.Contains(item)) erros.Add($"O requisito {r.Codigo} está inativo.");
            if (item.ProcessoId is int pid)
            {
                if (!processos.TryGetValue(pid, out var proc)) erros.Add($"O requisito {r.Codigo} está associado a um processo que não foi selecionado.");
                else if (proc.Normas.All(n => n.NormaId != r.NormaId))
                    erros.Add($"O processo {proc.Nome} não está cadastrado como auditável na norma do requisito {r.Codigo}.");
            }
        }
        foreach (var proc in processos.Values.Where(p => dto.Itens.All(i => i.ProcessoId != p.Id)))
            erros.Add($"Selecione os requisitos que serão auditados no processo {proc.Nome}.");

        // ---- Equipe e habilitações
        var listaNormas = normas.Values.Select(n => new NormaInfo(n.Id, n.Nome)).ToList();
        void Checar(int auditorId, FuncaoAuditoria funcao)
        {
            var info = ParaInfo(auditores[auditorId]);
            var c = RegrasHabilitacao.Verificar(info, funcao, listaNormas);
            if (!c.Compativel) erros.Add($"{info.Nome} não pode ser designado como {funcao.Texto().ToLowerInvariant()}: {c.Motivo}");
        }
        Checar(dto.LiderId.Value, FuncaoAuditoria.Lider);
        foreach (var x in dto.AuditorIds) Checar(x, FuncaoAuditoria.Auditor);
        foreach (var x in dto.ObservadorIds) Checar(x, FuncaoAuditoria.Observador);
        var semCobertura = RegrasHabilitacao.NormasSemCobertura(listaNormas,
            dto.AuditorIds.Append(dto.LiderId.Value).Select(x => ParaInfo(auditores[x])));
        foreach (var n in semCobertura)
            erros.Add($"A norma {n.Nome} não está coberta: nenhum membro da equipe (líder ou auditores) tem habilitação de auditor nela.");

        // ---- Bloqueios por documentos existentes
        if (a != null && a.Relatorio != null)
        {
            var bloqueio = MotivoBloqueioEstrutura(a);
            var mudou = !estruturaMesma
                        || !a.Areas.Select(x => x.AreaId).ToHashSet().SetEquals(dto.AreaIds)
                        || !a.Macroprocessos.Select(x => x.MacroprocessoId).ToHashSet().SetEquals(dto.MacroprocessoIds)
                        || !itensAtuais.SetEquals(dto.Itens)
                        || a.Lider?.AuditorId != dto.LiderId
                        || !a.Participantes.Where(p => p.Funcao == FuncaoAuditoria.Auditor).Select(p => p.AuditorId).ToHashSet().SetEquals(dto.AuditorIds)
                        || !a.Participantes.Where(p => p.Funcao == FuncaoAuditoria.Observador).Select(p => p.AuditorId).ToHashSet().SetEquals(dto.ObservadorIds)
                        || a.FornecedorNome != dto.FornecedorNome || a.FornecedorCnpj != dto.FornecedorCnpj
                        || a.FornecedorEndereco != dto.FornecedorEndereco || a.EscopoFornecimento != dto.EscopoFornecimento;
            if (bloqueio != null && mudou) erros.Insert(0, bloqueio);
            // Em revisão: requisitos usados em constatações não podem sair.
            var usados = a.Relatorio.Constatacoes.SelectMany(c => c.Requisitos).Select(cr => cr.AuditoriaRequisitoId).ToHashSet();
            foreach (var ar in a.Requisitos.Where(r => usados.Contains(r.Id) && !dto.Itens.Contains(new ItemRequisito(r.ProcessoId, r.RequisitoId))))
                erros.Add($"O requisito {ar.Codigo} ({ar.ProcessoExibicao}) está classificado em constatações do relatório e não pode ser retirado.");
        }
        if (erros.Count > 0) throw new RegraNegocioException(erros.Distinct());

        // ---- Aplicação
        bool novo = a == null;
        object? antes = novo ? null : Retrato(a!);
        if (novo)
        {
            a = new Auditoria { Tipo = dto.Tipo, Ano = dto.Ano };
            Historico.MarcarCriacao(a, ator, agora);
            if (db.Database.IsSqlServer())
            {
                // Serializa a geração do código por tipo/ano dentro da transação (liberado no commit/rollback).
                await db.Database.ExecuteSqlRawAsync(
                    "DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; " +
                    "IF @r < 0 THROW 50001, 'Não foi possível reservar o código da auditoria.', 1;",
                    $"NuClaud.CodigoAuditoria.{dto.Tipo}.{dto.Ano}");
            }
            var seqs = await db.Auditorias.Where(x => x.Tipo == dto.Tipo && x.Ano == dto.Ano).Select(x => x.Sequencia).ToListAsync();
            a.Sequencia = CodigoAuditoria.ProximaSequencia(seqs);
            a.Codigo = CodigoAuditoria.Formatar(dto.Tipo, a.Sequencia, dto.Ano);
            db.Auditorias.Add(a);
        }
        else
        {
            Historico.MarcarAlteracao(db, a!, ator, agora, dto.Versao);
        }
        var aud = a!;
        bool refrescarRetratos = aud.Relatorio == null; // após início do relatório, retratos não são reescritos
        aud.MesPrevisto = dto.MesPrevisto;
        aud.FornecedorNome = dto.FornecedorNome;
        aud.FornecedorCnpj = dto.FornecedorCnpj;
        aud.FornecedorEndereco = dto.FornecedorEndereco;
        aud.EscopoFornecimento = dto.EscopoFornecimento;

        Sincronizar(aud.Normas, dto.NormaIds, x => x.NormaId, idn => new AuditoriaNorma { NormaId = idn }, refrescarRetratos, x =>
        {
            var n = normas[x.NormaId];
            x.NomeNorma = n.Nome; x.EdicaoNorma = n.Edicao; x.GrupoCor = n.GrupoCor;
        });
        Sincronizar(aud.Areas, dto.AreaIds, x => x.AreaId, idn => new AuditoriaArea { AreaId = idn }, refrescarRetratos, x => x.NomeArea = areas[x.AreaId].Nome);
        Sincronizar(aud.Macroprocessos, dto.MacroprocessoIds, x => x.MacroprocessoId, idn => new AuditoriaMacroprocesso { MacroprocessoId = idn }, refrescarRetratos,
            x => x.NomeMacroprocesso = macros[x.MacroprocessoId].Nome);
        Sincronizar(aud.Processos, dto.ProcessoIds, x => x.ProcessoId, idn => new AuditoriaProcesso { ProcessoId = idn }, refrescarRetratos, x =>
        {
            var p = processos[x.ProcessoId];
            x.NomeProcesso = p.Nome; x.NomeMacroprocesso = p.Macroprocesso!.Nome;
        });
        // Itens (processo, requisito): mantém as linhas existentes (constatações apontam para elas), inclui as novas e retira as desmarcadas.
        aud.Requisitos.RemoveAll(x => !dto.Itens.Contains(new ItemRequisito(x.ProcessoId, x.RequisitoId)));
        foreach (var item in dto.Itens)
        {
            var linha = aud.Requisitos.FirstOrDefault(x => x.RequisitoId == item.RequisitoId && x.ProcessoId == item.ProcessoId);
            if (linha == null) aud.Requisitos.Add(linha = new AuditoriaRequisito { RequisitoId = item.RequisitoId, ProcessoId = item.ProcessoId });
            else if (!refrescarRetratos) continue;
            var r = requisitos[item.RequisitoId];
            linha.NormaId = r.NormaId; linha.NomeNorma = normas[r.NormaId].Nome; linha.Codigo = r.Codigo; linha.Titulo = r.Titulo; linha.ChaveOrdenacao = r.ChaveOrdenacao;
            linha.NomeProcesso = item.ProcessoId is int pid ? processos[pid].Nome : null;
        }

        var desejados = new List<(int AuditorId, FuncaoAuditoria Funcao)> { (dto.LiderId.Value, FuncaoAuditoria.Lider) };
        desejados.AddRange(dto.AuditorIds.Select(x => (x, FuncaoAuditoria.Auditor)));
        desejados.AddRange(dto.ObservadorIds.Select(x => (x, FuncaoAuditoria.Observador)));
        aud.Participantes.RemoveAll(p => !desejados.Contains((p.AuditorId, p.Funcao)));
        foreach (var (auditorId, funcao) in desejados)
        {
            var p = aud.Participantes.FirstOrDefault(x => x.AuditorId == auditorId && x.Funcao == funcao);
            if (p == null) aud.Participantes.Add(p = new AuditoriaParticipante { AuditorId = auditorId, Funcao = funcao });
            else if (!refrescarRetratos) continue;
            p.NomeAuditor = auditores[auditorId].NomeCompleto;
            p.EmailAuditor = auditores[auditorId].Email;
        }

        await db.SaveChangesAsync();
        Historico.Registrar(db, ator, agora, "Auditoria", aud.Id, novo ? "Programação" : "Alteração da programação",
            novo ? $"Auditoria {aud.Codigo} programada para {Rotulos.NomeMes(aud.MesPrevisto)}/{aud.Ano}."
                 : $"Programação da auditoria {aud.Codigo} alterada.",
            new { antes, depois = Retrato(aud) });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { throw new ConcorrenciaException(); }
        await tx.CommitAsync();
        return aud.Id;
    }

    private static void Sincronizar<T>(List<T> linhas, ICollection<int> ids, Func<T, int> chave, Func<int, T> criar, bool refrescar, Action<T> retrato)
    {
        linhas.RemoveAll(l => !ids.Contains(chave(l)));
        foreach (var l in linhas.ToList()) if (refrescar) retrato(l);
        foreach (var id in ids.Where(i => linhas.All(l => chave(l) != i)))
        {
            var nova = criar(id);
            retrato(nova);
            linhas.Add(nova);
        }
    }

    private static object Retrato(Auditoria a) => new
    {
        a.Codigo, a.Tipo, a.Ano, a.MesPrevisto,
        Normas = a.Normas.Select(n => n.NomeNorma).OrderBy(x => x).ToList(),
        Areas = a.Areas.Select(n => n.NomeArea).OrderBy(x => x).ToList(),
        Processos = a.Processos.Select(n => n.NomeProcesso).OrderBy(x => x).ToList(),
        Requisitos = a.Requisitos.OrderBy(r => r.NomeNorma).ThenBy(r => r.ChaveOrdenacao).Select(r => $"{r.NomeNorma} {r.Codigo} · {r.ProcessoExibicao}").ToList(),
        Equipe = a.Participantes.OrderBy(p => p.Funcao).Select(p => $"{p.Funcao.Texto()}: {p.NomeAuditor}").ToList(),
        a.FornecedorNome, a.FornecedorCnpj, a.FornecedorEndereco, a.EscopoFornecimento
    };

    // ------------------------------------------------------------------ Excluir

    public async Task ExcluirAsync(Ator ator, int id)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException("Somente administradores podem excluir programações.");
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await CarregarCompletaAsync(db, id, rastrear: true) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        var retrato = new
        {
            programacao = Retrato(a),
            plano = a.Plano == null ? null : new { a.Plano.Local, a.Plano.GestorNome, a.Plano.GestorEmail, Periodos = a.Plano.Periodos.Select(p => Rotulos.Periodo(p.Data, p.HoraInicio, p.HoraFim)) },
            relatorio = a.Relatorio == null ? null : new { a.Relatorio.Situacao, Constatacoes = a.Relatorio.Constatacoes.Count, a.Relatorio.ConcluidoPor, a.Relatorio.ConcluidoEmUtc }
        };
        if (a.Relatorio != null)
        {
            db.Relatorios.Remove(a.Relatorio);
            await db.SaveChangesAsync();
        }
        db.Auditorias.Remove(a);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Auditoria", a.Id, "Exclusão",
            $"Programação {a.Codigo} excluída{(a.Plano != null ? " (com plano)" : "")}{(a.Relatorio != null ? " (com relatório)" : "")}.", retrato);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
