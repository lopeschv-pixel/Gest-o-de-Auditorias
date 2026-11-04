using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Core.Pdf;

public static class PdfDocumentos
{
    private const double LarguraRetrato = 21.0 - 2 * PdfBase.Margem;
    private const double LarguraPaisagem = 29.7 - 2 * PdfBase.Margem;

    public static string TituloPrograma(int ano) => $"Programa de Auditorias Internas — {ano}";
    public static string TituloPlano => "Plano de Auditoria";
    public static string TituloRelatorio(TipoAuditoria t) =>
        t == TipoAuditoria.Interna ? "Relatório de Auditoria Interna" : "Relatório de Auditoria de Fornecedores";

    private static string Lista(IEnumerable<string> itens, string separador = ", ")
    {
        var l = itens.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        return l.Count == 0 ? "—" : string.Join(separador, l);
    }

    private static string Equipe(Auditoria a, FuncaoAuditoria f) =>
        Lista(a.Participantes.Where(p => p.Funcao == f).Select(p => p.NomeAuditor));

    private static IEnumerable<(string, string?)> Identificacao(Auditoria a)
    {
        yield return ("Código", a.Codigo);
        yield return ("Tipo", a.Tipo == TipoAuditoria.Interna ? "Auditoria interna" : "Auditoria de fornecedores");
        yield return ("Mês previsto", $"{Rotulos.NomeMes(a.MesPrevisto)} de {a.Ano}");
        yield return ("Normas", Lista(a.Normas.Select(n => string.IsNullOrWhiteSpace(n.EdicaoNorma) ? n.NomeNorma : $"{n.NomeNorma} — {n.EdicaoNorma}"), "\n"));
        if (a.Tipo == TipoAuditoria.Interna)
        {
            yield return ("Áreas", Lista(a.Areas.Select(x => x.NomeArea)));
            if (a.Macroprocessos.Count > 0) yield return ("Macroprocessos", Lista(a.Macroprocessos.Select(x => x.NomeMacroprocesso)));
            if (a.Processos.Count > 0) yield return ("Processos", Lista(a.Processos.Select(x => x.NomeProcesso)));
        }
        else
        {
            yield return ("Fornecedor", a.FornecedorNome);
            yield return ("CNPJ", a.FornecedorCnpj);
            yield return ("Endereço", a.FornecedorEndereco);
            yield return ("Escopo de fornecimento", a.EscopoFornecimento);
        }
    }

    private static void Periodos(Section s, Auditoria a)
    {
        PdfBase.Titulo1(s, "Datas e horários");
        var periodos = a.Plano?.Periodos.OrderBy(p => p.Data).ThenBy(p => p.HoraInicio).ToList() ?? new();
        if (periodos.Count == 0) { PdfBase.Texto(s, "Datas e horários ainda não definidos."); return; }
        var t = PdfBase.TabelaComCabecalho(s, ("Data", 3.4), ("Dia da semana", 4.0), ("Horário inicial", 4.0), ("Horário final", LarguraRetrato - 11.4));
        foreach (var p in periodos)
        {
            var r = t.AddRow();
            r.Cells[0].AddParagraph(Rotulos.Data(p.Data));
            r.Cells[1].AddParagraph(Rotulos.DiaSemana(p.Data));
            r.Cells[2].AddParagraph(Rotulos.Hora(p.HoraInicio));
            r.Cells[3].AddParagraph(Rotulos.Hora(p.HoraFim));
        }
        s.AddParagraph().Format.SpaceAfter = 2;
    }

    private static void EquipeSecao(Section s, Auditoria a)
    {
        PdfBase.Titulo1(s, "Equipe auditora");
        var campos = new List<(string, string?)>
        {
            ("Auditor líder", Equipe(a, FuncaoAuditoria.Lider)),
            ("Auditores", Equipe(a, FuncaoAuditoria.Auditor)),
        };
        if (a.Participantes.Any(p => p.Funcao == FuncaoAuditoria.Observador)) campos.Add(("Observadores", Equipe(a, FuncaoAuditoria.Observador)));
        PdfBase.Campos(s, LarguraRetrato, campos);
    }

