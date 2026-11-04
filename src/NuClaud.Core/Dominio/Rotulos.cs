using System.Globalization;

namespace NuClaud.Core.Dominio;

/// <summary>Textos em português do Brasil para enumerações e formatações.</summary>
public static class Rotulos
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Texto(this PerfilUsuario p) => p switch
    {
        PerfilUsuario.Administrador => "Administrador",
        _ => "Usuário"
    };

    public static string Texto(this TipoAuditoria t) => t switch
    {
        TipoAuditoria.Interna => "Interna",
        _ => "Fornecedores"
    };

    public static string Texto(this FuncaoAuditoria f) => f switch
    {
        FuncaoAuditoria.Lider => "Auditor líder",
        FuncaoAuditoria.Auditor => "Auditor",
        _ => "Observador"
    };

    public static string Texto(this ClassificacaoConstatacao c) => c switch
    {
        ClassificacaoConstatacao.Conformidade => "Conformidade",
        ClassificacaoConstatacao.NaoConformidade => "Não conformidade",
        ClassificacaoConstatacao.Observacao => "Observação",
        _ => "Oportunidade de melhoria"
    };

    public static string Texto(this QualificacaoFornecedor q) => q switch
    {
        QualificacaoFornecedor.NaoQualificado => "Não qualificado",
        QualificacaoFornecedor.QualificadoUmAno => "Qualificado por um ano",
        _ => "Qualificado por três anos"
    };

    public static string Texto(this SituacaoAuditoria s) => s switch
    {
        SituacaoAuditoria.Programada => "Programada",
        SituacaoAuditoria.Planejada => "Plano emitido",
        SituacaoAuditoria.RelatorioEmElaboracao => "Relatório em elaboração",
        SituacaoAuditoria.EmRevisao => "Relatório em revisão",
        _ => "Realizada"
    };

    public static string Texto(this SituacaoRelatorio s) => s switch
    {
        SituacaoRelatorio.EmElaboracao => "Em elaboração",
        SituacaoRelatorio.Concluido => "Concluído",
        _ => "Em revisão"
    };

    public static string Texto(this GrupoCorNorma g) => g switch
    {
        GrupoCorNorma.Qualidade => "Azul (qualidade — ISO 9001)",
        GrupoCorNorma.AmbienteSso => "Verde (meio ambiente / SSO — ISO 14001, ISO 45001)",
        GrupoCorNorma.NuclearNaval => "Laranja (nuclear / naval — ANSN 1.16, Secretaria Naval)",
        GrupoCorNorma.Asme => "Amarelo (ASME III)",
        _ => "Sem grupo de cor (azul-petróleo)"
    };

    public static string NomeMes(int mes) =>
        mes is >= 1 and <= 12 ? PtBr.TextInfo.ToTitleCase(PtBr.DateTimeFormat.GetMonthName(mes)) : "—";

    public static string Data(DateOnly d) => d.ToString("dd/MM/yyyy", PtBr);
    public static string Hora(TimeOnly t) => t.ToString("HH:mm", PtBr);

    public static string DiaSemana(DateOnly d) => PtBr.DateTimeFormat.GetDayName(d.DayOfWeek);

    public static string Periodo(DateOnly d, TimeOnly ini, TimeOnly fim) =>
        $"{Data(d)} — {Hora(ini)} às {Hora(fim)}";

    public static string NumeroConstatacao(int n) => n.ToString("00", PtBr);
}
