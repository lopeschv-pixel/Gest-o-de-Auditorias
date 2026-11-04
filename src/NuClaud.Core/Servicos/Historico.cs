using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dados;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Servicos;

public static class Historico
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = false,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serializar(object? dados) => JsonSerializer.Serialize(dados, Json);

    /// <summary>Cópia profunda de um DTO: os serviços nunca alteram o objeto recebido da interface.</summary>
    public static T Clonar<T>(T dto) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(dto))!;

    public static void Registrar(NuClaudDbContext db, Ator ator, DateTime agoraUtc, string entidade, object entidadeId,
        string acao, string resumo, object? dados = null)
    {
        db.Historico.Add(new HistoricoAlteracao
        {
            DataHoraUtc = agoraUtc,
            UsuarioId = ator.UsuarioId == 0 ? null : ator.UsuarioId,
            UsuarioNome = ator.Assinatura,
            Entidade = entidade,
            EntidadeId = entidadeId.ToString() ?? "",
            Acao = acao,
            Resumo = resumo.Length > 1000 ? resumo[..1000] : resumo,
            DadosJson = dados == null ? null : Serializar(dados)
        });
    }

    public static void MarcarCriacao(EntidadeRastreada e, Ator ator, DateTime agora)
    {
        e.CriadoEmUtc = agora;
        e.CriadoPor = ator.Assinatura;
        e.Versao = Guid.NewGuid();
    }

    /// <summary>
    /// Aplica o controle de concorrência otimista: a versão que o usuário carregou precisa
    /// coincidir com a gravada; caso contrário o SaveChanges lança DbUpdateConcurrencyException.
    /// </summary>
    public static void MarcarAlteracao(NuClaudDbContext db, EntidadeRastreada e, Ator ator, DateTime agora, Guid? versaoCarregada)
    {
        if (versaoCarregada is Guid v)
        {
            if (v != e.Versao) throw new ConcorrenciaException();
            db.Entry(e).Property(nameof(EntidadeRastreada.Versao)).OriginalValue = v;
        }
        e.Versao = Guid.NewGuid();
        e.AlteradoEmUtc = agora;
        e.AlteradoPor = ator.Assinatura;
    }

    public static async Task SalvarAsync(NuClaudDbContext db, CancellationToken ct = default)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConcorrenciaException(); }
    }

    /// <summary>Detecta violação de índice único (SQL Server 2601/2627 ou SQLite 19).</summary>
    public static bool EhViolacaoUnicidade(DbUpdateException ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            var tipo = e.GetType().Name;
            if (tipo == "SqlException")
            {
                var numero = (int?)e.GetType().GetProperty("Number")?.GetValue(e);
                if (numero is 2601 or 2627) return true;
            }
            if (tipo == "SqliteException")
            {
                var codigo = (int?)e.GetType().GetProperty("SqliteErrorCode")?.GetValue(e);
                if (codigo == 19) return true;
            }
        }
        return false;
    }
}
