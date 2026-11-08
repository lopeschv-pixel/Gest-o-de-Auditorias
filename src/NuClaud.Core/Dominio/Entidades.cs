namespace NuClaud.Core.Dominio;

// ------------------------------------------------------------------
// Enumerações
// ------------------------------------------------------------------

public enum PerfilUsuario { Administrador = 1, Usuario = 2 }

public enum TipoAuditoria { Interna = 1, Fornecedor = 2 }

public enum FuncaoAuditoria { Lider = 1, Auditor = 2, Observador = 3 }

/// <summary>Grupo usado para colorir os cartões do calendário.</summary>
public enum GrupoCorNorma
{
    Nenhum = 0,
    /// <summary>ISO 9001 — azul.</summary>
    Qualidade = 1,
    /// <summary>ISO 14001 / ISO 45001 — verde.</summary>
    AmbienteSso = 2,
    /// <summary>ANSN 1.16 / Secretaria Naval — laranja.</summary>
    NuclearNaval = 3,
    /// <summary>ASME III — amarelo.</summary>
    Asme = 4
}

public enum SituacaoRelatorio { EmElaboracao = 1, Concluido = 2, EmRevisao = 3 }

public enum ClassificacaoConstatacao
{
    Conformidade = 1,
    NaoConformidade = 2,
    Observacao = 3,
    OportunidadeMelhoria = 4
}

public enum QualificacaoFornecedor
{
    NaoQualificado = 1,
    QualificadoUmAno = 2,
    QualificadoTresAnos = 3
}

/// <summary>Situação derivada da auditoria (não armazenada).</summary>
public enum SituacaoAuditoria
{
    Programada = 1,
    Planejada = 2,
    RelatorioEmElaboracao = 3,
    EmRevisao = 4,
    Realizada = 5
}

// ------------------------------------------------------------------
// Bases
// ------------------------------------------------------------------

/// <summary>
/// Entidade com identificador interno permanente, controle de concorrência otimista
/// (Versao) e rastreio de autoria. Instantes são gravados em UTC.
/// </summary>
public abstract class EntidadeRastreada
{
    public int Id { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
    public DateTime CriadoEmUtc { get; set; }
    public string CriadoPor { get; set; } = "";
    public DateTime? AlteradoEmUtc { get; set; }
    public string? AlteradoPor { get; set; }
}

// ------------------------------------------------------------------
// Cadastros
// ------------------------------------------------------------------

public class Usuario : EntidadeRastreada
{
    public string NomeRede { get; set; } = "";
    public string NomeCompleto { get; set; } = "";
    public string Email { get; set; } = "";
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Usuario;
    public bool Ativo { get; set; } = true;
    public string? SenhaHash { get; set; }
    public Guid CarimboSeguranca { get; set; } = Guid.NewGuid();
    public bool DeveTrocarSenha { get; set; } = true;
    public int FalhasAcesso { get; set; }
    public DateTime? BloqueadoAteUtc { get; set; }
    public DateTime? UltimoAcessoUtc { get; set; }
    public Auditor? Auditor { get; set; }
}

public class Area : EntidadeRastreada
{
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public class Norma : EntidadeRastreada
{
    public string Nome { get; set; } = "";
    public string? Edicao { get; set; }
    /// <summary>Fonte / observações da carga inicial dos requisitos.</summary>
    public string? FonteCarga { get; set; }
    public GrupoCorNorma GrupoCor { get; set; }
    public bool Ativo { get; set; } = true;
    public int Ordem { get; set; }
    public List<Requisito> Requisitos { get; set; } = new();
}

public class Requisito : EntidadeRastreada
{
    public int NormaId { get; set; }
    public Norma? Norma { get; set; }
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    /// <summary>Chave para ordenação numérica por segmentos (ex.: 0004.0002.0001).</summary>
    public string ChaveOrdenacao { get; set; } = "";
    public bool Ativo { get; set; } = true;
}

public class Auditor : EntidadeRastreada
{
    public string NomeCompleto { get; set; } = "";
    public string? Email { get; set; }
    public int? UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public bool Ativo { get; set; } = true;
    public List<HabilitacaoAuditor> Habilitacoes { get; set; } = new();
}

public class HabilitacaoAuditor
{
    public int Id { get; set; }
    public int AuditorId { get; set; }
    public Auditor? Auditor { get; set; }
    public int NormaId { get; set; }
    public Norma? Norma { get; set; }
    public bool PodeLiderar { get; set; }
    public bool PodeAuditar { get; set; }
    public bool PodeObservar { get; set; }
}

public class Macroprocesso : EntidadeRastreada
{
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public List<Processo> Processos { get; set; } = new();
}

public class Processo : EntidadeRastreada
{
    public string Nome { get; set; } = "";
    public int MacroprocessoId { get; set; }
    public Macroprocesso? Macroprocesso { get; set; }
    public bool Ativo { get; set; } = true;
    public List<ProcessoNorma> Normas { get; set; } = new();
    public List<ProcessoRequisito> RequisitosObrigatorios { get; set; } = new();
}

public class ProcessoNorma
{
    public int ProcessoId { get; set; }
    public Processo? Processo { get; set; }
    public int NormaId { get; set; }
    public Norma? Norma { get; set; }
}

public class ProcessoRequisito
{
    public int ProcessoId { get; set; }
    public Processo? Processo { get; set; }
    public int RequisitoId { get; set; }
    public Requisito? Requisito { get; set; }
}

// ------------------------------------------------------------------
// Auditoria programada (com retratos históricos dos cadastros)
// ------------------------------------------------------------------

public class Auditoria : EntidadeRastreada
{
    public TipoAuditoria Tipo { get; set; }
    public int Ano { get; set; }
    public int Sequencia { get; set; }
    /// <summary>Código visível (ex.: AUDI-I-001-2027). Não é chave primária.</summary>
    public string Codigo { get; set; } = "";
    public int MesPrevisto { get; set; }