    // ------------------------------------------------------------------ Programa anual

    public static byte[] Programa(int ano, IReadOnlyList<Auditoria> auditorias, string descricaoFiltro, LogoPdf? logo)
    {
        var titulo = TituloPrograma(ano);
        var doc = PdfBase.CriarDocumento(titulo);
        var s = PdfBase.CriarSecao(doc, titulo, null, logo, paisagem: true);
        var pf = s.AddParagraph(descricaoFiltro, "Pequeno");
        pf.Format.SpaceAfter = Unit.FromPoint(6);

        if (auditorias.Count == 0)
        {
            var p = s.AddParagraph("Nenhuma auditoria encontrada para os filtros selecionados.");
            p.Format.SpaceBefore = Unit.FromCentimeter(1);
            p.Format.Alignment = ParagraphAlignment.Center;
            p.Format.Font.Size = 12;
            return PdfBase.Renderizar(doc, assinatura: false);
        }

        var t = PdfBase.TabelaComCabecalho(s,
            ("Mês previsto", 2.2), ("Código", 2.9), ("Área / Fornecedor", 3.9), ("Macroprocessos / Processos", 3.9),
            ("Normas", 2.9), ("Auditores", 4.3), ("Requisitos", LarguraPaisagem - 2.2 - 2.9 - 3.9 - 3.9 - 2.9 - 4.3 - 2.1), ("Situação", 2.1));
        foreach (var a in auditorias)
        {
            var r = t.AddRow();
            r.Cells[0].AddParagraph(Rotulos.NomeMes(a.MesPrevisto));
            var pc = r.Cells[1].AddParagraph(a.Codigo); pc.Format.Font.Bold = true;
            r.Cells[2].AddParagraph(a.Tipo == TipoAuditoria.Interna ? Lista(a.Areas.Select(x => x.NomeArea)) : $"Fornecedor: {a.FornecedorNome}");
            r.Cells[3].AddParagraph(a.Tipo == TipoAuditoria.Interna && (a.Macroprocessos.Count > 0 || a.Processos.Count > 0)
                ? string.Join("\n", a.Macroprocessos.Select(m =>
                {
                    var procs = a.Processos.Where(p => p.NomeMacroprocesso == m.NomeMacroprocesso).Select(p => p.NomeProcesso).ToList();
                    return procs.Count == 0 ? m.NomeMacroprocesso : $"{m.NomeMacroprocesso}: {string.Join(", ", procs)}";
                }))
                : "—");
            r.Cells[4].AddParagraph(Lista(a.Normas.Select(n => n.NomeNorma), "\n"));
            var eq = r.Cells[5].AddParagraph();
            void Funcao(string rotulo, FuncaoAuditoria f)
            {
                var nomes = a.Participantes.Where(p => p.Funcao == f).Select(p => p.NomeAuditor).ToList();
                if (nomes.Count == 0) return;
                if (eq.Elements.Count > 0) eq.AddLineBreak();
                eq.AddFormattedText(rotulo + ": ", TextFormat.Bold);
                eq.AddText(string.Join(", ", nomes));
            }
            Funcao("Líder", FuncaoAuditoria.Lider);
            Funcao("Auditores", FuncaoAuditoria.Auditor);
            Funcao("Observadores", FuncaoAuditoria.Observador);
            var pr = r.Cells[6].AddParagraph();
            var grupos = a.Requisitos.GroupBy(x => x.NomeNorma).OrderBy(g => g.Key).ToList();
            if (grupos.Count == 0) pr.AddText("—");
            foreach (var g in grupos)
            {
                if (pr.Elements.Count > 0) pr.AddLineBreak();
                pr.AddFormattedText(g.Key + ": ", TextFormat.Bold);
                pr.AddText(string.Join(", ", g.OrderBy(x => x.ChaveOrdenacao, StringComparer.Ordinal).Select(x => x.Codigo)));
            }
            r.Cells[7].AddParagraph(a.Situacao.Texto());
        }
        var total = s.AddParagraph($"Total: {auditorias.Count} auditoria(s).", "Pequeno");
        total.Format.SpaceBefore = Unit.FromPoint(6);
        return PdfBase.Renderizar(doc, assinatura: false);
    }

