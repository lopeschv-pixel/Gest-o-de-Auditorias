using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Pdf;
using NuClaud.Core.Servicos;
using NuClaud.Web.Components;
using NuClaud.Web.Infra;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// Cultura pt-BR para toda a aplicação.
var ptBr = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = ptBr;
CultureInfo.DefaultThreadCurrentUICulture = ptBr;

// ---------------------------------------------------------------- Banco de dados
var conexao = cfg.GetConnectionString("NuClaud");
if (string.IsNullOrWhiteSpace(conexao))
    throw new InvalidOperationException(
        "Conexão com o banco não configurada. Defina ConnectionStrings__NuClaud (variável de ambiente), " +
        "user-secrets (desenvolvimento) ou appsettings.{Ambiente}.json fora do repositório. Ver docs/IMPLANTACAO.md.");
builder.Services.AddDbContextFactory<NuClaudDbContext>(o =>
{
    o.UseSqlServer(conexao, sql => sql.CommandTimeout(60));
    o.EnableSensitiveDataLogging(false);
});

// ---------------------------------------------------------------- Serviços de negócio
builder.Services.AddSingleton<IRelogio, RelogioSistema>();
builder.Services.AddScoped<UsuarioServico>();
builder.Services.AddScoped<CadastroServico>();
builder.Services.AddScoped<NormaServico>();
builder.Services.AddScoped<AuditorServico>();
builder.Services.AddScoped<ProgramacaoServico>();
builder.Services.AddScoped<PlanoServico>();
builder.Services.AddScoped<RelatorioServico>();
builder.Services.AddScoped<ConsultasServico>();
builder.Services.AddScoped<IdentidadeVisualServico>();
builder.Services.AddScoped<PdfServico>();
builder.Services.AddScoped<AtorAtual>();
builder.Services.AddScoped<NuClaud.Web.Components.Shared.ServicoNotificacao>();
builder.Services.AddScoped<NuClaud.Web.Components.Shared.ServicoConfirmacao>();
builder.Services.AddMemoryCache();

// ---------------------------------------------------------------- Autenticação local (preparada para AD/Entra ID — ver docs)
var chaves = cfg["DataProtection:CaminhoChaves"];
var dp = builder.Services.AddDataProtection().SetApplicationName("NuClaud");
if (!string.IsNullOrWhiteSpace(chaves)) dp.PersistKeysToFileSystem(new DirectoryInfo(chaves));

builder.Services.AddScoped<EventosCookie>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "NuClaud.Sessao";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = cfg.GetValue("Seguranca:CookieSomenteHttps", true) ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
        o.LoginPath = "/entrar";
        o.LogoutPath = "/conta/sair";
        o.AccessDeniedPath = "/acesso-negado";
        o.ExpireTimeSpan = TimeSpan.FromHours(cfg.GetValue("Seguranca:HorasSessao", 8));
        o.SlidingExpiration = true;
        o.EventsType = typeof(EventosCookie);
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ClaimsNuclaud.PoliticaAdmin, p => p.RequireRole("Administrador"));
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidacaoAutenticacao>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(o => o.DetailedErrors = builder.Environment.IsDevelopment());

if (cfg.GetValue("ProxyReverso:Habilitado", false))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        foreach (var ip in cfg.GetSection("ProxyReverso:ProxiesConhecidos").Get<string[]>() ?? Array.Empty<string>())
            o.KnownProxies.Add(System.Net.IPAddress.Parse(ip));
    });
}

var app = builder.Build();

await Inicializacao.ExecutarAsync(app);

if (cfg.GetValue("ProxyReverso:Habilitado", false)) app.UseForwardedHeaders();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    if (cfg.GetValue("Seguranca:UsarHsts", true)) app.UseHsts();
}
if (cfg.GetValue("Seguranca:RedirecionarHttps", false)) app.UseHttpsRedirection();
app.UseStatusCodePagesWithReExecute("/nao-encontrado", createScopeForStatusCodePages: true);
app.UseCabecalhosSeguranca();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(ptBr),
    SupportedCultures = new[] { ptBr },
    SupportedUICultures = new[] { ptBr }
});

app.UseAuthentication();
app.UseAuthorization();
app.UseTrocaDeSenhaObrigatoria();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapEndpointsSaude();
app.MapearEndpointsNuclaud();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }

internal static class Saude
{
    public static void MapEndpointsSaude(this WebApplication app) =>
        app.MapGet("/saude", async (IDbContextFactory<NuClaudDbContext> f) =>
        {
            await using var db = await f.CreateDbContextAsync();
            return await db.Database.CanConnectAsync() ? Results.Text("ok") : Results.StatusCode(503);
        }).AllowAnonymous();
}
