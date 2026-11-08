using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public sealed record LinhaPlanilha(int Linha, string Macroprocesso, string Processo);

public enum ResultadoLinha { NovoProcesso, NormaAdicionada, SemMudanca }

public sealed record PreviaLinha(int Linha, string Macroprocesso, string Processo, bool NovoMacroprocesso, ResultadoLinha Resultado);

public sealed record ResumoImportacao(int MacroprocessosNovos, int ProcessosNovos, int ProcessosAtualizados, int SemMudanca);

/// <summary>
/// Importa macroprocessos e processos auditáveis de uma planilha (.xlsx ou .csv) para uma norma.
/// Colunas "Macroprocesso" e "Processo" (títulos na primeira linha); sem títulos, A = macroprocesso e B = processo.
/// Macroprocesso em branco (ou célula mesclada) repete o valor de cima.
/// </summary>
public class ImportacaoProcessosServico
{
    public const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public ImportacaoProcessosServico(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    // ------------------------------------------------------------------ Leitura da planilha

    public static List<LinhaPlanilha> Ler(byte[] dados, string nomeArquivo)
    {
        if (dados.Length == 0) throw new RegraNegocioException("Selecione um arquivo.");
        if (dados.Length > TamanhoMaximoBytes) throw new RegraNegocioException("O arquivo deve ter no máximo 5 MB.");
        var ext = Path.GetExtension(nomeArquivo).ToLowerInvariant();
        List<string[]> linhas = ext switch
        {
            ".xlsx" or ".xlsm" => LerXlsx(dados),
            ".csv" or ".txt" => LerCsv(dados),
            ".xls" => throw new RegraNegocioException("O formato .xls (Excel 97-2003) não é aceito. No Excel, use Arquivo › Salvar como › Pasta de Trabalho do Excel (.xlsx)."),
            _ => throw new RegraNegocioException("Envie uma planilha do Excel (.xlsx) ou um arquivo CSV.")
        };
        return Interpretar(linhas);
    }

    private static List<string[]> LerXlsx(byte[] dados)
    {
        try
        {
            using var ms = new MemoryStream(dados);
            using var wb = new XLWorkbook(ms);
            var ws = wb.Worksheets.First();
            var usado = ws.RangeUsed();
            if (usado == null) return new();
            var ultimaColuna = Math.Min(usado.LastColumn().ColumnNumber(), 30);
            var resultado = new List<string[]>();
            foreach (var row in ws.Rows(1, usado.LastRow().RowNumber()))
            {
                var valores = new string[ultimaColuna];
                for (int c = 1; c <= ultimaColuna; c++)
                {
                    var cel = row.Cell(c);
                    // Célula mesclada: usa o valor da primeira célula da mesclagem.
                    var origem = cel.IsMerged() ? cel.MergedRange().FirstCell() : cel;
                    valores[c - 1] = origem.GetFormattedString().Trim();
                }
                resultado.Add(valores);
            }
            return resultado;
        }
        catch (RegraNegocioException) { throw; }
        catch (Exception) { throw new RegraNegocioException("Não foi possível ler a planilha. Confirme que o arquivo é um .xlsx válido."); }
    }

    private static List<string[]> LerCsv(byte[] dados)
    {
        var texto = Encoding.UTF8.GetString(dados);
        if (texto.Contains('�')) texto = Encoding.Latin1.GetString(dados); // CSV salvo pelo Excel em ANSI
        var linhas = texto.Replace("\r\n", "\n").Split('\n');
        var sep = linhas.FirstOrDefault(l => l.Length > 0)?.Count(c => c == ';') > 0 ? ';' : ',';
        return linhas.Select(l => l.Split(sep).Select(v => v.Trim().Trim('"').Trim()).ToArray()).ToList();
    }

    public static List<LinhaPlanilha> Interpretar(List<string[]> linhas)
    {
        int cab = -1, cm = 0, cp = 1;
        for (int i = 0; i < Math.Min(linhas.Count, 10) && cab < 0; i++)
        {
            var norm = linhas[i].Select(v => RegrasCalendario.SemAcento(v)).ToArray();
            var a = Array.FindIndex(norm, v => v.StartsWith("macro"));
            var b = Array.FindIndex(norm, v => v.Contains("processo") && !v.StartsWith("macro"));
            if (a >= 0 && b >= 0) { cab = i; cm = a; cp = b; }
        }
        var resultado = new List<LinhaPlanilha>();
        var vistos = new HashSet<string>();
        var macroAtual = "";
        for (int i = cab + 1; i < linhas.Count; i++)
        {
            string Val(int c) => c < linhas[i].Length ? linhas[i][c].Trim() : "";
            var m = Val(cm); var p = Val(cp);
            if (m.Length > 0) macroAtual = m;
            if (p.Length == 0 || macroAtual.Length == 0) continue;
            if (macroAtual.Length > 150 || p.Length > 150)
                throw new RegraNegocioException($"Linha {i + 1}: nomes de macroprocesso e processo devem ter até 150 caracteres.");
            if (vistos.Add(RegrasCalendario.SemAcento(macroAtual) + "|" + RegrasCalendario.SemAcento(p)))
                resultado.Add(new LinhaPlanilha(i + 1, macroAtual, p));
        }
        if (resultado.Count == 0)
            throw new RegraNegocioException("Nenhuma linha com macroprocesso e processo foi encontrada na primeira aba da planilha.");
        return resultado;
    }

    // ------------------------------------------------------------------ Prévia e importação

    private static string Chave(string s) => RegrasCalendario.SemAcento(s.Trim());

    public async Task<List<PreviaLinha>> PreverAsync(Ator ator, int normaId, IReadOnlyList<LinhaPlanilha> linhas)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        if (!await db.Normas.AnyAsync(n => n.Id == normaId)) throw new RegraNegocioException("Selecione a norma da planilha.");
        var macros = await db.Macroprocessos.AsNoTracking().ToListAsync();
        var processos = await db.Processos.AsNoTracking().Include(p => p.Normas).ToListAsync();
        return linhas.Select(l =>
        {
            var m = macros.FirstOrDefault(x => Chave(x.Nome) == Chave(l.Macroprocesso));
            var p = m == null ? null : processos.FirstOrDefault(x => x.MacroprocessoId == m.Id && Chave(x.Nome) == Chave(l.Processo));
            var r = p == null ? ResultadoLinha.NovoProcesso : p.Normas.Any(n => n.NormaId == normaId) ? ResultadoLinha.SemMudanca : ResultadoLinha.NormaAdicionada;
            return new PreviaLinha(l.Linha, m?.Nome ?? l.Macroprocesso, p?.Nome ?? l.Processo, m == null, r);
        }).ToList();
    }

