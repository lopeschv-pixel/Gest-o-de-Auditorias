using System.Text.RegularExpressions;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

/// <summary>Ordenação numérica por segmentos e validação de níveis dos códigos de requisitos.</summary>
public static partial class OrdenacaoRequisitos
{
    /// <summary>Aceita apenas dois ou três segmentos numéricos (ex.: 4.1, 4.4.1). Não aceita "4" nem "4.4.1.1".</summary>
    [GeneratedRegex(@"^\d{1,4}(\.\d{1,4}){1,2}$")]
    private static partial Regex FormatoValido();

    public static bool CodigoValido(string? codigo) => codigo != null && FormatoValido().IsMatch(codigo.Trim());

    public static string Chave(string codigo)
    {
        var partes = codigo.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('.', partes.Select(p => int.TryParse(p, out var n) ? n.ToString("D4") : p.PadLeft(4, '0')));
    }

    public static IComparer<string> Comparador { get; } = Comparer<string>.Create((a, b) =>
        string.CompareOrdinal(Chave(a), Chave(b)));

    public static IEnumerable<T> Ordenar<T>(IEnumerable<T> itens, Func<T, string> codigo) =>
        itens.OrderBy(i => Chave(codigo(i)), StringComparer.Ordinal);

    public static string Exibicao(string codigo, string titulo) => $"{codigo} — {titulo}";
}

public static class CodigoAuditoria
{
    public static string Prefixo(TipoAuditoria tipo) => tipo == TipoAuditoria.Interna ? "AUDI-I" : "AUDI-F";

    /// <summary>Ex.: AUDI-I-001-2027. Pelo menos três dígitos na sequência.</summary>
    public static string Formatar(TipoAuditoria tipo, int sequencia, int ano) =>
        $"{Prefixo(tipo)}-{sequencia:D3}-{ano}";

    /// <summary>Próxima sequência = maior sequência existente do tipo/ano + 1; 1 se não houver nenhuma.</summary>
    public static int ProximaSequencia(IEnumerable<int> sequenciasExistentes)
    {
        var max = 0;
        foreach (var s in sequenciasExistentes) if (s > max) max = s;
        return max + 1;
    }
}

