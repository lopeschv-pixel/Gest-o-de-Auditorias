namespace NuClaud.Web.Components.Shared;

public sealed record OpcaoSelecao(int Valor, string Texto, string? Grupo = null, bool Desabilitado = false, string? Detalhe = null);
