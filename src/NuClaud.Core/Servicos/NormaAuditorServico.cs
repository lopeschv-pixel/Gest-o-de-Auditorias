using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed class NormaEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public string Nome { get; set; } = "";
    public string? Edicao { get; set; }
    public string? FonteCarga { get; set; }
    public GrupoCorNorma GrupoCor { get; set; }
    public bool Ativo { get; set; } = true;
}

public sealed class RequisitoEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public int NormaId { get; set; }
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public sealed class HabilitacaoEdicao
{
    public int NormaId { get; set; }
    public bool PodeLiderar { get; set; }
    public bool PodeAuditar { get; set; }
    public bool PodeObservar { get; set; }
}

public sealed class AuditorEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public string NomeCompleto { get; set; } = "";
    public string? Email { get; set; }
    public int? UsuarioId { get; set; }
    public bool Ativo { get; set; } = true;
    public List<HabilitacaoEdicao> Habilitacoes { get; set; } = new();
}

public class NormaServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public NormaServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    private static void ExigirAdmin(Ator a) { if (!a.EhAdministrador) throw new AcessoNegadoException(); }

    public async Task<List<Norma>> ListarAsync(bool somenteAtivas = false, bool incluirRequisitos = false)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        IQueryable<Norma> q = db.Normas.AsNoTracking();
        if (incluirRequisitos) q = q.Include(n => n.Requisitos);
        var lista = await q.Where(n => !somenteAtivas || n.Ativo).OrderBy(n => n.Ordem).ThenBy(n => n.Nome).ToListAsync();
        foreach (var n in lista)
            n.Requisitos = n.Requisitos.OrderBy(r => r.ChaveOrdenacao, StringComparer.Ordinal).ToList();
        return lista;
    }

    public async Task<List<Requisito>> ListarRequisitosAsync(int normaId)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Requisitos.AsNoTracking().Where(r => r.NormaId == normaId).OrderBy(r => r.ChaveOrdenacao).ToListAsync();
    }

    public async Task<int> SalvarNormaAsync(Ator ator, NormaEdicao dto)
    {
        ExigirAdmin(ator);
        var nome = Validacoes.Limpar(dto.Nome);
        if (nome == null) throw new RegraNegocioException("Informe o nome da norma.");
        if (nome.Length > 100) throw new RegraNegocioException("O nome da norma deve ter até 100 caracteres.");
        if ((dto.Edicao?.Length ?? 0) > 200) throw new RegraNegocioException("A edição deve ter até 200 caracteres.");
        if (!Enum.IsDefined(dto.GrupoCor)) throw new RegraNegocioException("Grupo de cor inválido.");
        await using var db = await _fabrica.CreateDbContextAsync();
        if (await db.Normas.AnyAsync(n => n.Nome == nome && n.Id != (dto.Id ?? 0)))
            throw new RegraNegocioException("Já existe uma norma com este nome.");
        var agora = _relogio.UtcAgora;
        Norma n;
        if (dto.Id is int id)
        {
            n = await db.Normas.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
            Historico.MarcarAlteracao(db, n, ator, agora, dto.Versao);
        }
        else
        {
            n = new Norma { Ordem = (await db.Normas.MaxAsync(x => (int?)x.Ordem) ?? 0) + 1 };
            Historico.MarcarCriacao(n, ator, agora);
            db.Normas.Add(n);
        }
        n.Nome = nome;
        n.Edicao = Validacoes.Limpar(dto.Edicao);
        n.FonteCarga = Validacoes.Limpar(dto.FonteCarga);
        n.GrupoCor = dto.GrupoCor;
        n.Ativo = dto.Ativo;
        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Norma", n.Id, dto.Id == null ? "Criação" : "Alteração", $"Norma \"{n.Nome}\" {(dto.Id == null ? "cadastrada" : "alterada")}.",
            new { n.Nome, n.Edicao, n.GrupoCor, n.Ativo });
        await db.SaveChangesAsync();
        return n.Id;
    }

    public async Task ExcluirNormaAsync(Ator ator, int id)
    {
        ExigirAdmin(ator);
        await using var db = await _fabrica.CreateDbContextAsync();
        var n = await db.Normas.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
        if (await db.Requisitos.AnyAsync(r => r.NormaId == id))
            throw new RegraNegocioException($"A norma \"{n.Nome}\" possui requisitos cadastrados. Inative a norma para impedir novos usos.");
        if (await db.AuditoriaNormas.AnyAsync(x => x.NormaId == id) || await db.ProcessoNormas.AnyAsync(x => x.NormaId == id) ||
            await db.Habilitacoes.AnyAsync(x => x.NormaId == id))
            throw new RegraNegocioException($"A norma \"{n.Nome}\" está vinculada a auditorias, processos ou habilitações. Inative-a em vez de excluir.");
        db.Normas.Remove(n);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Norma", id, "Exclusão", $"Norma \"{n.Nome}\" excluída.");
        await db.SaveChangesAsync();
    }

    public async Task<int> SalvarRequisitoAsync(Ator ator, RequisitoEdicao dto)
    {
        ExigirAdmin(ator);
        var erros = new List<string>();
        var codigo = (dto.Codigo ?? "").Trim();
        var titulo = Validacoes.Limpar(dto.Titulo);
        if (!OrdenacaoRequisitos.CodigoValido(codigo))
            erros.Add("O número do requisito deve ter dois ou três níveis numéricos, por exemplo 4.1 ou 4.4.1 (não use apenas o capítulo, como \"4\", nem quatro níveis, como \"4.4.1.1\").");
        if (titulo == null) erros.Add("Informe o título do requisito.");
        else if (titulo.Length > 300) erros.Add("O título deve ter até 300 caracteres.");
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        await using var db = await _fabrica.CreateDbContextAsync();
        if (!await db.Normas.AnyAsync(n => n.Id == dto.NormaId)) throw new RegraNegocioException("Norma não encontrada.");
        if (await db.Requisitos.AnyAsync(r => r.NormaId == dto.NormaId && r.Codigo == codigo && r.Id != (dto.Id ?? 0)))
            throw new RegraNegocioException($"Já existe o requisito {codigo} nesta norma.");
        var agora = _relogio.UtcAgora;
        Requisito r;
        string? antes = null;
        if (dto.Id is int id)
        {
            r = await db.Requisitos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
            if (r.NormaId != dto.NormaId) throw new RegraNegocioException("Não é possível mover um requisito para outra norma.");
            antes = OrdenacaoRequisitos.Exibicao(r.Codigo, r.Titulo);
            Historico.MarcarAlteracao(db, r, ator, agora, dto.Versao);
        }
        else
        {
            r = new Requisito { NormaId = dto.NormaId };
            Historico.MarcarCriacao(r, ator, agora);
            db.Requisitos.Add(r);
        }
        r.Codigo = codigo;
        r.Titulo = titulo!;
        r.ChaveOrdenacao = OrdenacaoRequisitos.Chave(codigo);
        r.Ativo = dto.Ativo;
        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Requisito", r.Id, dto.Id == null ? "Criação" : "Alteração",
            dto.Id == null ? $"Requisito {OrdenacaoRequisitos.Exibicao(r.Codigo, r.Titulo)} cadastrado."
                           : $"Requisito \"{antes}\" alterado para \"{OrdenacaoRequisitos.Exibicao(r.Codigo, r.Titulo)}\" ({(r.Ativo ? "ativo" : "inativo")}). Documentos existentes mantêm o retrato anterior.");
        await db.SaveChangesAsync();
        return r.Id;
    }

    public async Task ExcluirRequisitoAsync(Ator ator, int id)
    {
        ExigirAdmin(ator);
        await using var db = await _fabrica.CreateDbContextAsync();
        var r = await db.Requisitos.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
        if (await db.AuditoriaRequisitos.AnyAsync(x => x.RequisitoId == id))
            throw new RegraNegocioException($"O requisito {r.Codigo} já foi usado em auditorias. Inative-o em vez de excluir.");
        if (await db.ProcessoRequisitos.AnyAsync(x => x.RequisitoId == id))
            throw new RegraNegocioException($"O requisito {r.Codigo} é obrigatório em processos cadastrados. Retire-o dos processos ou inative-o.");
        db.Requisitos.Remove(r);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Requisito", id, "Exclusão", $"Requisito {OrdenacaoRequisitos.Exibicao(r.Codigo, r.Titulo)} excluído.");
        await db.SaveChangesAsync();
    }
}

