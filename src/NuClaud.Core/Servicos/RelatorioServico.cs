using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed class ConstatacaoEdicao
{
    public int? Id { get; set; }
    /// <summary>Chave local da interface (para constatações ainda não salvas).</summary>
    public Guid Chave { get; set; } = Guid.NewGuid();
    public ClassificacaoConstatacao Classificacao { get; set; } = ClassificacaoConstatacao.Conformidade;
    public HashSet<int> AuditoriaRequisitoIds { get; set; } = new();
    public string? Evidencia { get; set; }
    public string? DescricaoNaoConformidade { get; set; }
    public string? EvidenciasObjetivas { get; set; }
    public string? NumeroPac { get; set; }
    public string? Observacao { get; set; }
    public string? OportunidadeMelhoria { get; set; }

    public ConstatacaoEdicao Clonar() => new()
    {
        Id = Id, Chave = Chave, Classificacao = Classificacao, AuditoriaRequisitoIds = new(AuditoriaRequisitoIds),
        Evidencia = Evidencia, DescricaoNaoConformidade = DescricaoNaoConformidade, EvidenciasObjetivas = EvidenciasObjetivas,
        NumeroPac = NumeroPac, Observacao = Observacao, OportunidadeMelhoria = OportunidadeMelhoria
    };

    /// <summary>Campos preenchidos que não pertencem à classificação informada (seriam descartados ao salvar).</summary>
    public List<string> CamposQueSeraoDescartados(ClassificacaoConstatacao nova)
    {
        var l = new List<string>();
        void Ver(bool pertence, string? valor, string nome) { if (!pertence && !string.IsNullOrWhiteSpace(valor)) l.Add(nome); }
        Ver(nova == ClassificacaoConstatacao.Conformidade, Evidencia, "Evidência encontrada");
        Ver(nova == ClassificacaoConstatacao.NaoConformidade, DescricaoNaoConformidade, "Descrição da não conformidade");
        Ver(nova == ClassificacaoConstatacao.NaoConformidade, EvidenciasObjetivas, "Evidências objetivas");
        Ver(nova == ClassificacaoConstatacao.NaoConformidade, NumeroPac, "Número do PAC");
        Ver(nova == ClassificacaoConstatacao.Observacao, Observacao, "Observação");
        Ver(nova == ClassificacaoConstatacao.OportunidadeMelhoria, OportunidadeMelhoria, "Oportunidade de melhoria");
        if (!RegrasRelatorio.UsaRequisitos(nova) && AuditoriaRequisitoIds.Count > 0) l.Add("Requisitos selecionados");
        return l;
    }
}

public sealed class RelatorioEdicao
{
    public int AuditoriaId { get; set; }
    public Guid? Versao { get; set; }
    public List<ConstatacaoEdicao> Constatacoes { get; set; } = new();
    public QualificacaoFornecedor? Qualificacao { get; set; }
    public string? ObservacoesConclusao { get; set; }
    public string? NumeroRqf { get; set; }
}

public sealed record RequisitoPendente(int AuditoriaRequisitoId, string Norma, string Codigo, string Titulo)
{
    public string Exibicao => $"{Norma} — {OrdenacaoRequisitos.Exibicao(Codigo, Titulo)}";
}

public sealed record ResultadoConclusao(bool Concluido, ResultadoCobertura Cobertura, IReadOnlyList<RequisitoPendente> Pendentes, IReadOnlyList<string> Erros);