    public string? FornecedorNome { get; set; }
    public string? FornecedorCnpj { get; set; }
    public string? FornecedorEndereco { get; set; }
    public string? EscopoFornecimento { get; set; }

    public List<AuditoriaArea> Areas { get; set; } = new();
    public List<AuditoriaNorma> Normas { get; set; } = new();
    public List<AuditoriaMacroprocesso> Macroprocessos { get; set; } = new();
    public List<AuditoriaProcesso> Processos { get; set; } = new();
    public List<AuditoriaParticipante> Participantes { get; set; } = new();
    public List<AuditoriaRequisito> Requisitos { get; set; } = new();
    public Plano? Plano { get; set; }
    public Relatorio? Relatorio { get; set; }

    public SituacaoAuditoria Situacao => Relatorio?.Situacao switch
    {
        SituacaoRelatorio.Concluido => SituacaoAuditoria.Realizada,
        SituacaoRelatorio.EmRevisao => SituacaoAuditoria.EmRevisao,
        SituacaoRelatorio.EmElaboracao => SituacaoAuditoria.RelatorioEmElaboracao,
        _ => Plano != null ? SituacaoAuditoria.Planejada : SituacaoAuditoria.Programada
    };

    public AuditoriaParticipante? Lider => Participantes.FirstOrDefault(p => p.Funcao == FuncaoAuditoria.Lider);
}

public class AuditoriaArea
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public int AreaId { get; set; }
    public Area? Area { get; set; }
    public string NomeArea { get; set; } = "";
}

public class AuditoriaNorma
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public int NormaId { get; set; }
    public Norma? Norma { get; set; }
    public string NomeNorma { get; set; } = "";
    public string? EdicaoNorma { get; set; }
    public GrupoCorNorma GrupoCor { get; set; }
}

public class AuditoriaMacroprocesso
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public int MacroprocessoId { get; set; }
    public Macroprocesso? Macroprocesso { get; set; }
    public string NomeMacroprocesso { get; set; } = "";
}

public class AuditoriaProcesso
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public int ProcessoId { get; set; }
    public Processo? Processo { get; set; }
    public string NomeProcesso { get; set; } = "";
    public string NomeMacroprocesso { get; set; } = "";
}

public class AuditoriaParticipante
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public Auditoria? Auditoria { get; set; }
    public int AuditorId { get; set; }
    public Auditor? Auditor { get; set; }
    public FuncaoAuditoria Funcao { get; set; }
    public string NomeAuditor { get; set; } = "";
    public string? EmailAuditor { get; set; }
}