public class AuditorServico
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public AuditorServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public async Task<List<Auditor>> ListarAsync(bool somenteAtivos = false)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Auditores.AsNoTracking().Include(a => a.Habilitacoes).Include(a => a.Usuario)
            .Where(a => !somenteAtivos || a.Ativo).OrderBy(a => a.NomeCompleto).ToListAsync();
    }

    public async Task<Dictionary<int, int>> ContarParticipacoesAsync()
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.AuditoriaParticipantes.GroupBy(p => p.AuditorId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
    }

    public async Task<int> SalvarAsync(Ator ator, AuditorEdicao dto)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        var erros = new List<string>();
        var nome = Validacoes.Limpar(dto.NomeCompleto);
        if (nome == null) erros.Add("Informe o nome completo.");
        else if (nome.Length > 200) erros.Add("O nome deve ter até 200 caracteres.");
        var email = Validacoes.Limpar(dto.Email);
        if (email != null && !Validacoes.EmailValido(email)) erros.Add("E-mail inválido.");
        if (dto.Habilitacoes.GroupBy(h => h.NormaId).Any(g => g.Count() > 1)) erros.Add("Habilitação duplicada para a mesma norma.");
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var normaIds = await db.Normas.Select(n => n.Id).ToListAsync();
        if (dto.Habilitacoes.Any(h => !normaIds.Contains(h.NormaId))) throw new RegraNegocioException("Norma inválida nas habilitações.");
        if (dto.UsuarioId is int uid)
        {
            if (!await db.Usuarios.AnyAsync(u => u.Id == uid)) throw new RegraNegocioException("Usuário não encontrado.");
            var outro = await db.Auditores.FirstOrDefaultAsync(a => a.UsuarioId == uid && a.Id != (dto.Id ?? 0));
            if (outro != null) throw new RegraNegocioException($"Este usuário já está vinculado ao auditor {outro.NomeCompleto}.");
        }

        var agora = _relogio.UtcAgora;
        Auditor a;
        if (dto.Id is int id)
        {
            a = await db.Auditores.Include(x => x.Habilitacoes).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
            Historico.MarcarAlteracao(db, a, ator, agora, dto.Versao);
        }
        else
        {
            a = new Auditor();
            Historico.MarcarCriacao(a, ator, agora);
            db.Auditores.Add(a);
        }
        var usuarioAnterior = a.UsuarioId;
        a.NomeCompleto = nome!;
        a.Email = email;
        a.UsuarioId = dto.UsuarioId;
        a.Ativo = dto.Ativo;

        var desejadas = dto.Habilitacoes.Where(h => h.PodeLiderar || h.PodeAuditar || h.PodeObservar).ToDictionary(h => h.NormaId);
        a.Habilitacoes.RemoveAll(h => !desejadas.ContainsKey(h.NormaId));
        foreach (var d in desejadas.Values)
        {
            var h = a.Habilitacoes.FirstOrDefault(x => x.NormaId == d.NormaId);
            if (h == null) a.Habilitacoes.Add(h = new HabilitacaoAuditor { NormaId = d.NormaId });
            h.PodeLiderar = d.PodeLiderar;
            h.PodeAuditar = d.PodeAuditar;
            h.PodeObservar = d.PodeObservar;
        }

        // Vínculo alterado: encerra sessões dos usuários afetados (permissões mudam).
        if (usuarioAnterior != dto.UsuarioId)
            foreach (var uidAfetado in new[] { usuarioAnterior, dto.UsuarioId }.Where(x => x != null))
            {
                var u = await db.Usuarios.FirstAsync(x => x.Id == uidAfetado);
                u.CarimboSeguranca = Guid.NewGuid();
            }

        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Auditor", a.Id, dto.Id == null ? "Criação" : "Alteração",
            $"Auditor {a.NomeCompleto} {(dto.Id == null ? "cadastrado" : "alterado")}. Participações anteriores são preservadas.",
            new { a.NomeCompleto, a.Email, a.UsuarioId, a.Ativo, Habilitacoes = desejadas.Values });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return a.Id;
    }

    public async Task ExcluirAsync(Ator ator, int id)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var a = await db.Auditores.Include(x => x.Habilitacoes).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
        var usos = await db.AuditoriaParticipantes.CountAsync(p => p.AuditorId == id);
        if (usos > 0)
            throw new RegraNegocioException($"{a.NomeCompleto} possui {usos} participação(ões) em auditorias. Inative o auditor para preservar o histórico.");
        db.Auditores.Remove(a);
        Historico.Registrar(db, ator, _relogio.UtcAgora, "Auditor", id, "Exclusão", $"Auditor {a.NomeCompleto} excluído.");
        await db.SaveChangesAsync();
    }
}