public class RelatorioServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public RelatorioServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public static RelatorioEdicao ParaEdicao(Auditoria a) => new()
    {
        AuditoriaId = a.Id,
        Versao = a.Relatorio?.Versao,
        Qualificacao = a.Relatorio?.Qualificacao,
        ObservacoesConclusao = a.Relatorio?.ObservacoesConclusao,
        NumeroRqf = a.Relatorio?.NumeroRqf,
        Constatacoes = (a.Relatorio?.Constatacoes ?? new()).OrderBy(c => c.Numero).Select(c => new ConstatacaoEdicao
        {
            Id = c.Id,
            Classificacao = c.Classificacao,
            AuditoriaRequisitoIds = c.Requisitos.Select(r => r.AuditoriaRequisitoId).ToHashSet(),
            Evidencia = c.Evidencia,
            DescricaoNaoConformidade = c.DescricaoNaoConformidade,
            EvidenciasObjetivas = c.EvidenciasObjetivas,
            NumeroPac = c.NumeroPac,
            Observacao = c.Observacao,
            OportunidadeMelhoria = c.OportunidadeMelhoria
        }).ToList()
    };

    /// <summary>Limpa os campos que não pertencem à classificação (não são exigidos nem gravados).</summary>
    public static void Normalizar(ConstatacaoEdicao c)
    {
        c.Evidencia = c.Classificacao == ClassificacaoConstatacao.Conformidade ? Validacoes.Limpar(c.Evidencia) : null;
        var nc = c.Classificacao == ClassificacaoConstatacao.NaoConformidade;
        c.DescricaoNaoConformidade = nc ? Validacoes.Limpar(c.DescricaoNaoConformidade) : null;
        c.EvidenciasObjetivas = nc ? Validacoes.Limpar(c.EvidenciasObjetivas) : null;
        c.NumeroPac = nc ? Validacoes.Limpar(c.NumeroPac) : null;
        c.Observacao = c.Classificacao == ClassificacaoConstatacao.Observacao ? Validacoes.Limpar(c.Observacao) : null;
        c.OportunidadeMelhoria = c.Classificacao == ClassificacaoConstatacao.OportunidadeMelhoria ? Validacoes.Limpar(c.OportunidadeMelhoria) : null;
        if (!RegrasRelatorio.UsaRequisitos(c.Classificacao)) c.AuditoriaRequisitoIds.Clear();
    }

    /// <summary>Valida uma constatação já normalizada.</summary>
    public static List<string> Validar(ConstatacaoEdicao c, ICollection<int> requisitosDaAuditoria, string prefixo = "")
    {
        var e = new List<string>();
        void Texto(string? v, string nome)
        {
            if (string.IsNullOrWhiteSpace(v)) e.Add($"{prefixo}preencha o campo \"{nome}\".");
            else if (v.Length > LimitesTexto.TextoLongo) e.Add($"{prefixo}o campo \"{nome}\" excede {LimitesTexto.TextoLongo} caracteres.");
        }
        if (!Enum.IsDefined(c.Classificacao)) { e.Add($"{prefixo}classificação inválida."); return e; }
        switch (c.Classificacao)
        {
            case ClassificacaoConstatacao.Conformidade:
                if (c.AuditoriaRequisitoIds.Count == 0) e.Add($"{prefixo}selecione ao menos um requisito auditado.");
                Texto(c.Evidencia, "Evidência encontrada");
                break;
            case ClassificacaoConstatacao.NaoConformidade:
                if (c.AuditoriaRequisitoIds.Count == 0) e.Add($"{prefixo}selecione ao menos um requisito auditado.");
                Texto(c.DescricaoNaoConformidade, "Descrição da não conformidade");
                Texto(c.EvidenciasObjetivas, "Evidências objetivas");
                if (c.NumeroPac?.Length > 50) e.Add($"{prefixo}o número do PAC deve ter até 50 caracteres.");
                break;
            case ClassificacaoConstatacao.Observacao:
                Texto(c.Observacao, "Observação");
                break;
            case ClassificacaoConstatacao.OportunidadeMelhoria:
                Texto(c.OportunidadeMelhoria, "Oportunidade de melhoria");
                break;
        }
        if (c.AuditoriaRequisitoIds.Any(r => !requisitosDaAuditoria.Contains(r)))
            e.Add($"{prefixo}há requisito que não pertence aos requisitos auditados desta auditoria.");
        return e;
    }

    public static ResultadoCobertura CalcularCobertura(Auditoria a, IEnumerable<ConstatacaoEdicao> constatacoes) =>
        RegrasRelatorio.Cobertura(a.Requisitos.OrderBy(r => r.NomeNorma).ThenBy(r => r.ChaveOrdenacao, StringComparer.Ordinal).Select(r => r.Id).ToList(),
            constatacoes.Select(c => new ConstatacaoCobertura(c.Classificacao, c.AuditoriaRequisitoIds)));

    // ------------------------------------------------------------------

    public async Task IniciarAsync(Ator ator, int auditoriaId)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        var a = await db.Auditorias.Include(x => x.Participantes).Include(x => x.Plano).Include(x => x.Relatorio)
            .FirstOrDefaultAsync(x => x.Id == auditoriaId) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        if (!Permissoes.PodeVisualizar(ator, a)) throw new AcessoNegadoException("Você não tem acesso a esta auditoria.");
        if (a.Relatorio != null) return;
        if (!Permissoes.PodeEditarRelatorio(ator, a))
            throw new AcessoNegadoException("Somente o auditor líder designado ou um administrador pode gerar o relatório.");
        if (a.Plano == null) throw new RegraNegocioException("Salve o plano da auditoria antes de gerar o relatório.");
        var agora = _relogio.UtcAgora;
        var r = new Relatorio { AuditoriaId = a.Id, Situacao = SituacaoRelatorio.EmElaboracao };
        Historico.MarcarCriacao(r, ator, agora);
        db.Relatorios.Add(r);
        Historico.Registrar(db, ator, agora, "Relatorio", a.Id, "Início do relatório", $"Relatório da auditoria {a.Codigo} iniciado.");
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (Historico.EhViolacaoUnicidade(ex)) { /* outro usuário iniciou ao mesmo tempo */ }
    }

    public Task SalvarAsync(Ator ator, RelatorioEdicao dto) => GravarAsync(ator, dto, concluir: false);

    public async Task<ResultadoConclusao> ConcluirAsync(Ator ator, RelatorioEdicao dto) => await GravarAsync(ator, dto, concluir: true);

    private async Task<ResultadoConclusao> GravarAsync(Ator ator, RelatorioEdicao dto, bool concluir)
    {
        dto = Historico.Clonar(dto);
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await ProgramacaoServico.CarregarCompletaAsync(db, dto.AuditoriaId, rastrear: true) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        if (!Permissoes.PodeVisualizar(ator, a)) throw new AcessoNegadoException("Você não tem acesso a esta auditoria.");
        var rel = a.Relatorio ?? throw new RegraNegocioException("O relatório ainda não foi gerado.");
        if (rel.Situacao == SituacaoRelatorio.Concluido)
            throw new RegraNegocioException(ator.EhAdministrador
                ? "O relatório está concluído. Use \"Corrigir relatório\" para reabri-lo para revisão."
                : "O relatório está concluído e não pode mais ser alterado.");
        if (!Permissoes.PodeEditarRelatorio(ator, a))
            throw new AcessoNegadoException(rel.Situacao == SituacaoRelatorio.EmRevisao
                ? "O relatório foi reaberto para revisão administrativa; somente administradores podem alterá-lo."
                : "Somente o auditor líder designado ou um administrador pode alterar este relatório.");

        // ---- Validação
        var erros = new List<string>();
        var reqIds = a.Requisitos.Select(r => r.Id).ToHashSet();
        for (int i = 0; i < dto.Constatacoes.Count; i++)
        {
            var c = dto.Constatacoes[i];
            Normalizar(c);
            erros.AddRange(Validar(c, reqIds, $"Constatação {Rotulos.NumeroConstatacao(i + 1)}: "));
        }
        var existentes = rel.Constatacoes.ToDictionary(c => c.Id);
        if (dto.Constatacoes.Any(c => c.Id is int id && !existentes.ContainsKey(id))) erros.Add("Constatação inválida.");
        if (dto.Constatacoes.Where(c => c.Id != null).GroupBy(c => c.Id).Any(g => g.Count() > 1)) erros.Add("Constatação duplicada.");

        if (a.Tipo == TipoAuditoria.Fornecedor)
        {
            dto.ObservacoesConclusao = Validacoes.Limpar(dto.ObservacoesConclusao);
            dto.NumeroRqf = Validacoes.Limpar(dto.NumeroRqf);
            if (dto.Qualificacao != null && !Enum.IsDefined(dto.Qualificacao.Value)) erros.Add("Qualificação inválida.");
            if (dto.ObservacoesConclusao?.Length > 8000) erros.Add("As observações da conclusão devem ter até 8000 caracteres.");
            if (dto.NumeroRqf?.Length > 50) erros.Add("O número do RQF deve ter até 50 caracteres.");
        }
        else
        {
            dto.Qualificacao = null; dto.ObservacoesConclusao = null; dto.NumeroRqf = null;
        }
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        // ---- Aplicação (rascunho)
        var agora = _relogio.UtcAgora;
        Historico.MarcarAlteracao(db, rel, ator, agora, dto.Versao);
        rel.Qualificacao = dto.Qualificacao;
        rel.ObservacoesConclusao = dto.ObservacoesConclusao;
        rel.NumeroRqf = dto.NumeroRqf;
        var manter = dto.Constatacoes.Where(c => c.Id != null).Select(c => c.Id!.Value).ToHashSet();
        rel.Constatacoes.RemoveAll(c => !manter.Contains(c.Id));
        for (int i = 0; i < dto.Constatacoes.Count; i++)
        {
            var d = dto.Constatacoes[i];
            var c = d.Id is int id ? existentes[id] : new Constatacao();
            if (d.Id == null) rel.Constatacoes.Add(c);
            c.Numero = i + 1;
            c.Classificacao = d.Classificacao;
            c.Evidencia = d.Evidencia;
            c.DescricaoNaoConformidade = d.DescricaoNaoConformidade;
            c.EvidenciasObjetivas = d.EvidenciasObjetivas;
            c.NumeroPac = d.NumeroPac;
            c.Observacao = d.Observacao;
            c.OportunidadeMelhoria = d.OportunidadeMelhoria;
            c.Requisitos.RemoveAll(r => !d.AuditoriaRequisitoIds.Contains(r.AuditoriaRequisitoId));
            foreach (var r in d.AuditoriaRequisitoIds.Where(r => c.Requisitos.All(x => x.AuditoriaRequisitoId != r)))
                c.Requisitos.Add(new ConstatacaoRequisito { AuditoriaRequisitoId = r });
        }

        var cobertura = CalcularCobertura(a, dto.Constatacoes);
        var porId = a.Requisitos.ToDictionary(r => r.Id);
        var pendentes = cobertura.Pendentes.Select(id => porId[id]).Select(r => new RequisitoPendente(r.Id, r.NomeNorma, r.Codigo, r.Titulo)).ToList();
        var bloqueios = new List<string>();
        if (concluir)
        {
            if (a.Requisitos.Count == 0) bloqueios.Add("A auditoria não possui requisitos auditados. Inclua os requisitos na programação antes de concluir.");
            if (pendentes.Count > 0) bloqueios.Add($"Há {pendentes.Count} requisito(s) sem classificação de Conformidade ou Não conformidade.");
            if (a.Tipo == TipoAuditoria.Fornecedor && dto.Qualificacao == null) bloqueios.Add("Selecione a qualificação do fornecedor na seção Conclusão.");
            if (a.Plano == null || a.Plano.Periodos.Count == 0) bloqueios.Add("O plano precisa ter datas e horários definidos.");
        }

        bool concluido = concluir && bloqueios.Count == 0;
        if (concluido)
        {
            rel.Situacao = SituacaoRelatorio.Concluido;
            rel.ConcluidoEmUtc = agora;
            rel.ConcluidoPor = ator.Assinatura;
            rel.ConcluidoPorId = ator.UsuarioId;
        }
        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Relatorio", a.Id, concluido ? "Conclusão" : "Relatório salvo",
            concluido
                ? $"Relatório da auditoria {a.Codigo} concluído{(rel.NumeroRevisao > 0 ? $" (revisão {rel.NumeroRevisao})" : "")}. {cobertura.Resumo}."
                : $"Relatório da auditoria {a.Codigo} salvo: {dto.Constatacoes.Count} constatação(ões); {cobertura.Resumo}.",
            concluido ? RetratoRelatorio(a) : null);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new ResultadoConclusao(concluido, cobertura, pendentes, bloqueios);
    }

    private static object RetratoRelatorio(Auditoria a) => new
    {
        a.Codigo,
        a.Relatorio!.Situacao,
        a.Relatorio.NumeroRevisao,
        a.Relatorio.Qualificacao,
        a.Relatorio.ObservacoesConclusao,
        a.Relatorio.NumeroRqf,
        a.Relatorio.ConcluidoPor,
        a.Relatorio.ConcluidoEmUtc,
        Constatacoes = a.Relatorio.Constatacoes.OrderBy(c => c.Numero).Select(c => new
        {
            c.Numero, c.Classificacao,
            Requisitos = c.Requisitos.Select(r => a.Requisitos.FirstOrDefault(x => x.Id == r.AuditoriaRequisitoId) is { } ar ? $"{ar.NomeNorma} {ar.Codigo}" : "?").ToList(),
            c.Evidencia, c.DescricaoNaoConformidade, c.EvidenciasObjetivas, c.NumeroPac, c.Observacao, c.OportunidadeMelhoria
        }).ToList()
    };

    /// <summary>Correção administrativa: reabre o relatório concluído para revisão (exige nova conclusão).</summary>
    public async Task ReabrirAsync(Ator ator, int auditoriaId, string? motivo)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException("Somente administradores podem corrigir relatórios concluídos.");
        var m = Validacoes.Limpar(motivo);
        if (m == null) throw new RegraNegocioException("Informe o motivo da correção.");
        if (m.Length > 900) throw new RegraNegocioException("O motivo deve ter até 900 caracteres.");
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await ProgramacaoServico.CarregarCompletaAsync(db, auditoriaId, rastrear: true) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        if (a.Relatorio?.Situacao != SituacaoRelatorio.Concluido) throw new RegraNegocioException("Somente relatórios concluídos podem ser reabertos para correção.");
        var agora = _relogio.UtcAgora;
        var retrato = RetratoRelatorio(a);
        var rel = a.Relatorio;
        Historico.MarcarAlteracao(db, rel, ator, agora, null);
        rel.Situacao = SituacaoRelatorio.EmRevisao;
        rel.NumeroRevisao++;
        rel.ConcluidoEmUtc = null;
        rel.ConcluidoPor = null;
        rel.ConcluidoPorId = null;
        Historico.Registrar(db, ator, agora, "Relatorio", a.Id, "Reabertura para revisão",
            $"Relatório da auditoria {a.Codigo} reaberto para revisão {rel.NumeroRevisao}. Motivo: {m}", new { versaoConcluidaAnterior = retrato, motivo = m });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