public class AuditoriaRequisito
{
    public int Id { get; set; }
    public int AuditoriaId { get; set; }
    public int RequisitoId { get; set; }
    public Requisito? Requisito { get; set; }
    public int NormaId { get; set; }
    public string NomeNorma { get; set; } = "";
    public string Codigo { get; set; } = "";
    public string Titulo { get; set; } = "";
    public string ChaveOrdenacao { get; set; } = "";
    /// <summary>Processo em que o requisito será auditado (nulo = requisito geral, sem processo específico).</summary>
    public int? ProcessoId { get; set; }
    public Processo? Processo { get; set; }
    /// <summary>Retrato do nome do processo no momento da programação.</summary>
    public string? NomeProcesso { get; set; }
    /// <summary>Legado (versões anteriores): requisito incluído automaticamente pelo processo.</summary>
    public bool Obrigatorio { get; set; }
    /// <summary>Legado (versões anteriores): processos de origem.</summary>
    public string? Origem { get; set; }

    public string ProcessoExibicao => NomeProcesso ?? "Geral";
}

// ------------------------------------------------------------------
// Plano e relatório
// ------------------------------------------------------------------

public class Plano : EntidadeRastreada
{
    public int AuditoriaId { get; set; }
    public Auditoria? Auditoria { get; set; }
    public string? Local { get; set; }
    public string? GestorNome { get; set; }
    public string? GestorEmail { get; set; }
    /// <summary>Somente fornecedores.</summary>
    public string? Objetivo { get; set; }
    /// <summary>Somente fornecedores.</summary>
    public string? Escopo { get; set; }
    public List<PlanoPeriodo> Periodos { get; set; } = new();
}

public class PlanoPeriodo
{
    public int Id { get; set; }
    public int PlanoId { get; set; }
    public DateOnly Data { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFim { get; set; }
}

public class Relatorio : EntidadeRastreada
{
    public int AuditoriaId { get; set; }
    public Auditoria? Auditoria { get; set; }
    public SituacaoRelatorio Situacao { get; set; } = SituacaoRelatorio.EmElaboracao;
    public QualificacaoFornecedor? Qualificacao { get; set; }
    public string? ObservacoesConclusao { get; set; }
    public string? NumeroRqf { get; set; }
    public DateTime? ConcluidoEmUtc { get; set; }
    public string? ConcluidoPor { get; set; }
    public int? ConcluidoPorId { get; set; }
    public int NumeroRevisao { get; set; }
    public List<Constatacao> Constatacoes { get; set; } = new();
}

public class Constatacao
{
    public int Id { get; set; }
    public int RelatorioId { get; set; }
    public int Numero { get; set; }
    public ClassificacaoConstatacao Classificacao { get; set; }
    public string? Evidencia { get; set; }
    public string? DescricaoNaoConformidade { get; set; }
    public string? EvidenciasObjetivas { get; set; }
    public string? NumeroPac { get; set; }
    public string? Observacao { get; set; }
    public string? OportunidadeMelhoria { get; set; }
    public List<ConstatacaoRequisito> Requisitos { get; set; } = new();
}

public class ConstatacaoRequisito
{
    public int ConstatacaoId { get; set; }
    public int AuditoriaRequisitoId { get; set; }
    public AuditoriaRequisito? AuditoriaRequisito { get; set; }
}

// ------------------------------------------------------------------
// Configuração e histórico
// ------------------------------------------------------------------

public class ConfiguracaoVisual
{
    public int Id { get; set; }
    public byte[]? LogoOriginal { get; set; }
    public string? LogoTipoConteudo { get; set; }
    /// <summary>Versão normalizada em PNG usada nos PDFs.</summary>
    public byte[]? LogoPng { get; set; }
    public int LogoLargura { get; set; }
    public int LogoAltura { get; set; }
    public DateTime? AtualizadoEmUtc { get; set; }
    public string? AtualizadoPor { get; set; }
    public Guid Versao { get; set; } = Guid.NewGuid();
}

public class HistoricoAlteracao
{
    public long Id { get; set; }
    public DateTime DataHoraUtc { get; set; }
    public int? UsuarioId { get; set; }
    public string UsuarioNome { get; set; } = "";
    public string Entidade { get; set; } = "";
    public string EntidadeId { get; set; } = "";
    public string Acao { get; set; } = "";
    public string Resumo { get; set; } = "";
    public string? DadosJson { get; set; }
}
