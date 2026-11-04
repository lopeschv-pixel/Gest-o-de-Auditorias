namespace NuClaud.Web.Components.Shared;

public enum TipoNotificacao { Sucesso, Erro, Aviso, Info }

public sealed record Notificacao(Guid Id, TipoNotificacao Tipo, string Texto);

/// <summary>Mensagens de confirmação visual ("salvo com sucesso", erros, avisos).</summary>
public sealed class ServicoNotificacao
{
    public event Action? Mudou;
    public List<Notificacao> Itens { get; } = new();

    public void Mostrar(TipoNotificacao tipo, string texto)
    {
        var n = new Notificacao(Guid.NewGuid(), tipo, texto);
        Itens.Add(n);
        Mudou?.Invoke();
        _ = RemoverDepois(n, tipo == TipoNotificacao.Erro ? 9000 : 5000);
    }

    public void Sucesso(string t) => Mostrar(TipoNotificacao.Sucesso, t);
    public void Erro(string t) => Mostrar(TipoNotificacao.Erro, t);
    public void Aviso(string t) => Mostrar(TipoNotificacao.Aviso, t);

    public void Remover(Guid id)
    {
        Itens.RemoveAll(x => x.Id == id);
        Mudou?.Invoke();
    }

    private async Task RemoverDepois(Notificacao n, int ms)
    {
        await Task.Delay(ms);
        Remover(n.Id);
    }
}

public sealed record PedidoConfirmacao(string Titulo, string Mensagem, string TextoConfirmar, bool Perigo, IReadOnlyList<string>? Itens,
    TaskCompletionSource<bool> Resultado);

/// <summary>Diálogo de confirmação antes de exclusões e ações irreversíveis.</summary>
public sealed class ServicoConfirmacao
{
    public event Action? Mudou;
    public PedidoConfirmacao? Atual { get; private set; }

    public Task<bool> ConfirmarAsync(string titulo, string mensagem, string textoConfirmar = "Confirmar", bool perigo = false, IReadOnlyList<string>? itens = null)
    {
        Atual?.Resultado.TrySetResult(false);
        var p = new PedidoConfirmacao(titulo, mensagem, textoConfirmar, perigo, itens, new TaskCompletionSource<bool>());
        Atual = p;
        Mudou?.Invoke();
        return p.Resultado.Task;
    }

    public void Responder(bool ok)
    {
        var p = Atual;
        Atual = null;
        Mudou?.Invoke();
        p?.Resultado.TrySetResult(ok);
    }
}
