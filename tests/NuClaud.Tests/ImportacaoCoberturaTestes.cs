using ClosedXML.Excel;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Tests;

public class ImportacaoTestes
{
    private static byte[] Planilha(params string[][] linhas)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Processos");
        for (int i = 0; i < linhas.Length; i++)
            for (int j = 0; j < linhas[i].Length; j++) ws.Cell(i + 1, j + 1).Value = linhas[i][j];
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Le_xlsx_com_titulos_mesclagem_e_macro_em_branco()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("P");
        ws.Cell(1, 1).Value = "Código"; ws.Cell(1, 2).Value = "Macroprocesso"; ws.Cell(1, 3).Value = "Processo";
        ws.Cell(2, 2).Value = "Suprimentos"; ws.Cell(2, 3).Value = "Compras";
        ws.Cell(3, 3).Value = "Gestão de fornecedores";             // macro mesclado
        ws.Range("B2:B3").Merge();
        ws.Cell(4, 2).Value = "Produção"; ws.Cell(4, 3).Value = "Soldagem";
        ws.Cell(5, 3).Value = "Usinagem";                            // macro em branco repete o de cima
        ws.Cell(6, 2).Value = "Produção"; ws.Cell(6, 3).Value = "soldagem"; // duplicado (sem diferenciar maiúsculas)
        using var ms = new MemoryStream(); wb.SaveAs(ms);
        var l = ImportacaoProcessosServico.Ler(ms.ToArray(), "processos.xlsx");
        Assert.Equal(new[] { "Suprimentos|Compras", "Suprimentos|Gestão de fornecedores", "Produção|Soldagem", "Produção|Usinagem" },
            l.Select(x => x.Macroprocesso + "|" + x.Processo));
    }

    [Fact]
    public void Le_sem_titulos_e_csv_e_rejeita_formatos_invalidos()
    {
        var l = ImportacaoProcessosServico.Ler(Planilha(new[] { "Engenharia", "Projeto" }, new[] { "", "Alterações de projeto" }), "x.xlsx");
        Assert.Equal(2, l.Count);
        var csv = System.Text.Encoding.UTF8.GetBytes("Macroprocesso;Processo\nGestão;Controle de documentos\n;Auditoria interna\n");
        Assert.Equal(new[] { "Gestão|Controle de documentos", "Gestão|Auditoria interna" },
            ImportacaoProcessosServico.Ler(csv, "p.csv").Select(x => x.Macroprocesso + "|" + x.Processo));
        Assert.Throws<RegraNegocioException>(() => ImportacaoProcessosServico.Ler(new byte[] { 1, 2, 3 }, "antigo.xls"));
        Assert.Throws<RegraNegocioException>(() => ImportacaoProcessosServico.Ler(new byte[] { 1, 2, 3 }, "x.xlsx"));
        Assert.Throws<RegraNegocioException>(() => ImportacaoProcessosServico.Ler(Planilha(new[] { "Macroprocesso", "Processo" }), "vazia.xlsx"));
    }

    [Fact]
    public async Task Importa_para_a_norma_criando_e_atualizando_sem_duplicar()
    {
        using var amb = new Ambiente();
        var srv = new ImportacaoProcessosServico(amb.Fabrica, amb.Relogio);
        var linhas = ImportacaoProcessosServico.Ler(Planilha(
            new[] { "Macroprocesso", "Processo" },
            new[] { "Suprimentos", "Recebimento" },      // existe com ISO 9001
            new[] { "Suprimentos", "Compras" },          // existe com ISO 9001 e 14001
            new[] { "Engenharia", "Projeto" }), "p.xlsx");
        var previa = await srv.PreverAsync(amb.Admin, amb.Iso14001, linhas);
        Assert.Equal(new[] { ResultadoLinha.NormaAdicionada, ResultadoLinha.SemMudanca, ResultadoLinha.NovoProcesso }, previa.Select(x => x.Resultado));
        Assert.True(previa[2].NovoMacroprocesso);

        var usuario = await amb.UsuarioVinculado("comum", amb.AudAuditor);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => srv.ImportarAsync(usuario, amb.Iso14001, linhas, "p.xlsx"));

        var r = await srv.ImportarAsync(amb.Admin, amb.Iso14001, linhas, "p.xlsx");
        Assert.Equal(new ResumoImportacao(1, 1, 1, 1), r);
        var processos = await amb.Cadastro.ListarProcessosAsync();
        Assert.Contains(processos.Single(p => p.Nome == "Recebimento").Normas, n => n.NormaId == amb.Iso14001);
        Assert.Equal("Engenharia", processos.Single(p => p.Nome == "Projeto").Macroprocesso!.Nome);
        // Importar de novo não duplica nada.
        Assert.Equal(new ResumoImportacao(0, 0, 0, 3), await srv.ImportarAsync(amb.Admin, amb.Iso14001, linhas, "p.xlsx"));
    }
}

public class CoberturaTestes
{
    [Fact]
    public async Task Cobertura_de_processos_e_requisitos_por_periodo()
    {
        using var amb = new Ambiente();
        // 2027: Recebimento com 8.4 e 8.6 (concluída). 2028: Compras com 8.4 (programada).
        var id = await amb.ComPlano(amb.Interna());
        await amb.Relatorios.IniciarAsync(amb.Admin, id);
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        var dto = RelatorioServico.ParaEdicao(a);
        dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade, AuditoriaRequisitoIds = a.Requisitos.Select(r => r.Id).ToHashSet(), Evidencia = "ok" });
        Assert.True((await amb.Relatorios.ConcluirAsync(amb.Admin, dto)).Concluido);
        var p2 = amb.Interna(2028);
        p2.ProcessoIds = new() { amb.ProcCompras };
        p2.Itens = new() { new(amb.ProcCompras, amb.Req(amb.Iso9001, "8.4")) };
        await amb.Programacao.SalvarAsync(amb.Admin, p2);

        var c27 = await amb.Consultas.CoberturaAsync(amb.Admin, amb.Iso9001, 2027, 2027);
        Assert.Equal(SituacaoCobertura.Realizado, c27.Processos.Single(x => x.Processo == "Recebimento").Situacao);
        Assert.Equal(2, c27.Processos.Single(x => x.Processo == "Recebimento").RequisitosAuditados);
        Assert.Equal(SituacaoCobertura.NaoPlanejado, c27.Processos.Single(x => x.Processo == "Compras").Situacao);
        Assert.Equal(SituacaoCobertura.Realizado, c27.Requisitos.Single(x => x.Codigo == "8.4").Situacao);
        Assert.Equal(SituacaoCobertura.NaoPlanejado, c27.Requisitos.Single(x => x.Codigo == "4.1").Situacao);
        var (r, p, n) = CoberturaNorma.Contar(c27.Requisitos, x => x.Situacao);
        Assert.Equal((2, 0, 64), (r, p, n));

        var ciclo = await amb.Consultas.CoberturaAsync(amb.Admin, amb.Iso9001, 2027, 2028);
        Assert.Equal(SituacaoCobertura.Programado, ciclo.Processos.Single(x => x.Processo == "Compras").Situacao);
        Assert.Equal(2, ciclo.Requisitos.Single(x => x.Codigo == "8.4").Onde.Count); // 8.4 em Recebimento (2027) e Compras (2028)

        var usuario = await amb.UsuarioVinculado("comum", amb.AudAuditor);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Consultas.CoberturaAsync(usuario, amb.Iso9001, 2027, 2027));
    }
}
