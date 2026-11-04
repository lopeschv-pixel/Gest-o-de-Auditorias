using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed class UsuarioEdicao
{
    public int? Id { get; set; }
    public Guid? Versao { get; set; }
    public string NomeRede { get; set; } = "";
    public string NomeCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Usuario;
    public bool Ativo { get; set; } = true;
    public int? AuditorId { get; set; }
    /// <summary>Somente na criação (senha temporária, troca obrigatória no primeiro acesso).</summary>
    public string? SenhaInicial { get; set; }
}

public sealed record UsuarioResumo(int Id, Guid Versao, string NomeRede, string NomeCompleto, string Email, PerfilUsuario Perfil,
    bool Ativo, int? AuditorId, string? AuditorNome, DateTime? UltimoAcessoUtc, bool Bloqueado);

public sealed record ResultadoLogin(bool Sucesso, Usuario? Usuario, string? Mensagem);

public class UsuarioServico
{
    public const int TentativasAntesBloqueio = 5;
    public static readonly TimeSpan DuracaoBloqueio = TimeSpan.FromMinutes(15);
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;
    private readonly PasswordHasher<Usuario> _hasher = new();

    public UsuarioServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public static IReadOnlyList<string> ValidarSenha(string? senha)
    {
        var erros = new List<string>();
        if (string.IsNullOrEmpty(senha) || senha.Length < 10) erros.Add("A senha deve ter pelo menos 10 caracteres.");
        else
        {
            if (!senha.Any(char.IsLetter)) erros.Add("A senha deve conter ao menos uma letra.");
            if (!senha.Any(char.IsDigit)) erros.Add("A senha deve conter ao menos um número.");
            if (senha.Length > 128) erros.Add("A senha deve ter no máximo 128 caracteres.");
        }
        return erros;
    }

    public static string NormalizarNomeRede(string? s) => (s ?? "").Trim().ToLowerInvariant();

    // ---------------------------------------------------------------- Autenticação

    public async Task<ResultadoLogin> AutenticarAsync(string? nomeRede, string? senha)
    {
        const string generica = "Nome da rede ou senha inválidos.";
        var nome = NormalizarNomeRede(nomeRede);
        if (nome.Length == 0 || string.IsNullOrEmpty(senha)) return new(false, null, generica);

        await using var db = await _fabrica.CreateDbContextAsync();
        var u = await db.Usuarios.Include(x => x.Auditor).FirstOrDefaultAsync(x => x.NomeRede == nome);
        var agora = _relogio.UtcAgora;
        if (u == null || u.SenhaHash == null)
        {
            _hasher.HashPassword(new Usuario(), senha); // custo equivalente, evita revelar usuários existentes
            return new(false, null, generica);
        }
        if (!u.Ativo) return new(false, null, generica);
        if (u.BloqueadoAteUtc is DateTime ate && ate > agora)
            return new(false, null, "Acesso temporariamente bloqueado por excesso de tentativas. Tente novamente mais tarde.");

        var r = _hasher.VerifyHashedPassword(u, u.SenhaHash, senha);
        if (r == PasswordVerificationResult.Failed)
        {
            u.FalhasAcesso++;
            if (u.FalhasAcesso >= TentativasAntesBloqueio)
            {
                u.BloqueadoAteUtc = agora + DuracaoBloqueio;
                u.FalhasAcesso = 0;
                Historico.Registrar(db, Ator.Sistema, agora, "Usuario", u.Id, "Bloqueio", $"Acesso de {u.NomeRede} bloqueado por tentativas inválidas.");
            }
            await db.SaveChangesAsync();
            return new(false, null, generica);
        }
        if (r == PasswordVerificationResult.SuccessRehashNeeded) u.SenhaHash = _hasher.HashPassword(u, senha);
        u.FalhasAcesso = 0;
        u.BloqueadoAteUtc = null;
        u.UltimoAcessoUtc = agora;
        await db.SaveChangesAsync();
        return new(true, u, null);
    }

