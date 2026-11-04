using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;
using SkiaSharp;

namespace NuClaud.Core.Servicos;

public sealed record Logotipo(byte[] Png, int Largura, int Altura, DateTime? AtualizadoEmUtc, string? AtualizadoPor, Guid Versao);

public class IdentidadeVisualServico
{
    public const int TamanhoMaximoBytes = 2 * 1024 * 1024;
    public const int LadoMaximoPixels = 1600;
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public IdentidadeVisualServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public async Task<Logotipo?> ObterLogoAsync()
    {
        await using var db = await _fabrica.CreateDbContextAsync();
        var c = await db.ConfiguracoesVisuais.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1);
        return c?.LogoPng == null ? null : new Logotipo(c.LogoPng, c.LogoLargura, c.LogoAltura, c.AtualizadoEmUtc, c.AtualizadoPor, c.Versao);
    }

    /// <summary>Identifica o formato pelos bytes iniciais (não confia na extensão nem no tipo informado pelo navegador).</summary>
    public static string? DetectarFormato(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P') return "image/webp";
        return null;
    }

    /// <summary>Valida, decodifica e normaliza a imagem em PNG (remove metadados e conteúdo não visual).</summary>
    public static (byte[] Png, int Largura, int Altura, string Tipo) Processar(byte[] dados)
    {
        if (dados.Length == 0) throw new RegraNegocioException("Selecione um arquivo de imagem.");
        if (dados.Length > TamanhoMaximoBytes) throw new RegraNegocioException("O arquivo deve ter no máximo 2 MB.");
        var tipo = DetectarFormato(dados) ?? throw new RegraNegocioException("Formato não aceito. Envie uma imagem PNG, JPEG ou WebP.");
        SKBitmap? lido;
        try { lido = SKBitmap.Decode(dados); }
        catch (Exception) { lido = null; }
        using var bmp = lido ?? throw new RegraNegocioException("Não foi possível ler a imagem. O arquivo pode estar corrompido.");
        if (bmp.Width < 16 || bmp.Height < 16) throw new RegraNegocioException("A imagem é pequena demais (mínimo de 16 × 16 pixels).");
        if (bmp.Width > 10000 || bmp.Height > 10000) throw new RegraNegocioException("A imagem é grande demais (máximo de 10.000 pixels por lado).");

        var escala = Math.Min(1.0, (double)LadoMaximoPixels / Math.Max(bmp.Width, bmp.Height));
        var w = Math.Max(1, (int)Math.Round(bmp.Width * escala));
        var h = Math.Max(1, (int)Math.Round(bmp.Height * escala));
        using var destino = new SKBitmap(new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Unpremul));
        using (var canvas = new SKCanvas(destino))
        {
            canvas.Clear(SKColors.Transparent);
            using var img = SKImage.FromBitmap(bmp);
            canvas.DrawImage(img, new SKRect(0, 0, w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        }
        using var png = SKImage.FromBitmap(destino).Encode(SKEncodedImageFormat.Png, 100);
        return (png.ToArray(), w, h, tipo);
    }

    public async Task SalvarLogoAsync(Ator ator, byte[] dados)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        var (png, w, h, tipo) = Processar(dados);
        await using var db = await _fabrica.CreateDbContextAsync();
        var c = await db.ConfiguracoesVisuais.FirstOrDefaultAsync(x => x.Id == 1);
        if (c == null) db.ConfiguracoesVisuais.Add(c = new ConfiguracaoVisual { Id = 1 });
        var agora = _relogio.UtcAgora;
        c.LogoOriginal = dados;
        c.LogoTipoConteudo = tipo;
        c.LogoPng = png;
        c.LogoLargura = w;
        c.LogoAltura = h;
        c.AtualizadoEmUtc = agora;
        c.AtualizadoPor = ator.Assinatura;
        c.Versao = Guid.NewGuid();
        Historico.Registrar(db, ator, agora, "IdentidadeVisual", 1, "Logotipo", $"Logotipo atualizado ({tipo}, {w}×{h} px).");
        await db.SaveChangesAsync();
    }

    public async Task RemoverLogoAsync(Ator ator)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        var c = await db.ConfiguracoesVisuais.FirstOrDefaultAsync(x => x.Id == 1);
        if (c?.LogoPng == null) return;
        c.LogoOriginal = null; c.LogoPng = null; c.LogoTipoConteudo = null; c.LogoLargura = c.LogoAltura = 0;
        c.AtualizadoEmUtc = _relogio.UtcAgora; c.AtualizadoPor = ator.Assinatura; c.Versao = Guid.NewGuid();
        Historico.Registrar(db, ator, _relogio.UtcAgora, "IdentidadeVisual", 1, "Logotipo", "Logotipo removido.");
        await db.SaveChangesAsync();
    }
}
