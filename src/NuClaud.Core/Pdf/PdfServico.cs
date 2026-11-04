using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Core.Pdf;

public sealed record ArquivoPdf(string NomeArquivo, byte[] Conteudo);

/// <summary>Geração de PDFs sempre com verificação de permissão no servidor.</summary>
public class PdfServico
{
    private readonly ProgramacaoServico _programacao;
    private readonly ConsultasServico _consultas;
    private readonly IdentidadeVisualServico _visual;
    private readonly NormaServico _normas;

    public PdfServico(ProgramacaoServico programacao, ConsultasServico consultas, IdentidadeVisualServico visual, NormaServico normas)
    {
        _programacao = programacao;
        _consultas = consultas;
        _visual = visual;
        _normas = normas;
    }

    private async Task<LogoPdf?> LogoAsync()
    {
        var l = await _visual.ObterLogoAsync();
        return l == null ? null : new LogoPdf(l.Png, l.Largura, l.Altura);
    }

    public async Task<ArquivoPdf> ProgramaAsync(Ator ator, FiltroCalendario filtro)
    {
        var lista = await _consultas.CalendarioAsync(ator, filtro);
        var normas = await _normas.ListarAsync();
        var nomesNormas = filtro.NormaIds.Count == 0 ? "Todas"
            : string.Join(", ", normas.Where(n => filtro.NormaIds.Contains(n.Id)).Select(n => n.Nome));
        var desc = $"Normas: {nomesNormas}" + (string.IsNullOrWhiteSpace(filtro.Busca) ? "" : $" · Busca: \"{filtro.Busca.Trim()}\"") +
                   (ator.EhAdministrador ? "" : " · Somente auditorias em que você participa");
        var pdf = PdfDocumentos.Programa(filtro.Ano, lista, desc, await LogoAsync());
        return new ArquivoPdf($"Programa-de-Auditorias-{filtro.Ano}.pdf", pdf);
    }

    public async Task<ArquivoPdf> PlanoAsync(Ator ator, int auditoriaId)
    {
        var a = await _programacao.ObterDetalheAsync(ator, auditoriaId);
        if (a.Plano == null) throw new RegraNegocioException("O plano desta auditoria ainda não foi salvo.");
        return new ArquivoPdf($"Plano-{a.Codigo}.pdf", PdfDocumentos.Plano(a, await LogoAsync()));
    }

    public async Task<ArquivoPdf> RelatorioAsync(Ator ator, int auditoriaId)
    {
        var a = await _programacao.ObterDetalheAsync(ator, auditoriaId);
        if (a.Relatorio == null) throw new RegraNegocioException("O relatório desta auditoria ainda não foi gerado.");
        return new ArquivoPdf($"Relatorio-{a.Codigo}.pdf", PdfDocumentos.Relatorio(a, await LogoAsync()));
    }
}
