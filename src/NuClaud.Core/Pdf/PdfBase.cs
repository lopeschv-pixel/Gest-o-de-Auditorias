using System.Reflection;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using NuClaud.Core.Servicos;

namespace NuClaud.Core.Pdf;

/// <summary>Resolve as fontes embutidas no assembly (Carlito, licença OFL). Não depende de fontes do sistema nem da internet.</summary>
internal sealed class ResolvedorFontes : IFontResolver
{
    public const string Familia = "Carlito";
    private static readonly Dictionary<string, byte[]> Cache = new();

    public FontResolverInfo? ResolveTypeface(string familyName, bool bold, bool italic)
    {
        var face = (bold, italic) switch
        {
            (true, true) => "Carlito-BoldItalic",
            (true, false) => "Carlito-Bold",
            (false, true) => "Carlito-Italic",
            _ => "Carlito-Regular"
        };
        return new FontResolverInfo(face);
    }

    public byte[]? GetFont(string faceName)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(faceName, out var b)) return b;
            var asm = Assembly.GetExecutingAssembly();
            var nome = asm.GetManifestResourceNames().First(n => n.EndsWith(faceName + ".ttf", StringComparison.Ordinal));
            using var s = asm.GetManifestResourceStream(nome)!;
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return Cache[faceName] = ms.ToArray();
        }
    }
}

public sealed record LogoPdf(byte[] Png, int Largura, int Altura);

internal static class PdfBase
{
    private static readonly object Trava = new();
    private static bool _iniciado;

    public static readonly Color Tinta = new(0x1F, 0x2A, 0x33);
    public static readonly Color Destaque = new(0x0B, 0x4F, 0x6C);
    public static readonly Color Cinza = new(0x5F, 0x6B, 0x75);
    public static readonly Color Linha = new(0xC9, 0xD3, 0xDA);
    public static readonly Color FundoCabecalho = new(0xE8, 0xF0, 0xF4);

    public const double Margem = 1.8; // cm
    public const double MargemSuperior = 3.4;
    public const double MargemInferior = 2.0;

    public static void Iniciar()
    {
        lock (Trava)
        {
            if (_iniciado) return;
            if (GlobalFontSettings.FontResolver == null) GlobalFontSettings.FontResolver = new ResolvedorFontes();
            _iniciado = true;
        }
    }

    public static Document CriarDocumento(string titulo)
    {
        Iniciar();
        var doc = new Document();
        doc.Info.Title = titulo;
        doc.Info.Author = "";
        doc.Info.Subject = titulo;
        var n = doc.Styles[StyleNames.Normal]!;
        n.Font.Name = ResolvedorFontes.Familia;
        n.Font.Size = 10.5;
        n.Font.Color = Tinta;
        n.ParagraphFormat.SpaceAfter = Unit.FromPoint(2);
        n.ParagraphFormat.LineSpacingRule = MigraDoc.DocumentObjectModel.LineSpacingRule.Multiple;
        n.ParagraphFormat.LineSpacing = 1.1;

        var h1 = doc.Styles[StyleNames.Heading1]!;
        h1.Font.Size = 13; h1.Font.Bold = true; h1.Font.Color = Destaque;
        h1.ParagraphFormat.SpaceBefore = Unit.FromPoint(12); h1.ParagraphFormat.SpaceAfter = Unit.FromPoint(5);
        h1.ParagraphFormat.KeepWithNext = true;
        h1.ParagraphFormat.Borders.Bottom.Width = 0.75; h1.ParagraphFormat.Borders.Bottom.Color = Linha;
        h1.ParagraphFormat.Borders.DistanceFromBottom = Unit.FromPoint(2);

        var h2 = doc.Styles[StyleNames.Heading2]!;
        h2.Font.Size = 11.5; h2.Font.Bold = true; h2.Font.Color = Tinta;
        h2.ParagraphFormat.SpaceBefore = Unit.FromPoint(9); h2.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);
        h2.ParagraphFormat.KeepWithNext = true;

        var rotulo = doc.Styles.AddStyle("Rotulo", StyleNames.Normal);
        rotulo.Font.Bold = true; rotulo.Font.Size = 9.5; rotulo.Font.Color = Cinza;
        rotulo.ParagraphFormat.SpaceAfter = 0; rotulo.ParagraphFormat.KeepWithNext = true;

        var pequeno = doc.Styles.AddStyle("Pequeno", StyleNames.Normal);
        pequeno.Font.Size = 9; pequeno.Font.Color = Cinza;

