IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Areas] (
        [Id] int NOT NULL IDENTITY,
        [Nome] nvarchar(150) NOT NULL,
        [Ativo] bit NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Areas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Auditorias] (
        [Id] int NOT NULL IDENTITY,
        [Tipo] int NOT NULL,
        [Ano] int NOT NULL,
        [Sequencia] int NOT NULL,
        [Codigo] nvarchar(30) NOT NULL,
        [MesPrevisto] int NOT NULL,
        [FornecedorNome] nvarchar(200) NULL,
        [FornecedorCnpj] nvarchar(18) NULL,
        [FornecedorEndereco] nvarchar(400) NULL,
        [EscopoFornecimento] nvarchar(2000) NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Auditorias] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [ConfiguracoesVisuais] (
        [Id] int NOT NULL,
        [LogoOriginal] varbinary(max) NULL,
        [LogoTipoConteudo] nvarchar(50) NULL,
        [LogoPng] varbinary(max) NULL,
        [LogoLargura] int NOT NULL,
        [LogoAltura] int NOT NULL,
        [AtualizadoEmUtc] datetime2 NULL,
        [AtualizadoPor] nvarchar(260) NULL,
        [Versao] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_ConfiguracoesVisuais] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Historico] (
        [Id] bigint NOT NULL IDENTITY,
        [DataHoraUtc] datetime2 NOT NULL,
        [UsuarioId] int NULL,
        [UsuarioNome] nvarchar(260) NOT NULL,
        [Entidade] nvarchar(60) NOT NULL,
        [EntidadeId] nvarchar(40) NOT NULL,
        [Acao] nvarchar(60) NOT NULL,
        [Resumo] nvarchar(1000) NOT NULL,
        [DadosJson] nvarchar(max) NULL,
        CONSTRAINT [PK_Historico] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Macroprocessos] (
        [Id] int NOT NULL IDENTITY,
        [Nome] nvarchar(150) NOT NULL,
        [Ativo] bit NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Macroprocessos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Normas] (
        [Id] int NOT NULL IDENTITY,
        [Nome] nvarchar(100) NOT NULL,
        [Edicao] nvarchar(200) NULL,
        [FonteCarga] nvarchar(2000) NULL,
        [GrupoCor] int NOT NULL,
        [Ativo] bit NOT NULL,
        [Ordem] int NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Normas] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Usuarios] (
        [Id] int NOT NULL IDENTITY,
        [NomeRede] nvarchar(64) NOT NULL,
        [NomeCompleto] nvarchar(200) NOT NULL,
        [Email] nvarchar(254) NOT NULL,
        [Perfil] int NOT NULL,
        [Ativo] bit NOT NULL,
        [SenhaHash] nvarchar(500) NULL,
        [CarimboSeguranca] uniqueidentifier NOT NULL,
        [DeveTrocarSenha] bit NOT NULL,
        [FalhasAcesso] int NOT NULL,
        [BloqueadoAteUtc] datetime2 NULL,
        [UltimoAcessoUtc] datetime2 NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Usuarios] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [AuditoriaAreas] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [AreaId] int NOT NULL,
        [NomeArea] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_AuditoriaAreas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditoriaAreas_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AuditoriaAreas_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Planos] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [Local] nvarchar(300) NULL,
        [GestorNome] nvarchar(200) NULL,
        [GestorEmail] nvarchar(254) NULL,
        [Objetivo] nvarchar(4000) NULL,
        [Escopo] nvarchar(4000) NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Planos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Planos_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Relatorios] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [Situacao] int NOT NULL,
        [Qualificacao] int NULL,
        [ObservacoesConclusao] nvarchar(max) NULL,
        [NumeroRqf] nvarchar(50) NULL,
        [ConcluidoEmUtc] datetime2 NULL,
        [ConcluidoPor] nvarchar(260) NULL,
        [ConcluidoPorId] int NULL,
        [NumeroRevisao] int NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Relatorios] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Relatorios_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [AuditoriaMacroprocessos] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [MacroprocessoId] int NOT NULL,
        [NomeMacroprocesso] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_AuditoriaMacroprocessos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditoriaMacroprocessos_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AuditoriaMacroprocessos_Macroprocessos_MacroprocessoId] FOREIGN KEY ([MacroprocessoId]) REFERENCES [Macroprocessos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Processos] (
        [Id] int NOT NULL IDENTITY,
        [Nome] nvarchar(150) NOT NULL,
        [MacroprocessoId] int NOT NULL,
        [Ativo] bit NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Processos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Processos_Macroprocessos_MacroprocessoId] FOREIGN KEY ([MacroprocessoId]) REFERENCES [Macroprocessos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [AuditoriaNormas] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [NormaId] int NOT NULL,
        [NomeNorma] nvarchar(100) NOT NULL,
        [EdicaoNorma] nvarchar(200) NULL,
        [GrupoCor] int NOT NULL,
        CONSTRAINT [PK_AuditoriaNormas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditoriaNormas_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AuditoriaNormas_Normas_NormaId] FOREIGN KEY ([NormaId]) REFERENCES [Normas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Requisitos] (
        [Id] int NOT NULL IDENTITY,
        [NormaId] int NOT NULL,
        [Codigo] nvarchar(20) NOT NULL,
        [Titulo] nvarchar(300) NOT NULL,
        [ChaveOrdenacao] nvarchar(40) NOT NULL,
        [Ativo] bit NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Requisitos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Requisitos_Normas_NormaId] FOREIGN KEY ([NormaId]) REFERENCES [Normas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Auditores] (
        [Id] int NOT NULL IDENTITY,
        [NomeCompleto] nvarchar(200) NOT NULL,
        [Email] nvarchar(254) NULL,
        [UsuarioId] int NULL,
        [Ativo] bit NOT NULL,
        [Versao] uniqueidentifier NOT NULL,
        [CriadoEmUtc] datetime2 NOT NULL,
        [CriadoPor] nvarchar(260) NOT NULL,
        [AlteradoEmUtc] datetime2 NULL,
        [AlteradoPor] nvarchar(260) NULL,
        CONSTRAINT [PK_Auditores] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Auditores_Usuarios_UsuarioId] FOREIGN KEY ([UsuarioId]) REFERENCES [Usuarios] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [PlanoPeriodos] (
        [Id] int NOT NULL IDENTITY,
        [PlanoId] int NOT NULL,
        [Data] date NOT NULL,
        [HoraInicio] time NOT NULL,
        [HoraFim] time NOT NULL,
        CONSTRAINT [PK_PlanoPeriodos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanoPeriodos_Planos_PlanoId] FOREIGN KEY ([PlanoId]) REFERENCES [Planos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Constatacoes] (
        [Id] int NOT NULL IDENTITY,
        [RelatorioId] int NOT NULL,
        [Numero] int NOT NULL,
        [Classificacao] int NOT NULL,
        [Evidencia] nvarchar(max) NULL,
        [DescricaoNaoConformidade] nvarchar(max) NULL,
        [EvidenciasObjetivas] nvarchar(max) NULL,
        [NumeroPac] nvarchar(50) NULL,
        [Observacao] nvarchar(max) NULL,
        [OportunidadeMelhoria] nvarchar(max) NULL,
        CONSTRAINT [PK_Constatacoes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Constatacoes_Relatorios_RelatorioId] FOREIGN KEY ([RelatorioId]) REFERENCES [Relatorios] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [AuditoriaProcessos] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [ProcessoId] int NOT NULL,
        [NomeProcesso] nvarchar(150) NOT NULL,
        [NomeMacroprocesso] nvarchar(150) NOT NULL,
        CONSTRAINT [PK_AuditoriaProcessos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditoriaProcessos_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AuditoriaProcessos_Processos_ProcessoId] FOREIGN KEY ([ProcessoId]) REFERENCES [Processos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [ProcessoNormas] (
        [ProcessoId] int NOT NULL,
        [NormaId] int NOT NULL,
        CONSTRAINT [PK_ProcessoNormas] PRIMARY KEY ([ProcessoId], [NormaId]),
        CONSTRAINT [FK_ProcessoNormas_Normas_NormaId] FOREIGN KEY ([NormaId]) REFERENCES [Normas] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProcessoNormas_Processos_ProcessoId] FOREIGN KEY ([ProcessoId]) REFERENCES [Processos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [AuditoriaRequisitos] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [RequisitoId] int NOT NULL,
        [NormaId] int NOT NULL,
        [NomeNorma] nvarchar(100) NOT NULL,
        [Codigo] nvarchar(20) NOT NULL,
        [Titulo] nvarchar(300) NOT NULL,
        [ChaveOrdenacao] nvarchar(40) NOT NULL,
        [Obrigatorio] bit NOT NULL,
        [Origem] nvarchar(1000) NULL,
        CONSTRAINT [PK_AuditoriaRequisitos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditoriaRequisitos_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AuditoriaRequisitos_Requisitos_RequisitoId] FOREIGN KEY ([RequisitoId]) REFERENCES [Requisitos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [ProcessoRequisitos] (
        [ProcessoId] int NOT NULL,
        [RequisitoId] int NOT NULL,
        CONSTRAINT [PK_ProcessoRequisitos] PRIMARY KEY ([ProcessoId], [RequisitoId]),
        CONSTRAINT [FK_ProcessoRequisitos_Processos_ProcessoId] FOREIGN KEY ([ProcessoId]) REFERENCES [Processos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProcessoRequisitos_Requisitos_RequisitoId] FOREIGN KEY ([RequisitoId]) REFERENCES [Requisitos] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [AuditoriaParticipantes] (
        [Id] int NOT NULL IDENTITY,
        [AuditoriaId] int NOT NULL,
        [AuditorId] int NOT NULL,
        [Funcao] int NOT NULL,
        [NomeAuditor] nvarchar(200) NOT NULL,
        [EmailAuditor] nvarchar(254) NULL,
        CONSTRAINT [PK_AuditoriaParticipantes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditoriaParticipantes_Auditores_AuditorId] FOREIGN KEY ([AuditorId]) REFERENCES [Auditores] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AuditoriaParticipantes_Auditorias_AuditoriaId] FOREIGN KEY ([AuditoriaId]) REFERENCES [Auditorias] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [Habilitacoes] (
        [Id] int NOT NULL IDENTITY,
        [AuditorId] int NOT NULL,
        [NormaId] int NOT NULL,
        [PodeLiderar] bit NOT NULL,
        [PodeAuditar] bit NOT NULL,
        [PodeObservar] bit NOT NULL,
        CONSTRAINT [PK_Habilitacoes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Habilitacoes_Auditores_AuditorId] FOREIGN KEY ([AuditorId]) REFERENCES [Auditores] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Habilitacoes_Normas_NormaId] FOREIGN KEY ([NormaId]) REFERENCES [Normas] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE TABLE [ConstatacaoRequisitos] (
        [ConstatacaoId] int NOT NULL,
        [AuditoriaRequisitoId] int NOT NULL,
        CONSTRAINT [PK_ConstatacaoRequisitos] PRIMARY KEY ([ConstatacaoId], [AuditoriaRequisitoId]),
        CONSTRAINT [FK_ConstatacaoRequisitos_AuditoriaRequisitos_AuditoriaRequisitoId] FOREIGN KEY ([AuditoriaRequisitoId]) REFERENCES [AuditoriaRequisitos] ([Id]),
        CONSTRAINT [FK_ConstatacaoRequisitos_Constatacoes_ConstatacaoId] FOREIGN KEY ([ConstatacaoId]) REFERENCES [Constatacoes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Areas_Nome] ON [Areas] ([Nome]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Auditores_UsuarioId] ON [Auditores] ([UsuarioId]) WHERE [UsuarioId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_AuditoriaAreas_AreaId] ON [AuditoriaAreas] ([AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuditoriaAreas_AuditoriaId_AreaId] ON [AuditoriaAreas] ([AuditoriaId], [AreaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuditoriaMacroprocessos_AuditoriaId_MacroprocessoId] ON [AuditoriaMacroprocessos] ([AuditoriaId], [MacroprocessoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_AuditoriaMacroprocessos_MacroprocessoId] ON [AuditoriaMacroprocessos] ([MacroprocessoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuditoriaNormas_AuditoriaId_NormaId] ON [AuditoriaNormas] ([AuditoriaId], [NormaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_AuditoriaNormas_NormaId] ON [AuditoriaNormas] ([NormaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuditoriaParticipantes_AuditoriaId_AuditorId] ON [AuditoriaParticipantes] ([AuditoriaId], [AuditorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_AuditoriaParticipantes_AuditorId] ON [AuditoriaParticipantes] ([AuditorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuditoriaProcessos_AuditoriaId_ProcessoId] ON [AuditoriaProcessos] ([AuditoriaId], [ProcessoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_AuditoriaProcessos_ProcessoId] ON [AuditoriaProcessos] ([ProcessoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AuditoriaRequisitos_AuditoriaId_RequisitoId] ON [AuditoriaRequisitos] ([AuditoriaId], [RequisitoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_AuditoriaRequisitos_RequisitoId] ON [AuditoriaRequisitos] ([RequisitoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_Auditorias_Ano_MesPrevisto] ON [Auditorias] ([Ano], [MesPrevisto]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Auditorias_Codigo] ON [Auditorias] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Auditorias_Tipo_Ano_Sequencia] ON [Auditorias] ([Tipo], [Ano], [Sequencia]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_ConstatacaoRequisitos_AuditoriaRequisitoId] ON [ConstatacaoRequisitos] ([AuditoriaRequisitoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_Constatacoes_RelatorioId_Numero] ON [Constatacoes] ([RelatorioId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Habilitacoes_AuditorId_NormaId] ON [Habilitacoes] ([AuditorId], [NormaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_Habilitacoes_NormaId] ON [Habilitacoes] ([NormaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_Historico_DataHoraUtc] ON [Historico] ([DataHoraUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_Historico_Entidade_EntidadeId] ON [Historico] ([Entidade], [EntidadeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Macroprocessos_Nome] ON [Macroprocessos] ([Nome]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Normas_Nome] ON [Normas] ([Nome]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_PlanoPeriodos_PlanoId] ON [PlanoPeriodos] ([PlanoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Planos_AuditoriaId] ON [Planos] ([AuditoriaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProcessoNormas_NormaId] ON [ProcessoNormas] ([NormaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE INDEX [IX_ProcessoRequisitos_RequisitoId] ON [ProcessoRequisitos] ([RequisitoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Processos_MacroprocessoId_Nome] ON [Processos] ([MacroprocessoId], [Nome]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Relatorios_AuditoriaId] ON [Relatorios] ([AuditoriaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Requisitos_NormaId_Codigo] ON [Requisitos] ([NormaId], [Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Usuarios_NomeRede] ON [Usuarios] ([NomeRede]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261104021351_Inicial'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261104021351_Inicial', N'10.0.14');
END;

COMMIT;
GO