public static partial class Validacoes
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    public static bool EmailValido(string? email) => !string.IsNullOrWhiteSpace(email) && email.Length <= 254 && EmailRegex().IsMatch(email.Trim());

    public static string SomenteDigitos(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    public static bool CnpjValido(string? cnpj)
    {
        var d = SomenteDigitos(cnpj);
        if (d.Length != 14 || d.Distinct().Count() == 1) return false;
        int[] m1 = { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int[] m2 = { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 };
        int soma = 0;
        for (int i = 0; i < 12; i++) soma += (d[i] - '0') * m1[i];
        int r = soma % 11; int dv1 = r < 2 ? 0 : 11 - r;
        soma = 0;
        for (int i = 0; i < 13; i++) soma += (d[i] - '0') * m2[i];
        r = soma % 11; int dv2 = r < 2 ? 0 : 11 - r;
        return d[12] - '0' == dv1 && d[13] - '0' == dv2;
    }

    public static string FormatarCnpj(string? cnpj)
    {
        var d = SomenteDigitos(cnpj);
        return d.Length == 14 ? $"{d[..2]}.{d[2..5]}.{d[5..8]}/{d[8..12]}-{d[12..]}" : (cnpj ?? "");
    }

    public static string? Limpar(string? s)
    {
        if (s == null) return null;
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }
}

// ------------------------------------------------------------------
// Habilitações (regra documentada em docs/REGRAS.md)
// ------------------------------------------------------------------

public sealed record HabilitacaoInfo(int NormaId, bool PodeLiderar, bool PodeAuditar, bool PodeObservar);
public sealed record AuditorInfo(int Id, string Nome, bool Ativo, IReadOnlyList<HabilitacaoInfo> Habilitacoes);
public sealed record NormaInfo(int Id, string Nome);

/// <summary>Resultado da verificação de um candidato para uma função.</summary>
public sealed record Compatibilidade(bool Compativel, string? Motivo);

public static class RegrasHabilitacao
{
    /// <summary>
    /// Regra adotada para auditorias com várias normas:
    /// • Auditor líder: precisa ter a habilitação "Auditor líder" em TODAS as normas selecionadas.
    /// • Auditores: precisam ter a habilitação "Auditor" (ou "Auditor líder") em PELO MENOS UMA norma selecionada.
    /// • Observadores: precisam ter a habilitação "Observador" em pelo menos uma norma selecionada.
    /// • Cobertura: cada norma selecionada precisa ser coberta por ao menos um membro da equipe
    ///   (líder ou auditores) habilitado como Auditor ou Auditor líder naquela norma.
    /// </summary>
    public static Compatibilidade Verificar(AuditorInfo a, FuncaoAuditoria funcao, IReadOnlyList<NormaInfo> normas)
    {
        if (!a.Ativo) return new(false, "Auditor inativo.");
        if (normas.Count == 0) return new(false, "Selecione as normas da auditoria primeiro.");
        var hab = a.Habilitacoes.ToDictionary(h => h.NormaId);
        switch (funcao)
        {
            case FuncaoAuditoria.Lider:
                var faltam = normas.Where(n => !(hab.TryGetValue(n.Id, out var h) && h.PodeLiderar)).Select(n => n.Nome).ToList();
                return faltam.Count == 0 ? new(true, null)
                    : new(false, "Sem habilitação de auditor líder em: " + string.Join(", ", faltam) + ".");
            case FuncaoAuditoria.Auditor:
                return normas.Any(n => hab.TryGetValue(n.Id, out var h) && (h.PodeAuditar || h.PodeLiderar))
                    ? new(true, null)
                    : new(false, "Sem habilitação de auditor em nenhuma das normas selecionadas.");
            default:
                return normas.Any(n => hab.TryGetValue(n.Id, out var h) && h.PodeObservar)
                    ? new(true, null)
                    : new(false, "Sem habilitação de observador em nenhuma das normas selecionadas.");
        }
    }

    /// <summary>Normas que não estão cobertas por nenhum membro da equipe (líder + auditores).</summary>
    public static IReadOnlyList<NormaInfo> NormasSemCobertura(IReadOnlyList<NormaInfo> normas, IEnumerable<AuditorInfo> equipe)
    {
        var lista = equipe.ToList();
        return normas.Where(n => !lista.Any(a => a.Habilitacoes.Any(h => h.NormaId == n.Id && (h.PodeAuditar || h.PodeLiderar)))).ToList();
    }
}

// ------------------------------------------------------------------
// Requisitos automáticos da programação
// ------------------------------------------------------------------

public sealed record RequisitoProcessoInfo(int RequisitoId, int NormaId, bool Ativo);
public sealed record ProcessoInfo(int Id, string Nome, int MacroprocessoId, IReadOnlyList<int> NormaIds, IReadOnlyList<RequisitoProcessoInfo> RequisitosObrigatorios);

public static class RegrasRequisitos
{
    /// <summary>
    /// Une os requisitos obrigatórios dos processos selecionados, sem duplicação,
    /// considerando apenas as normas selecionadas na auditoria e requisitos ativos.
    /// Retorna requisitoId → nomes dos processos de origem.
    /// </summary>
    public static Dictionary<int, List<string>> Obrigatorios(IEnumerable<ProcessoInfo> processos, ICollection<int> normasSelecionadas)
    {
        var r = new Dictionary<int, List<string>>();
        foreach (var p in processos)
            foreach (var req in p.RequisitosObrigatorios)
            {
                if (!req.Ativo || !normasSelecionadas.Contains(req.NormaId)) continue;
                if (!r.TryGetValue(req.RequisitoId, out var origem)) r[req.RequisitoId] = origem = new();
                if (!origem.Contains(p.Nome)) origem.Add(p.Nome);
            }
        return r;
    }
}

// ------------------------------------------------------------------
// Cobertura do relatório
// ------------------------------------------------------------------

public sealed record ConstatacaoCobertura(ClassificacaoConstatacao Classificacao, IReadOnlyCollection<int> Requisitos);

public sealed record ResultadoCobertura(int Total, IReadOnlyList<int> Classificados, IReadOnlyList<int> Pendentes)
{
    public bool Completa => Total > 0 && Pendentes.Count == 0;
    public string Resumo => $"{Classificados.Count} de {Total} requisitos classificados";
}

public static class RegrasRelatorio
{
    /// <summary>
    /// Todos os requisitos auditados do plano precisam estar em pelo menos uma constatação de
    /// Conformidade ou Não conformidade. Observação e Oportunidade de melhoria não contam.
    /// </summary>
    public static ResultadoCobertura Cobertura(IReadOnlyList<int> requisitosPlano, IEnumerable<ConstatacaoCobertura> constatacoes)
    {
        var cobertos = constatacoes
            .Where(c => c.Classificacao is ClassificacaoConstatacao.Conformidade or ClassificacaoConstatacao.NaoConformidade)
            .SelectMany(c => c.Requisitos)
            .ToHashSet();
        var classificados = requisitosPlano.Where(cobertos.Contains).ToList();
        var pendentes = requisitosPlano.Where(r => !cobertos.Contains(r)).ToList();
        return new(requisitosPlano.Count, classificados, pendentes);
    }

    public static bool UsaRequisitos(ClassificacaoConstatacao c) =>
        c is ClassificacaoConstatacao.Conformidade or ClassificacaoConstatacao.NaoConformidade;
}

// ------------------------------------------------------------------
// Pontuação dos auditores
// ------------------------------------------------------------------

public sealed record ParticipacaoInfo(int AuditoriaId, int AuditorId, FuncaoAuditoria Funcao);

public sealed record LinhaRanking(int AuditorId, string Nome, int ComoLider, int ComoAuditor, int ComoObservador, int Pontos, int AuditoriasDistintas);

public static class RegrasPontuacao
{
    public const int PontosLider = 2;
    public const int PontosAuditor = 1;
    public const int PontosObservador = 0;

    /// <summary>
    /// Líder = 2, Auditor = 1, Observador = 0 por designação. Cada auditoria conta uma única vez por pessoa
    /// (se, por inconsistência, houver duas funções na mesma auditoria, prevalece a de maior pontuação).
    /// </summary>
    public static List<LinhaRanking> Calcular(IEnumerable<ParticipacaoInfo> participacoes, IReadOnlyDictionary<int, string> nomes)
    {
        var porPessoa = participacoes
            .GroupBy(p => (p.AuditorId, p.AuditoriaId))
            .Select(g => g.OrderBy(p => (int)p.Funcao).First()) // Lider(1) < Auditor(2) < Observador(3)
            .GroupBy(p => p.AuditorId);

        var linhas = new List<LinhaRanking>();
        foreach (var g in porPessoa)
        {
            int l = g.Count(p => p.Funcao == FuncaoAuditoria.Lider);
            int a = g.Count(p => p.Funcao == FuncaoAuditoria.Auditor);
            int o = g.Count(p => p.Funcao == FuncaoAuditoria.Observador);
            linhas.Add(new LinhaRanking(g.Key, nomes.TryGetValue(g.Key, out var n) ? n : $"Auditor {g.Key}",
                l, a, o, l * PontosLider + a * PontosAuditor + o * PontosObservador, g.Select(p => p.AuditoriaId).Distinct().Count()));
        }
        return linhas;
    }
}

// ------------------------------------------------------------------
// Permissões (sempre verificadas no servidor)
// ------------------------------------------------------------------

public static class Permissoes
{
    public static bool Participa(Ator ator, Auditoria a) =>
        ator.AuditorId is int id && a.Participantes.Any(p => p.AuditorId == id);

    public static bool EhLider(Ator ator, Auditoria a) =>
        ator.AuditorId is int id && a.Participantes.Any(p => p.AuditorId == id && p.Funcao == FuncaoAuditoria.Lider);

    public static bool PodeVisualizar(Ator ator, Auditoria a) => ator.EhAdministrador || Participa(ator, a);

    public static bool PodeAdministrarProgramacao(Ator ator) => ator.EhAdministrador;

    /// <summary>Gerar/salvar plano: administrador ou auditor líder designado, enquanto o relatório não estiver concluído.</summary>
    public static bool PodeEditarPlano(Ator ator, Auditoria a) =>
        (ator.EhAdministrador || EhLider(ator, a)) && a.Relatorio?.Situacao != SituacaoRelatorio.Concluido;

    /// <summary>Somente o administrador edita local, gestor, objetivo e escopo. O líder edita apenas datas e horários.</summary>
    public static bool PodeEditarCamposGeraisPlano(Ator ator, Auditoria a) => ator.EhAdministrador && PodeEditarPlano(ator, a);

    /// <summary>
    /// Relatório: líder designado ou administrador antes da conclusão.
    /// Após conclusão, ninguém edita sem reabertura. Em revisão (reaberto pelo administrador), apenas o administrador.
    /// </summary>
    public static bool PodeEditarRelatorio(Ator ator, Auditoria a)
    {
        var s = a.Relatorio?.Situacao ?? SituacaoRelatorio.EmElaboracao;
        return s switch
        {
            SituacaoRelatorio.EmElaboracao => ator.EhAdministrador || EhLider(ator, a),
            SituacaoRelatorio.EmRevisao => ator.EhAdministrador,
            _ => false
        };
    }

    public static bool PodeReabrirRelatorio(Ator ator, Auditoria a) =>
        ator.EhAdministrador && a.Relatorio?.Situacao == SituacaoRelatorio.Concluido;
}