    public async Task<ResumoImportacao> ImportarAsync(Ator ator, int normaId, IReadOnlyList<LinhaPlanilha> linhas, string nomeArquivo)
    {
        if (!ator.EhAdministrador) throw new AcessoNegadoException();
        await using var db = await _fabrica.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        var norma = await db.Normas.FirstOrDefaultAsync(n => n.Id == normaId) ?? throw new RegraNegocioException("Selecione a norma da planilha.");
        var agora = _relogio.UtcAgora;
        var macros = await db.Macroprocessos.ToListAsync();
        var processos = await db.Processos.Include(p => p.Normas).ToListAsync();
        int nm = 0, np = 0, na = 0, ns = 0;
        foreach (var l in linhas)
        {
            var m = macros.FirstOrDefault(x => Chave(x.Nome) == Chave(l.Macroprocesso));
            if (m == null)
            {
                m = new Macroprocesso { Nome = l.Macroprocesso.Trim(), Ativo = true };
                Historico.MarcarCriacao(m, ator, agora);
                db.Macroprocessos.Add(m); macros.Add(m); nm++;
            }
            var p = processos.FirstOrDefault(x => (x.Macroprocesso == m || x.MacroprocessoId == m.Id && m.Id != 0) && Chave(x.Nome) == Chave(l.Processo));
            if (p == null)
            {
                p = new Processo { Nome = l.Processo.Trim(), Macroprocesso = m, Ativo = true };
                Historico.MarcarCriacao(p, ator, agora);
                p.Normas.Add(new ProcessoNorma { NormaId = normaId });
                db.Processos.Add(p); processos.Add(p); np++;
            }
            else if (p.Normas.All(n => n.NormaId != normaId))
            {
                p.Normas.Add(new ProcessoNorma { NormaId = normaId });
                Historico.MarcarAlteracao(db, p, ator, agora, null);
                na++;
            }
            else ns++;
        }
        var resumo = new ResumoImportacao(nm, np, na, ns);
        Historico.Registrar(db, ator, agora, "Processo", "importacao", "Importação do Excel",
            $"Importação de \"{nomeArquivo}\" para {norma.Nome}: {nm} macroprocesso(s) e {np} processo(s) novos; {na} processo(s) passaram a ser auditáveis nesta norma; {ns} sem mudança.",
            new { arquivo = nomeArquivo, norma = norma.Nome, linhas = linhas.Select(x => $"{x.Macroprocesso} › {x.Processo}") });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return resumo;
    }
}
