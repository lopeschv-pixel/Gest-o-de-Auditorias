using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed class PeriodoEdicao
{
    public DateOnly? Data { get; set; }
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFim { get; set; }
}

public sealed class PlanoEdicao
{
    public int AuditoriaId { get; set; }
    public Guid? Versao { get; set; }
    public string? Local { get; set; }
    public string? GestorNome { get; set; }
    public string? GestorEmail { get; set; }
    public string? Objetivo { get; set; }
    public string? Escopo { get; set; }
    public List<PeriodoEdicao> Periodos { get; set; } = new();
}

public class PlanoServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public PlanoServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public static PlanoEdicao ParaEdicao(Auditoria a) => a.Plano == null
        ? new PlanoEdicao { AuditoriaId = a.Id, Periodos = new() { new PeriodoEdicao() } }
        : new PlanoEdicao
        {
            AuditoriaId = a.Id,
            Versao = a.Plano.Versao,
            Local = a.Plano.Local,
            GestorNome = a.Plano.GestorNome,
            GestorEmail = a.Plano.GestorEmail,
            Objetivo = a.Plano.Objetivo,
            Escopo = a.Plano.Escopo,
            Periodos = a.Plano.Periodos.OrderBy(p => p.Data).ThenBy(p => p.HoraInicio)
                .Select(p => new PeriodoEdicao { Data = p.Data, HoraInicio = p.HoraInicio, HoraFim = p.HoraFim }).ToList()
        };

    /// <summary>Valida datas, horários, duplicidades e sobreposições no mesmo dia.</summary>
    public static List<string> ValidarPeriodos(IReadOnlyList<PeriodoEdicao> periodos)
    {
        var erros = new List<string>();
        if (periodos.Count == 0) erros.Add("Informe ao menos um período (data, horário inicial e horário final).");
        for (int i = 0; i < periodos.Count; i++)
        {
            var p = periodos[i];
            var n = i + 1;
            if (p.Data == null) erros.Add($"Período {n}: informe a data.");
            if (p.HoraInicio == null) erros.Add($"Período {n}: informe o horário inicial.");
            if (p.HoraFim == null) erros.Add($"Período {n}: informe o horário final.");
            if (p.HoraInicio != null && p.HoraFim != null && p.HoraFim <= p.HoraInicio)
                erros.Add($"Período {n}: o horário final deve ser posterior ao inicial.");
            if (p.Data is DateOnly d && (d.Year < 2000 || d.Year > 2100)) erros.Add($"Período {n}: data inválida.");
        }
        if (erros.Count > 0) return erros;
        var ordenados = periodos.Select((p, i) => (p, i)).OrderBy(x => x.p.Data).ThenBy(x => x.p.HoraInicio).ToList();
        for (int k = 1; k < ordenados.Count; k++)
        {
            var (a, ia) = ordenados[k - 1];
            var (b, ib) = ordenados[k];
            if (a.Data != b.Data) continue;
            if (a.HoraInicio == b.HoraInicio && a.HoraFim == b.HoraFim)
                erros.Add($"Os períodos {ia + 1} e {ib + 1} estão duplicados ({Rotulos.Periodo(a.Data!.Value, a.HoraInicio!.Value, a.HoraFim!.Value)}).");
            else if (b.HoraInicio < a.HoraFim)
                erros.Add($"Os períodos {ia + 1} e {ib + 1} se sobrepõem em {Rotulos.Data(a.Data!.Value)}.");
        }
        return erros;
    }

    public async Task SalvarAsync(Ator ator, PlanoEdicao dto)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var a = await ProgramacaoServico.CarregarCompletaAsync(db, dto.AuditoriaId, rastrear: true) ?? throw new NaoEncontradoException("Auditoria não encontrada.");
        if (!Permissoes.PodeVisualizar(ator, a)) throw new AcessoNegadoException("Você não tem acesso a esta auditoria.");
        if (a.Relatorio?.Situacao == SituacaoRelatorio.Concluido)
            throw new RegraNegocioException("O relatório desta auditoria está concluído; o plano fica bloqueado. Para corrigir, o administrador deve reabrir o relatório para revisão.");
        if (!Permissoes.PodeEditarPlano(ator, a))
            throw new AcessoNegadoException("Somente o auditor líder designado ou um administrador pode gerar ou alterar o plano desta auditoria.");

        var local = Validacoes.Limpar(dto.Local);
        var gestor = Validacoes.Limpar(dto.GestorNome);
        var email = Validacoes.Limpar(dto.GestorEmail);
        var objetivo = a.Tipo == TipoAuditoria.Fornecedor ? Validacoes.Limpar(dto.Objetivo) : null;
        var escopo = a.Tipo == TipoAuditoria.Fornecedor ? Validacoes.Limpar(dto.Escopo) : null;

        var erros = ValidarPeriodos(dto.Periodos);
        if (!ator.EhAdministrador)
        {
            // Auditor líder: apenas datas e horários. Demais campos permanecem como estão.
            var p = a.Plano;
            bool alterou = local != p?.Local || gestor != p?.GestorNome || email != p?.GestorEmail ||
                           objetivo != (a.Tipo == TipoAuditoria.Fornecedor ? p?.Objetivo : null) ||
                           escopo != (a.Tipo == TipoAuditoria.Fornecedor ? p?.Escopo : null);
            if (alterou) throw new AcessoNegadoException("Como auditor líder, você pode alterar apenas as datas e os horários do plano.");
        }
        else
        {
            if (local?.Length > 300) erros.Add("O local deve ter até 300 caracteres.");
            if (gestor?.Length > 200) erros.Add("O nome do gestor deve ter até 200 caracteres.");
            if (email != null && !Validacoes.EmailValido(email)) erros.Add("Informe um e-mail do gestor válido.");
            if (objetivo?.Length > 4000) erros.Add("O objetivo deve ter até 4000 caracteres.");
            if (escopo?.Length > 4000) erros.Add("O escopo deve ter até 4000 caracteres.");
        }
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        var agora = _relogio.UtcAgora;
        var novo = a.Plano == null;
        var antes = novo ? null : new
        {
            a.Plano!.Local, a.Plano.GestorNome, a.Plano.GestorEmail, a.Plano.Objetivo, a.Plano.Escopo,
            Periodos = a.Plano.Periodos.OrderBy(x => x.Data).ThenBy(x => x.HoraInicio).Select(x => Rotulos.Periodo(x.Data, x.HoraInicio, x.HoraFim)).ToList()
        };
        var plano = a.Plano;
        if (plano == null)
        {
            plano = new Plano { AuditoriaId = a.Id };
            Historico.MarcarCriacao(plano, ator, agora);
            a.Plano = plano;
        }
        else Historico.MarcarAlteracao(db, plano, ator, agora, dto.Versao);

        if (ator.EhAdministrador)
        {
            plano.Local = local;
            plano.GestorNome = gestor;
            plano.GestorEmail = email;
            plano.Objetivo = objetivo;
            plano.Escopo = escopo;
        }
        plano.Periodos.Clear();
        foreach (var p in dto.Periodos.OrderBy(x => x.Data).ThenBy(x => x.HoraInicio))
            plano.Periodos.Add(new PlanoPeriodo { Data = p.Data!.Value, HoraInicio = p.HoraInicio!.Value, HoraFim = p.HoraFim!.Value });

        var depois = new
        {
            plano.Local, plano.GestorNome, plano.GestorEmail, plano.Objetivo, plano.Escopo,
            Periodos = plano.Periodos.Select(x => Rotulos.Periodo(x.Data, x.HoraInicio, x.HoraFim)).ToList()
        };
        var aposRelatorio = a.Relatorio != null ? " (alteração registrada após o início do relatório)" : "";
        Historico.Registrar(db, ator, agora, "Plano", a.Id, novo ? "Criação do plano" : "Alteração do plano",
            $"Plano da auditoria {a.Codigo} {(novo ? "gerado" : "alterado")}{aposRelatorio}.", new { antes, depois });
        await Historico.SalvarAsync(db);
        await tx.CommitAsync();
    }
}
