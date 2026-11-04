using Microsoft.AspNetCore.Components;
using NuClaud.Core.Servicos;
using NuClaud.Web.Infra;

namespace NuClaud.Web.Components.Shared;

/// <summary>Base das páginas: obtém o ator e trata erros de negócio de forma uniforme.</summary>
public abstract class PaginaBase : ComponentBase
{
    [Inject] protected AtorAtual AtorAtual { get; set; } = default!;
    [Inject] protected ServicoNotificacao Notificacoes { get; set; } = default!;
    [Inject] protected ServicoConfirmacao Confirmacao { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;
    [Inject] protected ILogger<PaginaBase> Log { get; set; } = default!;

    protected Ator? Ator { get; private set; }
    protected List<string> Erros { get; } = new();
    protected bool Ocupado { get; set; }
    protected string? ErroCarga { get; set; }

    protected async Task<Ator> ObterAtorAsync() => Ator = await AtorAtual.ObterAsync();

    /// <summary>Executa uma operação exibindo erros de validação, permissão e concorrência.</summary>
    protected async Task<bool> Executar(Func<Task> acao, string? mensagemSucesso = null)
    {
        if (Ocupado) return false;
        Ocupado = true;
        Erros.Clear();
        try
        {
            await acao();
            if (mensagemSucesso != null) Notificacoes.Sucesso(mensagemSucesso);
            return true;
        }
        catch (RegraNegocioException ex) { Erros.AddRange(ex.Erros); Notificacoes.Erro("Não foi possível concluir: verifique as mensagens na tela."); }
        catch (ConcorrenciaException ex) { Erros.Add(ex.Message); Notificacoes.Erro(ex.Message); }
        catch (AcessoNegadoException ex) { Erros.Add(ex.Message); Notificacoes.Erro(ex.Message); }
        catch (NaoEncontradoException ex) { Erros.Add(ex.Message); Notificacoes.Erro(ex.Message); }
        catch (Exception ex)
        {
            Log.LogError(ex, "Erro inesperado na operação");
            Erros.Add("Ocorreu um erro inesperado. Tente novamente; se persistir, contate o suporte.");
            Notificacoes.Erro("Ocorreu um erro inesperado.");
        }
        finally { Ocupado = false; }
        return false;
    }

    /// <summary>Carrega dados iniciais; falhas de acesso são exibidas no lugar do conteúdo.</summary>
    protected async Task Carregar(Func<Task> acao)
    {
        ErroCarga = null;
        try { await acao(); }
        catch (AcessoNegadoException ex) { ErroCarga = ex.Message; }
        catch (NaoEncontradoException ex) { ErroCarga = ex.Message; }
        catch (RegraNegocioException ex) { ErroCarga = ex.Message; }
        catch (Exception ex)
        {
            Log.LogError(ex, "Erro ao carregar a página");
            ErroCarga = "Não foi possível carregar os dados. Tente novamente.";
        }
    }
}