    /// <summary>Carrega o ator a partir das claims do cookie. Retorna null se inativo ou se o carimbo de segurança mudou.</summary>
    public async Task<Ator?> ObterAtorAsync(int usuarioId, Guid carimbo)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        var u = await db.Usuarios.AsNoTracking().Include(x => x.Auditor).FirstOrDefaultAsync(x => x.Id == usuarioId);
        if (u == null || !u.Ativo || u.CarimboSeguranca != carimbo) return null;
        return new Ator(u.Id, u.NomeRede, u.NomeCompleto, u.Perfil, u.Auditor?.Id);
    }

    public async Task<bool> PrecisaTrocarSenhaAsync(int usuarioId)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        return await db.Usuarios.Where(x => x.Id == usuarioId).Select(x => x.DeveTrocarSenha).FirstOrDefaultAsync();
    }

    public async Task<Usuario> AlterarSenhaAsync(int usuarioId, string? senhaAtual, string? novaSenha, string? confirmacao)
    {
        var erros = new List<string>(ValidarSenha(novaSenha));
        if (novaSenha != confirmacao) erros.Add("A confirmação não confere com a nova senha.");
        await using var db = await _fabrica.CreateDbContextAsync();
        var u = await db.Usuarios.Include(x => x.Auditor).FirstOrDefaultAsync(x => x.Id == usuarioId && x.Ativo) ?? throw new NaoEncontradoException();
        if (u.SenhaHash == null || string.IsNullOrEmpty(senhaAtual) || _hasher.VerifyHashedPassword(u, u.SenhaHash, senhaAtual) == PasswordVerificationResult.Failed)
            erros.Insert(0, "A senha atual está incorreta.");
        else if (senhaAtual == novaSenha) erros.Add("A nova senha deve ser diferente da atual.");
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        var agora = _relogio.UtcAgora;
        u.SenhaHash = _hasher.HashPassword(u, novaSenha!);
        u.DeveTrocarSenha = false;
        u.CarimboSeguranca = Guid.NewGuid();
        var ator = new Ator(u.Id, u.NomeRede, u.NomeCompleto, u.Perfil, u.Auditor?.Id);
        Historico.Registrar(db, ator, agora, "Usuario", u.Id, "Senha alterada", $"{u.NomeRede} alterou a própria senha.");
        await db.SaveChangesAsync();
        return u;
    }

    /// <summary>Cria o primeiro administrador quando o banco não tem nenhum usuário.</summary>
    public async Task<bool> GarantirAdministradorInicialAsync(string? nomeRede, string? nomeCompleto, string? email, string? senha)
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        if (await db.Usuarios.AnyAsync()) return false;
        var nome = NormalizarNomeRede(nomeRede);
        if (nome.Length == 0 || ValidarSenha(senha).Count > 0)
            throw new InvalidOperationException(
                "Banco sem usuários: configure Inicializacao:AdminNomeRede e Inicializacao:AdminSenhaInicial (fora do repositório) " +
                "com uma senha de pelo menos 10 caracteres, letras e números.");
        var agora = _relogio.UtcAgora;
        var u = new Usuario
        {
            NomeRede = nome,
            NomeCompleto = string.IsNullOrWhiteSpace(nomeCompleto) ? "Administrador" : nomeCompleto.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? "" : email.Trim(),
            Perfil = PerfilUsuario.Administrador,
            Ativo = true,
            DeveTrocarSenha = true
        };
        Historico.MarcarCriacao(u, Ator.Sistema, agora);
        u.SenhaHash = _hasher.HashPassword(u, senha!);
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        Historico.Registrar(db, Ator.Sistema, agora, "Usuario", u.Id, "Criação", $"Administrador inicial {u.NomeRede} criado.");
        await db.SaveChangesAsync();
        return true;
    }

    // ---------------------------------------------------------------- Administração

    private static void ExigirAdmin(Ator ator)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
    }

    public async Task<List<UsuarioResumo>> ListarAsync(Ator ator)
    {
        ExigirAdmin(ator);
        await using var db = await _fabrica.CreateDbContextAsync();
        var agora = _relogio.UtcAgora;
        return await db.Usuarios.AsNoTracking().OrderBy(u => u.NomeCompleto)
            .Select(u => new UsuarioResumo(u.Id, u.Versao, u.NomeRede, u.NomeCompleto, u.Email, u.Perfil, u.Ativo,
                u.Auditor != null ? u.Auditor.Id : null, u.Auditor != null ? u.Auditor.NomeCompleto : null,
                u.UltimoAcessoUtc, u.BloqueadoAteUtc != null && u.BloqueadoAteUtc > agora))
            .ToListAsync();
    }

    public async Task<int> SalvarAsync(Ator ator, UsuarioEdicao dto)
    {
        ExigirAdmin(ator);
        var erros = new List<string>();
        var nomeRede = NormalizarNomeRede(dto.NomeRede);
        if (nomeRede.Length == 0) erros.Add("Informe o nome da rede.");
        else if (nomeRede.Length > 64 || nomeRede.Any(c => char.IsWhiteSpace(c) || c is '<' or '>' or '"' or '\''))
            erros.Add("O nome da rede deve ter até 64 caracteres, sem espaços.");
        if (string.IsNullOrWhiteSpace(dto.NomeCompleto)) erros.Add("Informe o nome completo.");
        if (!Validacoes.EmailValido(dto.Email)) erros.Add("Informe um e-mail válido.");
        if (!Enum.IsDefined(dto.Perfil)) erros.Add("Perfil inválido.");
        if (dto.Id == null) erros.AddRange(ValidarSenha(dto.SenhaInicial).Select(e => "Senha inicial: " + e));
        if (erros.Count > 0) throw new RegraNegocioException(erros);

        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var agora = _relogio.UtcAgora;

        if (await db.Usuarios.AnyAsync(u => u.NomeRede == nomeRede && u.Id != (dto.Id ?? 0)))
            throw new RegraNegocioException("Já existe um usuário com este nome da rede.");

        Usuario u;
        string acao;
        object? antes = null;
        if (dto.Id is int id)
        {
            u = await db.Usuarios.Include(x => x.Auditor).FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
            antes = new { u.NomeRede, u.NomeCompleto, u.Email, u.Perfil, u.Ativo, AuditorId = u.Auditor?.Id };
            var deixaDeSerAdminAtivo = u.Perfil == PerfilUsuario.Administrador && u.Ativo &&
                                       (dto.Perfil != PerfilUsuario.Administrador || !dto.Ativo);
            if (deixaDeSerAdminAtivo && !await db.Usuarios.AnyAsync(x => x.Id != u.Id && x.Ativo && x.Perfil == PerfilUsuario.Administrador))
                throw new RegraNegocioException("Não é possível remover o perfil de administrador ou inativar o último administrador ativo do sistema.");
            if ((u.Perfil != dto.Perfil || u.Ativo != dto.Ativo)) u.CarimboSeguranca = Guid.NewGuid(); // encerra sessões
            Historico.MarcarAlteracao(db, u, ator, agora, dto.Versao);
            acao = "Alteração";
        }
        else
        {
            u = new Usuario { DeveTrocarSenha = true };
            Historico.MarcarCriacao(u, ator, agora);
            u.SenhaHash = _hasher.HashPassword(u, dto.SenhaInicial!);
            db.Usuarios.Add(u);
            acao = "Criação";
        }
        u.NomeRede = nomeRede;
        u.NomeCompleto = dto.NomeCompleto.Trim();
        u.Email = dto.Email.Trim();
        u.Perfil = dto.Perfil;
        u.Ativo = dto.Ativo;

        // Vínculo com auditor (1:1)
        var atual = u.Id == 0 ? null : await db.Auditores.FirstOrDefaultAsync(a => a.UsuarioId == u.Id);
        if (atual?.Id != dto.AuditorId)
        {
            if (atual != null) atual.UsuarioId = null;
            if (dto.AuditorId is int auditorId)
            {
                var novo = await db.Auditores.FirstOrDefaultAsync(a => a.Id == auditorId) ?? throw new RegraNegocioException("Auditor não encontrado.");
                if (novo.UsuarioId != null && novo.UsuarioId != u.Id)
                    throw new RegraNegocioException($"O auditor {novo.NomeCompleto} já está vinculado a outro usuário.");
                novo.Usuario = u;
            }
            u.CarimboSeguranca = Guid.NewGuid();
        }

        await Historico.SalvarAsync(db);
        Historico.Registrar(db, ator, agora, "Usuario", u.Id, acao, $"{acao} do usuário {u.NomeRede}.",
            new { antes, depois = new { u.NomeRede, u.NomeCompleto, u.Email, u.Perfil, u.Ativo, dto.AuditorId } });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return u.Id;
    }

    public async Task RedefinirSenhaAsync(Ator ator, int id, string? senhaTemporaria)
    {
        ExigirAdmin(ator);
        var erros = ValidarSenha(senhaTemporaria);
        if (erros.Count > 0) throw new RegraNegocioException(erros);
        await using var db = await _fabrica.CreateDbContextAsync();
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
        var agora = _relogio.UtcAgora;
        u.SenhaHash = _hasher.HashPassword(u, senhaTemporaria!);
        u.DeveTrocarSenha = true;
        u.FalhasAcesso = 0;
        u.BloqueadoAteUtc = null;
        u.CarimboSeguranca = Guid.NewGuid();
        Historico.MarcarAlteracao(db, u, ator, agora, null);
        Historico.Registrar(db, ator, agora, "Usuario", u.Id, "Senha redefinida", $"Senha temporária definida para {u.NomeRede}.");
        await db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Ator ator, int id)
    {
        ExigirAdmin(ator);
        if (id == ator.UsuarioId) throw new RegraNegocioException("Você não pode excluir o seu próprio usuário.");
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == id) ?? throw new NaoEncontradoException();
        if (u.Perfil == PerfilUsuario.Administrador && u.Ativo &&
            !await db.Usuarios.AnyAsync(x => x.Id != u.Id && x.Ativo && x.Perfil == PerfilUsuario.Administrador))
            throw new RegraNegocioException("Não é possível excluir o último administrador ativo do sistema.");
        var aud = await db.Auditores.FirstOrDefaultAsync(a => a.UsuarioId == u.Id);
        if (aud != null) aud.UsuarioId = null;
        var agora = _relogio.UtcAgora;
        db.Usuarios.Remove(u);
        Historico.Registrar(db, ator, agora, "Usuario", u.Id, "Exclusão", $"Usuário {u.NomeRede} ({u.NomeCompleto}) excluído.",
            new { u.NomeRede, u.NomeCompleto, u.Email, u.Perfil });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
