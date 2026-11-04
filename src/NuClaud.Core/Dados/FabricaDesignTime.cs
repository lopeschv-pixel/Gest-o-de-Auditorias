using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NuClaud.Core.Dados;

/// <summary>
/// Usada apenas pelas ferramentas do EF Core (dotnet ef). A geração de migrações não conecta ao banco;
/// para "database update" defina a variável NUCLAUD_CONEXAO (nunca grave credenciais no repositório).
/// </summary>
public sealed class FabricaDesignTime : IDesignTimeDbContextFactory<NuClaudDbContext>
{
    public NuClaudDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("NUCLAUD_CONEXAO")
                 ?? "Server=localhost;Database=NuClaud;Integrated Security=true;TrustServerCertificate=true";
        return new NuClaudDbContext(new DbContextOptionsBuilder<NuClaudDbContext>().UseSqlServer(cs).Options);
    }
}
