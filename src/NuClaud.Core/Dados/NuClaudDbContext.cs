using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NuClaud.Core.Dominio;

namespace NuClaud.Core.Dados;

public static class LimitesTexto
{
    /// <summary>Campos textuais longos (constatações). Mapeado para nvarchar(max) no SQL Server.</summary>
    public const int TextoLongo = 20000;
}

public class NuClaudDbContext : DbContext
{
    public NuClaudDbContext(DbContextOptions<NuClaudDbContext> options) : base(options) { }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Norma> Normas => Set<Norma>();
    public DbSet<Requisito> Requisitos => Set<Requisito>();
    public DbSet<Auditor> Auditores => Set<Auditor>();
    public DbSet<HabilitacaoAuditor> Habilitacoes => Set<HabilitacaoAuditor>();
    public DbSet<Macroprocesso> Macroprocessos => Set<Macroprocesso>();
    public DbSet<Processo> Processos => Set<Processo>();
    public DbSet<ProcessoNorma> ProcessoNormas => Set<ProcessoNorma>();
    public DbSet<ProcessoRequisito> ProcessoRequisitos => Set<ProcessoRequisito>();
    public DbSet<Auditoria> Auditorias => Set<Auditoria>();
    public DbSet<AuditoriaArea> AuditoriaAreas => Set<AuditoriaArea>();
    public DbSet<AuditoriaNorma> AuditoriaNormas => Set<AuditoriaNorma>();
    public DbSet<AuditoriaMacroprocesso> AuditoriaMacroprocessos => Set<AuditoriaMacroprocesso>();
    public DbSet<AuditoriaProcesso> AuditoriaProcessos => Set<AuditoriaProcesso>();
    public DbSet<AuditoriaParticipante> AuditoriaParticipantes => Set<AuditoriaParticipante>();
    public DbSet<AuditoriaRequisito> AuditoriaRequisitos => Set<AuditoriaRequisito>();
    public DbSet<Plano> Planos => Set<Plano>();
    public DbSet<PlanoPeriodo> PlanoPeriodos => Set<PlanoPeriodo>();
    public DbSet<Relatorio> Relatorios => Set<Relatorio>();
    public DbSet<Constatacao> Constatacoes => Set<Constatacao>();
    public DbSet<ConstatacaoRequisito> ConstatacaoRequisitos => Set<ConstatacaoRequisito>();
    public DbSet<ConfiguracaoVisual> ConfiguracoesVisuais => Set<ConfiguracaoVisual>();
    public DbSet<HistoricoAlteracao> Historico => Set<HistoricoAlteracao>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Por padrão, nenhuma exclusão em cascata entre cadastros: remoções são controladas pelos serviços.
        foreach (var fk in b.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        b.Entity<Usuario>(e =>
        {
            Rastreio(e);
            e.Property(x => x.NomeRede).HasMaxLength(64).IsRequired();
            e.HasIndex(x => x.NomeRede).IsUnique();
            e.Property(x => x.NomeCompleto).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.SenhaHash).HasMaxLength(500);
        });

