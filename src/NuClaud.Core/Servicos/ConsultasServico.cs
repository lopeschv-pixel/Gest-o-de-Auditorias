using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public enum CorCartao { Cinza, Amarelo, Laranja, Verde, Azul, Neutro }

public sealed class FiltroCalendario
{
    public int Ano { get; set; }
    /// <summary>Vazio = Todas.</summary>
    public HashSet<int> NormaIds { get; set; } = new();
    public string? Busca { get; set; }
}

public static class RegrasCalendario
{
    /// <summary>Prioridade: Concluída (cinza) &gt; ASME III (amarelo) &gt; ANSN/Secretaria Naval (laranja) &gt; ISO 14001/45001 (verde) &gt; ISO 9001 (azul).</summary>
    public static CorCartao Cor(SituacaoAuditoria situacao, IEnumerable<GrupoCorNorma> grupos)
    {
        if (situacao == SituacaoAuditoria.Realizada) return CorCartao.Cinza;
        var g = grupos.ToHashSet();
        if (g.Contains(GrupoCorNorma.Asme)) return CorCartao.Amarelo;
        if (g.Contains(GrupoCorNorma.NuclearNaval)) return CorCartao.Laranja;
        if (g.Contains(GrupoCorNorma.AmbienteSso)) return CorCartao.Verde;
        if (g.Contains(GrupoCorNorma.Qualidade)) return CorCartao.Azul;
        return CorCartao.Neutro;
    }

    public static string SemAcento(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var n = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(n.Length);
        foreach (var c in n)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }

    /// <summary>Filtro único usado pelo calendário e pelo PDF do programa.</summary>
    public static List<Auditoria> Aplicar(IEnumerable<Auditoria> auditorias, Ator ator, FiltroCalendario f)
    {
        var termo = SemAcento(f.Busca?.Trim());
        return auditorias
            .Where(a => a.Ano == f.Ano)
            .Where(a => Permissoes.PodeVisualizar(ator, a))
            .Where(a => f.NormaIds.Count == 0 || a.Normas.Any(n => f.NormaIds.Contains(n.NormaId)))
            .Where(a => termo.Length == 0
                        || SemAcento(a.Codigo).Contains(termo)
                        || SemAcento(a.FornecedorNome).Contains(termo)
                        || a.Areas.Any(x => SemAcento(x.NomeArea).Contains(termo))
                        || a.Participantes.Any(x => SemAcento(x.NomeAuditor).Contains(termo)))
            .DistinctBy(a => a.Id)
            .OrderBy(a => a.MesPrevisto).ThenBy(a => a.Tipo).ThenBy(a => a.Sequencia)
            .ToList();
    }
}

public sealed record Designacao(int AuditoriaId, string Codigo, int Mes, IReadOnlyList<PlanoPeriodo> Periodos, FuncaoAuditoria Funcao,
    IReadOnlyList<string> Normas, string Identificacao, SituacaoAuditoria Situacao, TipoAuditoria Tipo);

public sealed record ConflitoAgenda(string CodigoA, string CodigoB, DateOnly Data, string Descricao);

public sealed record PlanejamentoAuditor(int AuditorId, string Nome, bool Ativo, IReadOnlyList<Designacao> Designacoes,
    IReadOnlyList<ConflitoAgenda> Conflitos, IReadOnlyDictionary<int, int> PorMes)
{
    public int Pontos => Designacoes.Sum(d => d.Funcao switch { FuncaoAuditoria.Lider => 2, FuncaoAuditoria.Auditor => 1, _ => 0 });
    public int MaiorConcentracaoMensal => PorMes.Count == 0 ? 0 : PorMes.Values.Max();
}

