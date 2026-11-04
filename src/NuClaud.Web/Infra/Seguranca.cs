using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Caching.Memory;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Web.Infra;

public static class ClaimsNuclaud
{
    public const string Carimbo = "nuclaud:carimbo";
    public const string TrocarSenha = "nuclaud:trocar_senha";
    public const string PoliticaAdmin = "Administrador";

    public static ClaimsPrincipal Criar(Usuario u)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.NomeRede),
            new(ClaimTypes.GivenName, u.NomeCompleto),
            new(ClaimTypes.Role, u.Perfil == PerfilUsuario.Administrador ? "Administrador" : "Usuario"),
            new(Carimbo, u.CarimboSeguranca.ToString()),
        };
        if (u.DeveTrocarSenha) claims.Add(new Claim(TrocarSenha, "1"));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static (int Id, Guid Carimbo)? Ler(ClaimsPrincipal? p)
    {
        if (p?.Identity?.IsAuthenticated != true) return null;
        if (!int.TryParse(p.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        if (!Guid.TryParse(p.FindFirstValue(Carimbo), out var c)) return null;
        return (id, c);
    }
}

/// <summary>
/// Revalida o cookie: usuário inativado, perfil alterado ou senha redefinida encerram a sessão
/// (o carimbo de segurança muda). Resultado guardado em cache por 30 s para reduzir consultas.
/// </summary>
public sealed class EventosCookie : CookieAuthenticationEvents
{
    private readonly UsuarioServico _usuarios;
    private readonly IMemoryCache _cache;

    public EventosCookie(UsuarioServico usuarios, IMemoryCache cache)
    {
        _usuarios = usuarios;
        _cache = cache;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var dados = ClaimsNuclaud.Ler(context.Principal);
        if (dados == null) { await Rejeitar(context); return; }
        var chave = $"ator:{dados.Value.Id}:{dados.Value.Carimbo}";
        if (!_cache.TryGetValue(chave, out bool valido))
        {
            valido = await _usuarios.ObterAtorAsync(dados.Value.Id, dados.Value.Carimbo) != null;
            _cache.Set(chave, valido, TimeSpan.FromSeconds(30));
        }
        if (!valido) await Rejeitar(context);
    }

    private static async Task Rejeitar(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/pdf") || context.Request.Path.StartsWithSegments("/arquivos"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
        return base.RedirectToLogin(context);
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/pdf") || context.Request.Path.StartsWithSegments("/arquivos"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }
        return base.RedirectToAccessDenied(context);
    }
}

/// <summary>Revalida periodicamente o usuário dos circuitos Blazor abertos.</summary>
public sealed class RevalidacaoAutenticacao : RevalidatingServerAuthenticationStateProvider
{
    private readonly IServiceScopeFactory _escopos;

    public RevalidacaoAutenticacao(ILoggerFactory loggerFactory, IServiceScopeFactory escopos) : base(loggerFactory) => _escopos = escopos;

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(3);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState state, CancellationToken ct)
    {
        var dados = ClaimsNuclaud.Ler(state.User);
        if (dados == null) return false;
        await using var escopo = _escopos.CreateAsyncScope();
        var usuarios = escopo.ServiceProvider.GetRequiredService<UsuarioServico>();
        return await usuarios.ObterAtorAsync(dados.Value.Id, dados.Value.Carimbo) != null;
    }
}

/// <summary>Obtém o ator atual (sempre recarregado do banco) para as regras de negócio.</summary>
public sealed class AtorAtual
{
    private readonly AuthenticationStateProvider _estado;
    private readonly UsuarioServico _usuarios;

    public AtorAtual(AuthenticationStateProvider estado, UsuarioServico usuarios)
    {
        _estado = estado;
        _usuarios = usuarios;
    }

    public async Task<Ator> ObterAsync()
    {
        var st = await _estado.GetAuthenticationStateAsync();
        return await DeAsync(st.User, _usuarios);
    }

    public static async Task<Ator> DeAsync(ClaimsPrincipal principal, UsuarioServico usuarios)
    {
        var dados = ClaimsNuclaud.Ler(principal) ?? throw new AcessoNegadoException("Sessão expirada. Entre novamente.");
        return await usuarios.ObterAtorAsync(dados.Id, dados.Carimbo) ?? throw new AcessoNegadoException("Sessão expirada. Entre novamente.");
    }
}