    // ------------------------------------------------------------------ Plano

    public static byte[] Plano(Auditoria a, LogoPdf? logo)
    {
        var doc = PdfBase.CriarDocumento($"{TituloPlano} — {a.Codigo}");
        var s = PdfBase.CriarSecao(doc, TituloPlano, a.Codigo, logo, paisagem: false);

        Periodos(s, a);

        PdfBase.Titulo1(s, "Identificação");
        PdfBase.Campos(s, LarguraRetrato, Identificacao(a));

        PdfBase.Titulo1(s, "Local e contato");
        PdfBase.Campos(s, LarguraRetrato, new (string, string?)[]
        {
            ("Local", a.Plano?.Local), ("Nome do gestor", a.Plano?.GestorNome), ("E-mail do gestor", a.Plano?.GestorEmail)
        });

        if (a.Tipo == TipoAuditoria.Fornecedor)
        {
            PdfBase.Titulo1(s, "Objetivo e escopo");
            PdfBase.Campos(s, LarguraRetrato, new (string, string?)[]
            {
                ("Objetivo", a.Plano?.Objetivo), ("Escopo", a.Plano?.Escopo), ("Escopo de fornecimento", a.EscopoFornecimento)
            });
        }

        EquipeSecao(s, a);
        RequisitosTabela(s, a, null);
        return PdfBase.Renderizar(doc, assinatura: true);
    }

    private static void RequisitosTabela(Section s, Auditoria a, Dictionary<int, string>? classificacao)
    {
        PdfBase.Titulo1(s, classificacao == null ? "Requisitos" : "Requisitos auditados");
        if (a.Requisitos.Count == 0) { PdfBase.Texto(s, "Nenhum requisito definido."); return; }
        var colunas = classificacao == null
            ? new[] { ("Norma", 3.6), ("Requisito", 2.0), ("Título", LarguraRetrato - 5.6) }
            : new[] { ("Norma", 3.4), ("Requisito", 1.9), ("Título", LarguraRetrato - 5.3 - 3.6), ("Classificação", 3.6) };
        var t = PdfBase.TabelaComCabecalho(s, colunas);
        foreach (var r in a.Requisitos.OrderBy(x => x.NomeNorma).ThenBy(x => x.ChaveOrdenacao, StringComparer.Ordinal))
        {
            var row = t.AddRow();
            row.Cells[0].AddParagraph(r.NomeNorma);
            row.Cells[1].AddParagraph(r.Codigo);
            row.Cells[2].AddParagraph(r.Titulo);
            if (classificacao != null) row.Cells[3].AddParagraph(classificacao.TryGetValue(r.Id, out var c) ? c : "Não classificado");
        }
        s.AddParagraph().Format.SpaceAfter = 2;
    }

    // ------------------------------------------------------------------ Relatório