public class ConsultasServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;

    public ConsultasServico(IDbContextFactory<NuClaudDbContext> fabrica) => _fabrica = fabrica;

    private static IQueryable<Auditoria> Completa(NuClaudDbContext db) => db.Auditorias.AsNoTracking()
        .Include(a => a.Areas).Include(a => a.Normas).Include(a => a.Macroprocessos).Include(a => a.Processos)
        .Include(a => a.Participantes).Include(a => a.Requisitos)
        .Include(a => a.Plano!).ThenInclude(p => p.Periodos)
        .Include(a => a.Relatorio)
        .AsSplitQuery();

    public async Task<List<int>> AnosDisponiveisAsync()
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Auditorias.Select(a => a.Ano).Distinct().OrderBy(a => a).ToListAsync();
    }

    public async Task<List<Auditoria>> CalendarioAsync(Ator ator, FiltroCalendario filtro)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        var q = Completa(db).Where(a => a.Ano == filtro.Ano);
        if (!ator.EhAdministrador)
        {
            var aid = ator.AuditorId ?? -1;
            q = q.Where(a => a.Participantes.Any(p => p.AuditorId == aid));
        }
        var lista = await q.ToListAsync();
        foreach (var a in lista) ProgramacaoServico.Ordenar(a);
        return RegrasCalendario.Aplicar(lista, ator, filtro);
    }

    public async Task<List<LinhaRanking>> RankingAsync(Ator ator, int ano)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var parts = await db.AuditoriaParticipantes.AsNoTracking().Where(p => p.Auditoria!.Ano == ano)
            .Select(p => new ParticipacaoInfo(p.AuditoriaId, p.AuditorId, p.Funcao)).ToListAsync();
        var auditores = await db.Auditores.AsNoTracking().Select(a => new { a.Id, a.NomeCompleto, a.Ativo }).ToListAsync();
        var nomes = auditores.ToDictionary(a => a.Id, a => a.NomeCompleto);
        var linhas = RegrasPontuacao.Calcular(parts, nomes);
        // Auditores ativos sem designações também aparecem (zero pontos), para apoiar o equilíbrio.
        foreach (var a in auditores.Where(a => a.Ativo && linhas.All(l => l.AuditorId != a.Id)))
            linhas.Add(new LinhaRanking(a.Id, a.NomeCompleto, 0, 0, 0, 0, 0));
        return linhas;
    }

    public async Task<List<PlanejamentoAuditor>> PlanejamentoAsync(Ator ator, int ano, int? auditorId, int? normaId)
    {
        if (!ator.EhAdministrador)
        {
            if (ator.AuditorId == null) return new();
            auditorId = ator.AuditorId; // usuário vê apenas a própria agenda
        }
        await using var db = await _fabrica.CreateDbContextAsync();
        var q = Completa(db).Where(a => a.Ano == ano);
        if (normaId is int nid) q = q.Where(a => a.Normas.Any(n => n.NormaId == nid));
        var auditorias = await q.ToListAsync();
        var auditores = await db.Auditores.AsNoTracking().Where(a => auditorId == null || a.Id == auditorId).OrderBy(a => a.NomeCompleto).ToListAsync();

        var resultado = new List<PlanejamentoAuditor>();
        foreach (var aud in auditores)
        {
            var designacoes = auditorias
                .SelectMany(a => a.Participantes.Where(p => p.AuditorId == aud.Id).Select(p => (a, p)))
                .Select(x => new Designacao(x.a.Id, x.a.Codigo, x.a.MesPrevisto,
                    x.a.Plano?.Periodos.OrderBy(p => p.Data).ThenBy(p => p.HoraInicio).ToList() ?? new List<PlanoPeriodo>(),
                    x.p.Funcao, x.a.Normas.Select(n => n.NomeNorma).OrderBy(n => n).ToList(),
                    ProgramacaoServico.Identificacao(x.a), x.a.Situacao, x.a.Tipo))
                .OrderBy(d => d.Mes).ThenBy(d => d.Periodos.FirstOrDefault()?.Data).ThenBy(d => d.Codigo)
                .ToList();
            if (designacoes.Count == 0 && (!aud.Ativo || normaId != null)) continue;
            resultado.Add(new PlanejamentoAuditor(aud.Id, aud.NomeCompleto, aud.Ativo, designacoes,
                DetectarConflitos(designacoes),
                designacoes.Where(d => d.Funcao != FuncaoAuditoria.Observador).GroupBy(d => d.Mes).ToDictionary(g => g.Key, g => g.Count())));
        }
        return resultado;
    }

    /// <summary>Períodos de planos diferentes que se sobrepõem no mesmo dia para a mesma pessoa.</summary>
    public static List<ConflitoAgenda> DetectarConflitos(IReadOnlyList<Designacao> designacoes)
    {
        var conflitos = new List<ConflitoAgenda>();
        for (int i = 0; i < designacoes.Count; i++)
            for (int j = i + 1; j < designacoes.Count; j++)
            {
                var a = designacoes[i];
                var b = designacoes[j];
                if (a.AuditoriaId == b.AuditoriaId) continue;
                foreach (var pa in a.Periodos)
                    foreach (var pb in b.Periodos)
                        if (pa.Data == pb.Data && pa.HoraInicio < pb.HoraFim && pb.HoraInicio < pa.HoraFim)
                            conflitos.Add(new ConflitoAgenda(a.Codigo, b.Codigo, pa.Data,
                                $"{a.Codigo} ({Rotulos.Hora(pa.HoraInicio)}–{Rotulos.Hora(pa.HoraFim)}) e {b.Codigo} ({Rotulos.Hora(pb.HoraInicio)}–{Rotulos.Hora(pb.HoraFim)}) em {Rotulos.Data(pa.Data)}"));
            }
        return conflitos;
    }

    public async Task<List<HistoricoAlteracao>> HistoricoAsync(Ator ator, string? entidade, string? entidadeId, int limite = 300)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var q = db.Historico.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(entidade)) q = q.Where(h => h.Entidade == entidade);
        if (!string.IsNullOrWhiteSpace(entidadeId)) q = q.Where(h => h.EntidadeId == entidadeId);
        return await q.OrderByDescending(h => h.DataHoraUtc).ThenByDescending(h => h.Id).Take(limite).ToListAsync();
    }

    /// <summary>Histórico de uma auditoria (programação, plano e relatório), visível a quem pode ver a auditoria.</summary>
    public async Task<List<HistoricoAlteracao>> HistoricoAuditoriaAsync(Ator ator, Auditoria a)
    {
        if (!Permissoes.PodeVisualizar(ator, a)) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var id = a.Id.ToString();
        return await db.Historico.AsNoTracking()
            .Where(h => h.EntidadeId == id && (h.Entidade == "Auditoria" || h.Entidade == "Plano" || h.Entidade == "Relatorio"))
            .OrderByDescending(h => h.DataHoraUtc).ThenByDescending(h => h.Id).Take(200).ToListAsync();
    }
}