        var tabela = doc.Styles.AddStyle("Tabela", StyleNames.Normal);
        tabela.Font.Size = 9.5;
        tabela.ParagraphFormat.SpaceAfter = 0;
        return doc;
    }

    public static Section CriarSecao(Document doc, string titulo, string? identificacao, LogoPdf? logo, bool paisagem)
    {
        var s = doc.AddSection();
        var ps = s.PageSetup;
        ps.PageFormat = PageFormat.A4;
        ps.Orientation = paisagem ? Orientation.Landscape : Orientation.Portrait;
        ps.TopMargin = Unit.FromCentimeter(MargemSuperior);
        ps.BottomMargin = Unit.FromCentimeter(MargemInferior);
        ps.LeftMargin = ps.RightMargin = Unit.FromCentimeter(Margem);
        ps.HeaderDistance = Unit.FromCentimeter(1.0);
        ps.FooterDistance = Unit.FromCentimeter(0.9);

        var larguraUtil = (paisagem ? 29.7 : 21.0) - 2 * Margem;

        // Cabeçalho repetido em todas as páginas: logotipo (proporção preservada) + título e identificação.
        var cab = s.Headers.Primary.AddTable();
        cab.Borders.Visible = false;
        cab.AddColumn(Unit.FromCentimeter(5.0));
        cab.AddColumn(Unit.FromCentimeter(larguraUtil - 5.0));
        var row = cab.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;
        row.Height = Unit.FromCentimeter(1.6);
        if (logo != null && logo.Largura > 0 && logo.Altura > 0)
        {
            const double maxW = 4.6, maxH = 1.5; // cm
            var escala = Math.Min(maxW / logo.Largura, maxH / logo.Altura);
            var img = row.Cells[0].AddParagraph().AddImage("base64:" + Convert.ToBase64String(logo.Png));
            img.Width = Unit.FromCentimeter(logo.Largura * escala);
            img.Height = Unit.FromCentimeter(logo.Altura * escala);
            img.LockAspectRatio = true;
        }
        var pt = row.Cells[1].AddParagraph();
        pt.Format.Alignment = ParagraphAlignment.Right;
        pt.Format.SpaceAfter = 0;
        var ft = pt.AddFormattedText(titulo);
        ft.Font.Size = 14; ft.Font.Bold = true; ft.Font.Color = Destaque;
        if (!string.IsNullOrWhiteSpace(identificacao))
        {
            var pi = row.Cells[1].AddParagraph(identificacao);
            pi.Format.Alignment = ParagraphAlignment.Right;
            pi.Format.Font.Size = 10; pi.Format.Font.Color = Cinza; pi.Format.SpaceAfter = 0;
        }
        var linha = s.Headers.Primary.AddParagraph();
        linha.Format.SpaceBefore = Unit.FromPoint(4);
        linha.Format.Borders.Bottom.Width = 1.2;
        linha.Format.Borders.Bottom.Color = Destaque;

        // Rodapé: numeração de páginas (sem nome de sistema, servidor ou endereço).
        var rod = s.Footers.Primary.AddParagraph();
        rod.Format.Font.Size = 8.5; rod.Format.Font.Color = Cinza;
        rod.Format.Borders.Top.Width = 0.5; rod.Format.Borders.Top.Color = Linha;
        rod.Format.Borders.DistanceFromTop = Unit.FromPoint(3);
        rod.Format.AddTabStop(Unit.FromCentimeter(larguraUtil), TabAlignment.Right);
        rod.AddText(identificacao ?? "");
        rod.AddTab();
        rod.AddText("Página ");
        rod.AddPageField();
        rod.AddText(" de ");
        rod.AddNumPagesField();
        return s;
    }

    public static void Titulo1(Section s, string texto) => s.AddParagraph(texto, StyleNames.Heading1);
    public static void Titulo2(Section s, string texto) => s.AddParagraph(texto, StyleNames.Heading2);

    public static Paragraph Texto(Section s, string? texto, string? vazio = "—")
    {
        var p = s.AddParagraph();
        AdicionarTexto(p, string.IsNullOrWhiteSpace(texto) ? vazio ?? "" : texto);
        return p;
    }

    /// <summary>Preserva quebras de linha do texto digitado.</summary>
    public static void AdicionarTexto(Paragraph p, string texto)
    {
        var linhas = texto.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < linhas.Length; i++)
        {
            if (i > 0) p.AddLineBreak();
            p.AddText(linhas[i]);
        }
    }

    /// <summary>Tabela de pares rótulo/valor em duas colunas.</summary>
    public static void Campos(Section s, double larguraUtil, IEnumerable<(string Rotulo, string? Valor)> campos)
    {
        var t = s.AddTable();
        t.Style = "Tabela";
        t.Borders.Width = 0.5; t.Borders.Color = Linha;
        t.LeftPadding = t.RightPadding = Unit.FromPoint(5);
        t.TopPadding = t.BottomPadding = Unit.FromPoint(3);
        t.AddColumn(Unit.FromCentimeter(4.4));
        t.AddColumn(Unit.FromCentimeter(larguraUtil - 4.4));
        foreach (var (rotulo, valor) in campos)
        {
            var r = t.AddRow();
            r.Cells[0].Shading.Color = FundoCabecalho;
            var pr = r.Cells[0].AddParagraph(rotulo);
            pr.Format.Font.Bold = true;
            AdicionarTexto(r.Cells[1].AddParagraph(), string.IsNullOrWhiteSpace(valor) ? "—" : valor);
        }
        s.AddParagraph().Format.SpaceAfter = Unit.FromPoint(2);
    }

    public static Table TabelaComCabecalho(Section s, params (string Titulo, double LarguraCm)[] colunas)
    {
        var t = s.AddTable();
        t.Style = "Tabela";
        t.Borders.Width = 0.5; t.Borders.Color = Linha;
        t.LeftPadding = t.RightPadding = Unit.FromPoint(4);
        t.TopPadding = t.BottomPadding = Unit.FromPoint(3);
        foreach (var c in colunas) t.AddColumn(Unit.FromCentimeter(c.LarguraCm));
        var h = t.AddRow();
        h.HeadingFormat = true; // repete em quebras de página
        h.Shading.Color = FundoCabecalho;
        h.Format.Font.Bold = true;
        for (int i = 0; i < colunas.Length; i++) h.Cells[i].AddParagraph(colunas[i].Titulo);
        return t;
    }

    // ------------------------------------------------------------------ Renderização + assinatura

    private const double AlturaAssinaturaCm = 3.4; // espaço em branco + linha + rótulo
    private const double AfastamentoMinimoCm = 1.2; // distância mínima do último parágrafo

    public static byte[] Renderizar(Document doc, bool assinatura)
    {
        Iniciar();
        var renderer = Render(doc);
        if (assinatura)
        {
            var ps = doc.Sections.Count > 0 ? ((Section)doc.Sections[doc.Sections.Count - 1]!).PageSetup : doc.DefaultPageSetup;
            var paisagem = ps.Orientation == Orientation.Landscape;
            var alturaPagina = XUnit.FromCentimeter(paisagem ? 21.0 : 29.7).Point;
            var limiteCorpo = alturaPagina - XUnit.FromCentimeter(MargemInferior).Point;
            var topoCorpo = XUnit.FromCentimeter(MargemSuperior).Point;
            var fimConteudo = FimDoConteudo(renderer, topoCorpo, limiteCorpo);
            var necessario = XUnit.FromCentimeter(AlturaAssinaturaCm + AfastamentoMinimoCm).Point;
            if (limiteCorpo - fimConteudo < necessario)
            {
                // Não cabe: leva a assinatura para uma nova página (ajuste de paginação).
                var ultima = (Section)doc.Sections[doc.Sections.Count - 1]!;
                ultima.AddPageBreak();
                ultima.AddParagraph(" ");
                renderer = Render(doc);
            }
            DesenharAssinatura(renderer, paisagem);
        }
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static PdfDocumentRenderer Render(Document doc)
    {
        var r = new PdfDocumentRenderer { Document = doc.Clone() };
        r.RenderDocument();
        r.PdfDocument.Info.Creator = "";
        r.PdfDocument.Info.Author = "";
        return r;
    }

    private static double FimDoConteudo(PdfDocumentRenderer renderer, double topoCorpo, double limiteCorpo)
    {
        var dr = renderer.DocumentRenderer;
        var paginas = dr.FormattedDocument.PageCount;
        double fim = topoCorpo;
        foreach (var info in dr.GetRenderInfoFromPage(paginas))
        {
            var area = info.LayoutInfo.ContentArea;
            var y = area.Y.Point;
            var bottom = y + area.Height.Point;
            if (y >= topoCorpo - 1 && bottom <= limiteCorpo + 1 && bottom > fim) fim = bottom;
        }
        return fim;
    }

    private static void DesenharAssinatura(PdfDocumentRenderer renderer, bool paisagem)
    {
        var pdf = renderer.PdfDocument;
        var page = pdf.Pages[pdf.PageCount - 1];
        using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
        var largura = page.Width.Point;
        var alturaPagina = page.Height.Point;
        var baseCorpo = alturaPagina - XUnit.FromCentimeter(MargemInferior).Point;
        var yLinha = baseCorpo - XUnit.FromCentimeter(1.0).Point;
        var meio = largura / 2;
        var meiaLinha = XUnit.FromCentimeter(4.5).Point;
        var pen = new XPen(XColor.FromArgb(0x1F, 0x2A, 0x33), 0.8);
        gfx.DrawLine(pen, meio - meiaLinha, yLinha, meio + meiaLinha, yLinha);
        var fonte = new XFont(ResolvedorFontes.Familia, 10, XFontStyleEx.Regular);
        var rect = new XRect(meio - meiaLinha, yLinha + 3, 2 * meiaLinha, 16);
        gfx.DrawString("Assinatura do auditor líder", fonte, new XSolidBrush(XColor.FromArgb(0x1F, 0x2A, 0x33)), rect, XStringFormats.TopCenter);
    }
}