    public static byte[] Relatorio(Auditoria a, LogoPdf? logo)
    {
        var rel = a.Relatorio ?? throw new InvalidOperationException("Relatório inexistente.");
        var titulo = TituloRelatorio(a.Tipo);
        var doc = PdfBase.CriarDocumento($"{titulo} — {a.Codigo}");
        var s = PdfBase.CriarSecao(doc, titulo, a.Codigo, logo, paisagem: false);

        var situacao = rel.Situacao switch
        {
            SituacaoRelatorio.Concluido => $"Relatório concluído em {FusoHorario.Formatar(rel.ConcluidoEmUtc)}{(rel.NumeroRevisao > 0 ? $" (revisão {rel.NumeroRevisao})" : "")}.",
            SituacaoRelatorio.EmRevisao => $"Relatório em revisão (revisão {rel.NumeroRevisao}) — documento não concluído.",
            _ => "Relatório em elaboração — documento não concluído."
        };
        var ps = s.AddParagraph(situacao, "Pequeno");
        ps.Format.SpaceAfter = Unit.FromPoint(4);

        PdfBase.Titulo1(s, "Identificação");
        PdfBase.Campos(s, LarguraRetrato, Identificacao(a));
        Periodos(s, a);
        EquipeSecao(s, a);

        var porReq = new Dictionary<int, List<string>>();
        foreach (var c in rel.Constatacoes.Where(c => RegrasRelatorio.UsaRequisitos(c.Classificacao)))
            foreach (var cr in c.Requisitos)
            {
                if (!porReq.TryGetValue(cr.AuditoriaRequisitoId, out var l)) porReq[cr.AuditoriaRequisitoId] = l = new();
                var txt = c.Classificacao.Texto();
                if (!l.Contains(txt)) l.Add(txt);
            }
        RequisitosTabela(s, a, porReq.ToDictionary(k => k.Key, v => string.Join(" / ", v.Value)));

        PdfBase.Titulo1(s, "Constatações");
        var reqs = a.Requisitos.ToDictionary(r => r.Id);
        if (rel.Constatacoes.Count == 0) PdfBase.Texto(s, "Nenhuma constatação registrada.");
        foreach (var c in rel.Constatacoes.OrderBy(c => c.Numero))
        {
            var h = s.AddParagraph("", StyleNames.Heading2);
            h.AddText($"Constatação {Rotulos.NumeroConstatacao(c.Numero)} · {c.Classificacao.Texto()}");
            h.Format.Shading.Color = PdfBase.FundoCabecalho;
            h.Format.Borders.Left.Width = 3; h.Format.Borders.Left.Color = PdfBase.Destaque;
            h.Format.Borders.DistanceFromLeft = Unit.FromPoint(5);
            h.Format.LeftIndent = Unit.FromPoint(6);

            void Campo(string rotulo, string? valor)
            {
                s.AddParagraph(rotulo, "Rotulo").Format.SpaceBefore = Unit.FromPoint(3);
                var p = PdfBase.Texto(s, valor);
                p.Format.LeftIndent = Unit.FromPoint(0);
            }
            if (RegrasRelatorio.UsaRequisitos(c.Classificacao))
                Campo("Requisitos", string.Join("\n", c.Requisitos.Select(r => reqs.TryGetValue(r.AuditoriaRequisitoId, out var ar) ? ar : null)
                    .Where(r => r != null).OrderBy(r => r!.NomeNorma).ThenBy(r => r!.ChaveOrdenacao, StringComparer.Ordinal)
                    .Select(r => $"{r!.NomeNorma} — {OrdenacaoRequisitos.Exibicao(r.Codigo, r.Titulo)}")));
            switch (c.Classificacao)
            {
                case ClassificacaoConstatacao.Conformidade:
                    Campo("Evidência encontrada", c.Evidencia);
                    break;
                case ClassificacaoConstatacao.NaoConformidade:
                    Campo("Descrição da não conformidade", c.DescricaoNaoConformidade);
                    Campo("Evidências objetivas", c.EvidenciasObjetivas);
                    Campo("Número do PAC", c.NumeroPac);
                    break;
                case ClassificacaoConstatacao.Observacao:
                    Campo("Observação", c.Observacao);
                    break;
                default:
                    Campo("Oportunidade de melhoria", c.OportunidadeMelhoria);
                    break;
            }
        }

        if (a.Tipo == TipoAuditoria.Fornecedor)
        {
            PdfBase.Titulo1(s, "Conclusão");
            PdfBase.Campos(s, LarguraRetrato, new (string, string?)[]
            {
                ("Qualificação", rel.Qualificacao?.Texto()),
                ("Observações", rel.ObservacoesConclusao),
                ("Número do RQF", rel.NumeroRqf)
            });
        }
        return PdfBase.Renderizar(doc, assinatura: true);
    }
}
