using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using NuClaud.Core.Pdf;
using NuClaud.Core.Servicos;

namespace NuClaud.Web.Infra;

public static class Endpoints
{
    public static void MapearEndpointsNuclaud(this WebApplication app)
    {
        app.MapPost("/conta/sair", async (HttpContext ctx, [FromForm] string? origem) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/entrar");
        });

        app.MapGet("/arquivos/logo", async (IdentidadeVisualServico visual, HttpContext ctx) =>
        {
            var logo = await visual.ObterLogoAsync();
            if (logo == null) return Results.NotFound();
            ctx.Response.Headers.CacheControl = "private, max-age=60";
            return Results.File(logo.Png, "image/png");
        }).RequireAuthorization();

        var pdf = app.MapGroup("/pdf").RequireAuthorization();

        pdf.MapGet("/programa", (HttpContext ctx, PdfServico srv, UsuarioServico usu, int ano, string? normas, string? busca) =>
            Gerar(ctx, usu, async ator =>
            {
                var filtro = new FiltroCalendario
                {
                    Ano = ano,
                    Busca = busca,
                    NormaIds = (normas ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var n) ? n : -1).Where(n => n > 0).ToHashSet()
                };
                return await srv.ProgramaAsync(ator, filtro);
            }));

        pdf.MapGet("/plano/{id:int}", (HttpContext ctx, PdfServico srv, UsuarioServico usu, int id) =>
            Gerar(ctx, usu, ator => srv.PlanoAsync(ator, id)));

        pdf.MapGet("/relatorio/{id:int}", (HttpContext ctx, PdfServico srv, UsuarioServico usu, int id) =>
            Gerar(ctx, usu, ator => srv.RelatorioAsync(ator, id)));
    }

    private static async Task<IResult> Gerar(HttpContext ctx, UsuarioServico usuarios, Func<Ator, Task<ArquivoPdf>> gerar)
    {
        try
        {
            var ator = await AtorAtual.DeAsync(ctx.User, usuarios);
            var arq = await gerar(ator);
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.File(arq.Conteudo, "application/pdf", arq.NomeArquivo);
        }
        catch (AcessoNegadoException ex) { return Mensagem(StatusCodes.Status403Forbidden, ex.Message); }
        catch (NaoEncontradoException ex) { return Mensagem(StatusCodes.Status404NotFound, ex.Message); }
        catch (RegraNegocioException ex) { return Mensagem(StatusCodes.Status400BadRequest, ex.Message); }
    }

    private static IResult Mensagem(int status, string texto) =>
        Results.Content($"<!doctype html><html lang=\"pt-BR\"><meta charset=\"utf-8\"><title>NUCLAUD</title>" +
                        $"<body style=\"font-family:sans-serif;padding:2rem;font-size:18px\"><p>{System.Net.WebUtility.HtmlEncode(texto)}</p>" +
                        "<p><a href=\"javascript:history.back()\">Voltar</a></p></body></html>", "text/html; charset=utf-8", null, status);

    /// <summary>Força a troca da senha temporária antes de usar o sistema.</summary>
    public static IApplicationBuilder UseTrocaDeSenhaObrigatoria(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        if (ctx.User.Identity?.IsAuthenticated == true && ctx.User.HasClaim(ClaimsNuclaud.TrocarSenha, "1"))
        {
            var p = ctx.Request.Path;
            var permitido = p.StartsWithSegments("/alterar-senha") || p.StartsWithSegments("/conta/sair") || p.StartsWithSegments("/_framework")
                            || p.StartsWithSegments("/_content") || p.StartsWithSegments("/css") || p.StartsWithSegments("/fonts")
                            || p.StartsWithSegments("/img") || p.StartsWithSegments("/js") || Path.HasExtension(p.Value);
            if (!permitido)
            {
                ctx.Response.Redirect("/alterar-senha");
                return;
            }
        }
        await next();
    });

    public static IApplicationBuilder UseCabecalhosSeguranca(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["X-Frame-Options"] = "DENY";
        h["Referrer-Policy"] = "same-origin";
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        h["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self' data: blob:; style-src 'self' 'unsafe-inline'; script-src 'self'; " +
            "connect-src 'self' ws: wss:; font-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        await next();
    });
}
