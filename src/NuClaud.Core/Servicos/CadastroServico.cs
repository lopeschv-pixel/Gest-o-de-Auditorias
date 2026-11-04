using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed class AreaEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public sealed class MacroprocessoEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public sealed class ProcessoEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public string Nome { get; set; } = "";
    public int MacroprocessoId { get; set; }
    public bool Ativo { get; set; } = true;
    public HashSet<int> NormaIds { get; set; } = new();
    public HashSet<int> RequisitoIds { get; set; } = new();
}

/// <summary>Áreas, macroprocessos e processos.</summary>
public class CadastroServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public CadastroServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    private static void ExigirAdmin(Ator a) { if (!a.EhAdministrador) throw new AcessoNegadoException(); }

    // ----------------------------------------------------------- Áreas

    public async Task<List<Area>> ListarAreasAsync(bool somenteAtivas = false)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Areas.AsNoTracking().Where(a => !somenteAtivas || a.Ativo).OrderBy(a => a.Nome).ToListAsync();
    }

    public async Task<Dictionary<int, int>> ContarUsoAreasAsync()
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.AuditoriaAreas.GroupBy(x => x.AreaId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
    }

    public async Task<int> SalvarAreaAsync(Ator ator, AreaEdicao dto)
    {
        ExigirAdmin(ator);
        var nome = Validacoes.Limpar(dto.Nome);
        if (nome == null) throw new RegraNegocioException("Informe o nome da área.");
        if (nome.Length > 150) throw new RegraNegocioException("O nome da área deve ter até 150 caracteres.");
        await using var db = await _fabrica.CreateDbContextAsync();
        if (await db.Areas.AnyAsync(a => a.Nome == nome && a.Id != (dto.Id ?? 0)))
            throw new RegraNegocioException("Já existe uma área com este nome.");
        var agora = _relogio.UtcAgora;
        Area area;
        string? nomeAnterior = null;
        if (dto.Id is int id)
        {
            area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NaoEncontradoException();
            nomeAnterior = area.Nome;
            Historico.MarcarAlteracao(db, area, ator, agora, dto.Versao);
        }
        else
        {
            area = new Area();
            Historico.MarcarCriacao(area, ator, agora);
            db.Areas.Add(area);
        }
        area.Nome = nome;
        area.Ativo = dto.Ativo;
        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Area", area.Id, dto.Id == null ? "Criação" : "Alteração",
            dto.Id == null ? $"Área \"{nome}\" cadastrada." : $"Área \"{nomeAnterior}\" alterada para \"{nome}\" ({(dto.Ativo ? "ativa" : "inativa")}).");
        await db.SaveChangesAsync();
        return area.Id;
    }

    public async Task ExcluirAreaAsync(Ator ator, int id)
    {
        ExigirAdmin(ator);
        await using var db = await _fabrica.CreateDbContextAsync();
        var area = await db.Areas.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NaoEncontradoException();
        var usos = await db.AuditoriaAreas.CountAsync(x => x.AreaId == id);
        if (usos > 0)
            throw new RegraNegocioException($"A área \"{area.Nome}\" já foi usada em {usos} auditoria(s). Para preservar o histórico, inative-a em vez de excluir.");
        db.Areas.Remove(area);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Area", id, "Exclusão", $"Área \"{area.Nome}\" excluída.");
        await db.SaveChangesAsync();
    }

    // ----------------------------------------------------------- Macroprocessos

    public async Task<List<Macroprocesso>> ListarMacroprocessosAsync(bool somenteAtivos = false)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Macroprocessos.AsNoTracking().Where(m => !somenteAtivos || m.Ativo).OrderBy(m => m.Nome).ToListAsync();
    }

    public async Task<int> SalvarMacroprocessoAsync(Ator ator, MacroprocessoEdicao dto)
    {
        ExigirAdmin(ator);
        var nome = Validacoes.Limpar(dto.Nome);
        if (nome == null) throw new RegraNegocioException("Informe o nome do macroprocesso.");
        if (nome.Length > 150) throw new RegraNegocioException("O nome deve ter até 150 caracteres.");
        await using var db = await _fabrica.CreateDbContextAsync();
        if (await db.Macroprocessos.AnyAsync(a => a.Nome == nome && a.Id != (dto.Id ?? 0)))
            throw new RegraNegocioException("Já existe um macroprocesso com este nome.");
        var agora = _relogio.UtcAgora;
        Macroprocesso m;
        string? anterior = null;
        if (dto.Id is int id)
        {
            m = await db.Macroprocessos.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NaoEncontradoException();
            anterior = m.Nome;
            Historico.MarcarAlteracao(db, m, ator, agora, dto.Versao);
        }
        else
        {
            m = new Macroprocesso();
            Historico.MarcarCriacao(m, ator, agora);
            db.Macroprocessos.Add(m);
        }
        m.Nome = nome;
        m.Ativo = dto.Ativo;
        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Macroprocesso", m.Id, dto.Id == null ? "Criação" : "Alteração",
            dto.Id == null ? $"Macroprocesso \"{nome}\" cadastrado." : $"Macroprocesso \"{anterior}\" alterado para \"{nome}\" ({(dto.Ativo ? "ativo" : "inativo")}).");
        await db.SaveChangesAsync();
        return m.Id;
    }

    public async Task ExcluirMacroprocessoAsync(Ator ator, int id)
    {
        ExigirAdmin(ator);
        await using var db = await _fabrica.CreateDbContextAsync();
        var m = await db.Macroprocessos.FirstOrDefaultAsync(a => a.Id == id) ?? throw new NaoEncontradoException();
        if (await db.Processos.AnyAsync(p => p.MacroprocessoId == id))
            throw new RegraNegocioException($"O macroprocesso \"{m.Nome}\" possui processos cadastrados. Exclua ou mova os processos, ou inative o macroprocesso.");
        var usos = await db.AuditoriaMacroprocessos.CountAsync(x => x.MacroprocessoId == id);
        if (usos > 0)
            throw new RegraNegocioException($"O macroprocesso \"{m.Nome}\" já foi usado em {usos} auditoria(s). Inative-o em vez de excluir.");
        db.Macroprocessos.Remove(m);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Macroprocesso", id, "Exclusão", $"Macroprocesso \"{m.Nome}\" excluído.");
        await db.SaveChangesAsync();
    }

    // ----------------------------------------------------------- Processos

    public async Task<List<Processo>> ListarProcessosAsync(bool somenteAtivos = false)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Processos.AsNoTracking()
            .Include(p => p.Macroprocesso)
            .Include(p => p.Normas)
            .Include(p => p.RequisitosObrigatorios).ThenInclude(r => r.Requisito)
            .Where(p => !somenteAtivos || p.Ativo)
            .OrderBy(p => p.Macroprocesso!.Nome).ThenBy(p => p.Nome)
            .AsSplitQuery()
            .ToListAsync();
    }

    private static object RetratoProcesso(Processo p, IReadOnlyDictionary<int, Requisito> reqs, IReadOnlyDictionary<int, string> normas) => new
    {
        p.Nome,
        p.MacroprocessoId,
        p.Ativo,
        Normas = p.Normas.Select(n => normas.TryGetValue(n.NormaId, out var nn) ? nn : n.NormaId.ToString()).OrderBy(x => x).ToList(),
        RequisitosObrigatorios = p.RequisitosObrigatorios
            .Select(r => reqs.TryGetValue(r.RequisitoId, out var q) ? $"{(normas.TryGetValue(q.NormaId, out var nn) ? nn : "")} {q.Codigo}" : r.RequisitoId.ToString())
            .OrderBy(x => x).ToList()
    };

    public async Task<int> SalvarProcessoAsync(Ator ator, ProcessoEdicao dto)
    {
        ExigirAdmin(ator);
        var erros = new List<string>();
        var nome = Validacoes.Limpar(dto.Nome);
        if (nome == null) erros.Add("Informe o nome do processo.");
        else if (nome.Length > 150) erros.Add("O nome deve ter até 150 caracteres.");
        if (dto.MacroprocessoId <= 0) erros.Add("Selecione o macroprocesso.");
        if (dto.NormaIds.Count == 0) erros.Add("Selecione ao menos uma norma aplicável.");
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await db.Macroprocessos.AnyAsync(m => m.Id == dto.MacroprocessoId)) throw new RegraNegocioException("Macroprocesso não encontrado.");
        var normas = await db.Normas.AsNoTracking().ToDictionaryAsync(n => n.Id, n => n.Nome);
        if (dto.NormaIds.Any(n => !normas.ContainsKey(n))) throw new RegraNegocioException("Norma inválida.");
        var reqs = await db.Requisitos.AsNoTracking().Where(r => dto.RequisitoIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        if (reqs.Count != dto.RequisitoIds.Count) throw new RegraNegocioException("Requisito inválido.");
        var foraDasNormas = reqs.Values.Where(r => !dto.NormaIds.Contains(r.NormaId)).ToList();
        if (foraDasNormas.Count > 0)
            throw new RegraNegocioException("Os requisitos obrigatórios devem pertencer às normas selecionadas para o processo.");
        if (await db.Processos.AnyAsync(p => p.MacroprocessoId == dto.MacroprocessoId && p.Nome == nome && p.Id != (dto.Id ?? 0)))
            throw new RegraNegocioException("Já existe um processo com este nome neste macroprocesso.");

        var agora = _relogio.UtcAgora;
        Processo p;
        object? antes = null;
        if (dto.Id is int id)
        {
            p = await db.Processos.Include(x => x.Normas).Include(x => x.RequisitosObrigatorios).FirstOrDefaultAsync(x => x.Id == id)
                ?? throw new NaoEncontradoException();
            var reqsAntes = await db.Requisitos.AsNoTracking().Where(r => p.RequisitosObrigatorios.Select(x => x.RequisitoId).Contains(r.Id)).ToDictionaryAsync(r => r.Id);
            antes = RetratoProcesso(p, reqsAntes, normas);
            Historico.MarcarAlteracao(db, p, ator, agora, dto.Versao);
            // Novos requisitos obrigatórios precisam estar ativos; os já existentes (mesmo inativados depois) podem permanecer.
            var novos = dto.RequisitoIds.Except(p.RequisitosObrigatorios.Select(r => r.RequisitoId));
            if (novos.Any(r => !reqs[r].Ativo)) throw new RegraNegocioException("Requisitos inativos não podem ser adicionados.");
        }
        else
        {
            if (reqs.Values.Any(r => !r.Ativo)) throw new RegraNegocioException("Requisitos inativos não podem ser adicionados.");
            p = new Processo();
            Historico.MarcarCriacao(p, ator, agora);
            db.Processos.Add(p);
        }
        p.Nome = nome!;
        p.MacroprocessoId = dto.MacroprocessoId;
        p.Ativo = dto.Ativo;
        p.Normas.RemoveAll(n => !dto.NormaIds.Contains(n.NormaId));
        foreach (var n in dto.NormaIds.Where(n => p.Normas.All(x => x.NormaId != n))) p.Normas.Add(new ProcessoNorma { NormaId = n });
        p.RequisitosObrigatorios.RemoveAll(r => !dto.RequisitoIds.Contains(r.RequisitoId));
        foreach (var r in dto.RequisitoIds.Where(r => p.RequisitosObrigatorios.All(x => x.RequisitoId != r)))
            p.RequisitosObrigatorios.Add(new ProcessoRequisito { RequisitoId = r });

        await Historico.SalvarAsync(db);
        var depois = RetratoProcesso(p, reqs, normas);
        Historico.Registrar(db, ator, agora, "Processo", p.Id, dto.Id == null ? "Criação" : "Alteração",
            $"{(dto.Id == null ? "Cadastro" : "Alteração")} do processo \"{p.Nome}\": {p.Normas.Count} norma(s), {p.RequisitosObrigatorios.Count} requisito(s) obrigatório(s). " +
            "Auditorias já programadas mantêm a configuração registrada no momento da programação.",
            new { antes, depois });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return p.Id;
    }

    public async Task ExcluirProcessoAsync(Ator ator, int id)
    {
        ExigirAdmin(ator);
        await using var db = await _fabrica.CreateDbContextAsync();
        var p = await db.Processos.Include(x => x.Normas).Include(x => x.RequisitosObrigatorios).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NaoEncontradoException();
        var usos = await db.AuditoriaProcessos.CountAsync(x => x.ProcessoId == id);
        if (usos > 0)
            throw new RegraNegocioException($"O processo \"{p.Nome}\" já foi usado em {usos} auditoria(s). Inative-o em vez de excluir.");
        db.Processos.Remove(p);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Processo", id, "Exclusão", $"Processo \"{p.Nome}\" excluído.");
        await db.SaveChangesAsync();
    }
}
