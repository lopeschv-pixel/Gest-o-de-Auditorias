using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

/// <summary>Usuário autenticado que executa uma operação. Sempre carregado do banco.</summary>
public sealed record Ator(int UsuarioId, string NomeRede, string NomeCompleto, PerfilUsuario Perfil, int? AuditorId)
{
    public bool EhAdministrador => Perfil == PerfilUsuario.Administrador;
    public string Assinatura => $"{NomeCompleto} ({NomeRede})";

    /// <summary>Ator técnico usado por rotinas de inicialização.</summary>
    public static Ator Sistema { get; } = new(0, "sistema", "Sistema", PerfilUsuario.Administrador, null);
}

/// <summary>Violação de regra de negócio. Mensagens são seguras para exibir ao usuário.</summary>
public class RegraNegocioException : Exception
{
    public IReadOnlyList<string> Erros { get; }
    public RegraNegocioException(string mensagem) : base(mensagem) => Erros = new[] { mensagem };
    public RegraNegocioException(IEnumerable<string> erros) : this(erros.ToList()) { }
    private RegraNegocioException(List<string> erros)
        : base(erros.Count == 1 ? erros[0] : "Corrija os itens indicados.") => Erros = erros;
}

public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string mensagem = "Você não tem permissão para executar esta operação.") : base(mensagem) { }
}

public class NaoEncontradoException : Exception
{
    public NaoEncontradoException(string mensagem = "Registro não encontrado.") : base(mensagem) { }
}

public class ConcorrenciaException : Exception
{
    public ConcorrenciaException() : base(
        "Estes dados foram alterados por outra pessoa depois que você abriu a tela. " +
        "Recarregue a página para ver a versão atual antes de salvar novamente.") { }
}

public interface IRelogio
{
    DateTime UtcAgora { get; }
}

public sealed class RelogioSistema : IRelogio
{
    public DateTime UtcAgora => DateTime.UtcNow;
}

/// <summary>
/// Instantes de histórico são gravados em UTC e exibidos em America/Sao_Paulo.
/// Datas e horários de auditoria (DateOnly/TimeOnly) são "horário de negócio" e não sofrem conversão.
/// </summary>
public static class FusoHorario
{
    private static readonly TimeZoneInfo Brasilia = Obter();

    private static TimeZoneInfo Obter()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch { return TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "Brasília", "Brasília"); }
    }

    public static DateTime ParaBrasilia(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Brasilia);

    public static string Formatar(DateTime? utc) =>
        utc is null ? "—" : ParaBrasilia(utc.Value).ToString("dd/MM/yyyy HH:mm", Rotulos.PtBr);

    public static int AnoAtual(IRelogio relogio) => ParaBrasilia(relogio.UtcAgora).Year;
}