        b.Entity<Area>(e =>
        {
            Rastreio(e);
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Nome).IsUnique();
        });

        b.Entity<Norma>(e =>
        {
            Rastreio(e);
            e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Nome).IsUnique();
            e.Property(x => x.Edicao).HasMaxLength(200);
            e.Property(x => x.FonteCarga).HasMaxLength(2000);
        });

        b.Entity<Requisito>(e =>
        {
            Rastreio(e);
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Titulo).HasMaxLength(300).IsRequired();
            e.Property(x => x.ChaveOrdenacao).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.NormaId, x.Codigo }).IsUnique();
            e.HasOne(x => x.Norma).WithMany(n => n.Requisitos).HasForeignKey(x => x.NormaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Auditor>(e =>
        {
            Rastreio(e);
            e.Property(x => x.NomeCompleto).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasOne(x => x.Usuario).WithOne(u => u.Auditor).HasForeignKey<Auditor>(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UsuarioId).IsUnique().HasFilter("[UsuarioId] IS NOT NULL");
        });

        b.Entity<HabilitacaoAuditor>(e =>
        {
            e.HasIndex(x => new { x.AuditorId, x.NormaId }).IsUnique();
            e.HasOne(x => x.Auditor).WithMany(a => a.Habilitacoes).HasForeignKey(x => x.AuditorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Norma).WithMany().HasForeignKey(x => x.NormaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Macroprocesso>(e =>
        {
            Rastreio(e);
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.HasIndex(x => x.Nome).IsUnique();
        });

        b.Entity<Processo>(e =>
        {
            Rastreio(e);
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.HasIndex(x => new { x.MacroprocessoId, x.Nome }).IsUnique();
            e.HasOne(x => x.Macroprocesso).WithMany(m => m.Processos).HasForeignKey(x => x.MacroprocessoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ProcessoNorma>(e =>
        {
            e.HasKey(x => new { x.ProcessoId, x.NormaId });
            e.HasOne(x => x.Processo).WithMany(p => p.Normas).HasForeignKey(x => x.ProcessoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Norma).WithMany().HasForeignKey(x => x.NormaId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ProcessoRequisito>(e =>
        {
            e.HasKey(x => new { x.ProcessoId, x.RequisitoId });
            e.HasOne(x => x.Processo).WithMany(p => p.RequisitosObrigatorios).HasForeignKey(x => x.ProcessoId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Requisito).WithMany().HasForeignKey(x => x.RequisitoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Auditoria>(e =>
        {
            Rastreio(e);
            e.Property(x => x.Codigo).HasMaxLength(30).IsRequired();
            e.HasIndex(x => x.Codigo).IsUnique();
            e.HasIndex(x => new { x.Tipo, x.Ano, x.Sequencia }).IsUnique();
            e.HasIndex(x => new { x.Ano, x.MesPrevisto });
            e.Property(x => x.FornecedorNome).HasMaxLength(200);
            e.Property(x => x.FornecedorCnpj).HasMaxLength(18);
            e.Property(x => x.FornecedorEndereco).HasMaxLength(400);
            e.Property(x => x.EscopoFornecimento).HasMaxLength(2000);
            e.Ignore(x => x.Situacao);
            e.Ignore(x => x.Lider);
            e.HasMany(x => x.Areas).WithOne().HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Normas).WithOne().HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Macroprocessos).WithOne().HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Processos).WithOne().HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Participantes).WithOne(p => p.Auditoria).HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Requisitos).WithOne().HasForeignKey(x => x.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plano).WithOne(p => p.Auditoria).HasForeignKey<Plano>(p => p.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Relatorio).WithOne(r => r.Auditoria).HasForeignKey<Relatorio>(r => r.AuditoriaId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuditoriaArea>(e =>
        {
            e.Property(x => x.NomeArea).HasMaxLength(150).IsRequired();
            e.HasIndex(x => new { x.AuditoriaId, x.AreaId }).IsUnique();
            e.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditoriaNorma>(e =>
        {
            e.Property(x => x.NomeNorma).HasMaxLength(100).IsRequired();
            e.Property(x => x.EdicaoNorma).HasMaxLength(200);
            e.HasIndex(x => new { x.AuditoriaId, x.NormaId }).IsUnique();
            e.HasOne(x => x.Norma).WithMany().HasForeignKey(x => x.NormaId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditoriaMacroprocesso>(e =>
        {
            e.Property(x => x.NomeMacroprocesso).HasMaxLength(150).IsRequired();
            e.HasIndex(x => new { x.AuditoriaId, x.MacroprocessoId }).IsUnique();
            e.HasOne(x => x.Macroprocesso).WithMany().HasForeignKey(x => x.MacroprocessoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditoriaProcesso>(e =>
        {
            e.Property(x => x.NomeProcesso).HasMaxLength(150).IsRequired();
            e.Property(x => x.NomeMacroprocesso).HasMaxLength(150).IsRequired();
            e.HasIndex(x => new { x.AuditoriaId, x.ProcessoId }).IsUnique();
            e.HasOne(x => x.Processo).WithMany().HasForeignKey(x => x.ProcessoId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditoriaParticipante>(e =>
        {
            e.Property(x => x.NomeAuditor).HasMaxLength(200).IsRequired();
            e.Property(x => x.EmailAuditor).HasMaxLength(254);
            e.HasIndex(x => new { x.AuditoriaId, x.AuditorId }).IsUnique();
            e.HasOne(x => x.Auditor).WithMany().HasForeignKey(x => x.AuditorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AuditoriaRequisito>(e =>
        {
            e.Property(x => x.NomeNorma).HasMaxLength(100).IsRequired();
            e.Property(x => x.Codigo).HasMaxLength(20).IsRequired();
            e.Property(x => x.Titulo).HasMaxLength(300).IsRequired();
            e.Property(x => x.ChaveOrdenacao).HasMaxLength(40).IsRequired();
            e.Property(x => x.Origem).HasMaxLength(1000);
            e.HasIndex(x => new { x.AuditoriaId, x.RequisitoId }).IsUnique();
            e.HasOne(x => x.Requisito).WithMany().HasForeignKey(x => x.RequisitoId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Plano>(e =>
        {
            Rastreio(e);
            e.HasIndex(x => x.AuditoriaId).IsUnique();
            e.Property(x => x.Local).HasMaxLength(300);
            e.Property(x => x.GestorNome).HasMaxLength(200);
            e.Property(x => x.GestorEmail).HasMaxLength(254);
            e.Property(x => x.Objetivo).HasMaxLength(4000);
            e.Property(x => x.Escopo).HasMaxLength(4000);
            e.HasMany(x => x.Periodos).WithOne().HasForeignKey(x => x.PlanoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Relatorio>(e =>
        {
            Rastreio(e);
            e.HasIndex(x => x.AuditoriaId).IsUnique();
            e.Property(x => x.ObservacoesConclusao).HasMaxLength(8000);
            e.Property(x => x.NumeroRqf).HasMaxLength(50);
            e.Property(x => x.ConcluidoPor).HasMaxLength(260);
            e.HasMany(x => x.Constatacoes).WithOne().HasForeignKey(x => x.RelatorioId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Constatacao>(e =>
        {
            e.HasIndex(x => new { x.RelatorioId, x.Numero });
            e.Property(x => x.NumeroPac).HasMaxLength(50);
            e.Property(x => x.Evidencia).HasMaxLength(LimitesTexto.TextoLongo);
            e.Property(x => x.DescricaoNaoConformidade).HasMaxLength(LimitesTexto.TextoLongo);
            e.Property(x => x.EvidenciasObjetivas).HasMaxLength(LimitesTexto.TextoLongo);
            e.Property(x => x.Observacao).HasMaxLength(LimitesTexto.TextoLongo);
            e.Property(x => x.OportunidadeMelhoria).HasMaxLength(LimitesTexto.TextoLongo);
            e.HasMany(x => x.Requisitos).WithOne().HasForeignKey(x => x.ConstatacaoId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<ConstatacaoRequisito>(e =>
        {
            e.HasKey(x => new { x.ConstatacaoId, x.AuditoriaRequisitoId });
            // NoAction: evita múltiplos caminhos de cascata no SQL Server; o serviço remove o relatório antes da auditoria.
            e.HasOne(x => x.AuditoriaRequisito).WithMany().HasForeignKey(x => x.AuditoriaRequisitoId).OnDelete(DeleteBehavior.NoAction);
        });

        b.Entity<ConfiguracaoVisual>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.LogoTipoConteudo).HasMaxLength(50);
            e.Property(x => x.AtualizadoPor).HasMaxLength(260);
            e.Property(x => x.Versao).IsConcurrencyToken();
        });

        b.Entity<HistoricoAlteracao>(e =>
        {
            e.Property(x => x.UsuarioNome).HasMaxLength(260).IsRequired();
            e.Property(x => x.Entidade).HasMaxLength(60).IsRequired();
            e.Property(x => x.EntidadeId).HasMaxLength(40).IsRequired();
            e.Property(x => x.Acao).HasMaxLength(60).IsRequired();
            e.Property(x => x.Resumo).HasMaxLength(1000).IsRequired();
            e.Property(x => x.DadosJson).HasMaxLength(1_000_000);
            e.HasIndex(x => new { x.Entidade, x.EntidadeId });
            e.HasIndex(x => x.DataHoraUtc);
        });
    }

    private static void Rastreio<T>(EntityTypeBuilder<T> e) where T : EntidadeRastreada
    {
        e.Property(x => x.Versao).IsConcurrencyToken();
        e.Property(x => x.CriadoPor).HasMaxLength(260).IsRequired();
        e.Property(x => x.AlteradoPor).HasMaxLength(260);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<string>().HaveMaxLength(4000);
    }
}
