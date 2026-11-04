using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NuClaud.Core.Dados.Migracoes
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Auditorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<int>(type: "int", nullable: false),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Sequencia = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MesPrevisto = table.Column<int>(type: "int", nullable: false),
                    FornecedorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FornecedorCnpj = table.Column<string>(type: "nvarchar(18)", maxLength: 18, nullable: true),
                    FornecedorEndereco = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EscopoFornecimento = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfiguracoesVisuais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    LogoOriginal = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LogoTipoConteudo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    LogoPng = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    LogoLargura = table.Column<int>(type: "int", nullable: false),
                    LogoAltura = table.Column<int>(type: "int", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AtualizadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesVisuais", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Historico",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DataHoraUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    UsuarioNome = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Entidade = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EntidadeId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Resumo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DadosJson = table.Column<string>(type: "nvarchar(max)", maxLength: 1000000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historico", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Macroprocessos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Macroprocessos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Normas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Edicao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FonteCarga = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    GrupoCor = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Normas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NomeRede = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NomeCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    Perfil = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    SenhaHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CarimboSeguranca = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeveTrocarSenha = table.Column<bool>(type: "bit", nullable: false),
                    FalhasAcesso = table.Column<int>(type: "int", nullable: false),
                    BloqueadoAteUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimoAcessoUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaAreas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    AreaId = table.Column<int>(type: "int", nullable: false),
                    NomeArea = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaAreas_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaAreas_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Planos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    Local = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    GestorNome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GestorEmail = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    Objetivo = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Escopo = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Planos_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Relatorios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    Situacao = table.Column<int>(type: "int", nullable: false),
                    Qualificacao = table.Column<int>(type: "int", nullable: true),
                    ObservacoesConclusao = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    NumeroRqf = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ConcluidoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConcluidoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ConcluidoPorId = table.Column<int>(type: "int", nullable: true),
                    NumeroRevisao = table.Column<int>(type: "int", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relatorios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Relatorios_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaMacroprocessos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    MacroprocessoId = table.Column<int>(type: "int", nullable: false),
                    NomeMacroprocesso = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaMacroprocessos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaMacroprocessos_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriaMacroprocessos_Macroprocessos_MacroprocessoId",
                        column: x => x.MacroprocessoId,
                        principalTable: "Macroprocessos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MacroprocessoId = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processos_Macroprocessos_MacroprocessoId",
                        column: x => x.MacroprocessoId,
                        principalTable: "Macroprocessos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaNormas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    NormaId = table.Column<int>(type: "int", nullable: false),
                    NomeNorma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EdicaoNorma = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GrupoCor = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaNormas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaNormas_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriaNormas_Normas_NormaId",
                        column: x => x.NormaId,
                        principalTable: "Normas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Requisitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NormaId = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ChaveOrdenacao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requisitos_Normas_NormaId",
                        column: x => x.NormaId,
                        principalTable: "Normas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Auditores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NomeCompleto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true),
                    UsuarioId = table.Column<int>(type: "int", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    AlteradoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AlteradoPor = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auditores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auditores_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PlanoPeriodos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanoId = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false),
                    HoraInicio = table.Column<TimeOnly>(type: "time", nullable: false),
                    HoraFim = table.Column<TimeOnly>(type: "time", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanoPeriodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanoPeriodos_Planos_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "Planos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Constatacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RelatorioId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Classificacao = table.Column<int>(type: "int", nullable: false),
                    Evidencia = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    DescricaoNaoConformidade = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    EvidenciasObjetivas = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    NumeroPac = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true),
                    OportunidadeMelhoria = table.Column<string>(type: "nvarchar(max)", maxLength: 20000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Constatacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Constatacoes_Relatorios_RelatorioId",
                        column: x => x.RelatorioId,
                        principalTable: "Relatorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaProcessos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    ProcessoId = table.Column<int>(type: "int", nullable: false),
                    NomeProcesso = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NomeMacroprocesso = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaProcessos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaProcessos_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriaProcessos_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessoNormas",
                columns: table => new
                {
                    ProcessoId = table.Column<int>(type: "int", nullable: false),
                    NormaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessoNormas", x => new { x.ProcessoId, x.NormaId });
                    table.ForeignKey(
                        name: "FK_ProcessoNormas_Normas_NormaId",
                        column: x => x.NormaId,
                        principalTable: "Normas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessoNormas_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaRequisitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    RequisitoId = table.Column<int>(type: "int", nullable: false),
                    NormaId = table.Column<int>(type: "int", nullable: false),
                    NomeNorma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ChaveOrdenacao = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Obrigatorio = table.Column<bool>(type: "bit", nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaRequisitos_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditoriaRequisitos_Requisitos_RequisitoId",
                        column: x => x.RequisitoId,
                        principalTable: "Requisitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessoRequisitos",
                columns: table => new
                {
                    ProcessoId = table.Column<int>(type: "int", nullable: false),
                    RequisitoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessoRequisitos", x => new { x.ProcessoId, x.RequisitoId });
                    table.ForeignKey(
                        name: "FK_ProcessoRequisitos_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessoRequisitos_Requisitos_RequisitoId",
                        column: x => x.RequisitoId,
                        principalTable: "Requisitos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuditoriaParticipantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditoriaId = table.Column<int>(type: "int", nullable: false),
                    AuditorId = table.Column<int>(type: "int", nullable: false),
                    Funcao = table.Column<int>(type: "int", nullable: false),
                    NomeAuditor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EmailAuditor = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriaParticipantes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditoriaParticipantes_Auditores_AuditorId",
                        column: x => x.AuditorId,
                        principalTable: "Auditores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuditoriaParticipantes_Auditorias_AuditoriaId",
                        column: x => x.AuditoriaId,
                        principalTable: "Auditorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Habilitacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AuditorId = table.Column<int>(type: "int", nullable: false),
                    NormaId = table.Column<int>(type: "int", nullable: false),
                    PodeLiderar = table.Column<bool>(type: "bit", nullable: false),
                    PodeAuditar = table.Column<bool>(type: "bit", nullable: false),
                    PodeObservar = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Habilitacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Habilitacoes_Auditores_AuditorId",
                        column: x => x.AuditorId,
                        principalTable: "Auditores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Habilitacoes_Normas_NormaId",
                        column: x => x.NormaId,
                        principalTable: "Normas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConstatacaoRequisitos",
                columns: table => new
                {
                    ConstatacaoId = table.Column<int>(type: "int", nullable: false),
                    AuditoriaRequisitoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstatacaoRequisitos", x => new { x.ConstatacaoId, x.AuditoriaRequisitoId });
                    table.ForeignKey(
                        name: "FK_ConstatacaoRequisitos_AuditoriaRequisitos_AuditoriaRequisitoId",
                        column: x => x.AuditoriaRequisitoId,
                        principalTable: "AuditoriaRequisitos",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConstatacaoRequisitos_Constatacoes_ConstatacaoId",
                        column: x => x.ConstatacaoId,
                        principalTable: "Constatacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Nome",
                table: "Areas",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditores_UsuarioId",
                table: "Auditores",
                column: "UsuarioId",
                unique: true,
                filter: "[UsuarioId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAreas_AreaId",
                table: "AuditoriaAreas",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaAreas_AuditoriaId_AreaId",
                table: "AuditoriaAreas",
                columns: new[] { "AuditoriaId", "AreaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaMacroprocessos_AuditoriaId_MacroprocessoId",
                table: "AuditoriaMacroprocessos",
                columns: new[] { "AuditoriaId", "MacroprocessoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaMacroprocessos_MacroprocessoId",
                table: "AuditoriaMacroprocessos",
                column: "MacroprocessoId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaNormas_AuditoriaId_NormaId",
                table: "AuditoriaNormas",
                columns: new[] { "AuditoriaId", "NormaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaNormas_NormaId",
                table: "AuditoriaNormas",
                column: "NormaId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaParticipantes_AuditoriaId_AuditorId",
                table: "AuditoriaParticipantes",
                columns: new[] { "AuditoriaId", "AuditorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaParticipantes_AuditorId",
                table: "AuditoriaParticipantes",
                column: "AuditorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaProcessos_AuditoriaId_ProcessoId",
                table: "AuditoriaProcessos",
                columns: new[] { "AuditoriaId", "ProcessoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaProcessos_ProcessoId",
                table: "AuditoriaProcessos",
                column: "ProcessoId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaRequisitos_AuditoriaId_RequisitoId",
                table: "AuditoriaRequisitos",
                columns: new[] { "AuditoriaId", "RequisitoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditoriaRequisitos_RequisitoId",
                table: "AuditoriaRequisitos",
                column: "RequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Ano_MesPrevisto",
                table: "Auditorias",
                columns: new[] { "Ano", "MesPrevisto" });

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Codigo",
                table: "Auditorias",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Auditorias_Tipo_Ano_Sequencia",
                table: "Auditorias",
                columns: new[] { "Tipo", "Ano", "Sequencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstatacaoRequisitos_AuditoriaRequisitoId",
                table: "ConstatacaoRequisitos",
                column: "AuditoriaRequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_Constatacoes_RelatorioId_Numero",
                table: "Constatacoes",
                columns: new[] { "RelatorioId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_Habilitacoes_AuditorId_NormaId",
                table: "Habilitacoes",
                columns: new[] { "AuditorId", "NormaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Habilitacoes_NormaId",
                table: "Habilitacoes",
                column: "NormaId");

            migrationBuilder.CreateIndex(
                name: "IX_Historico_DataHoraUtc",
                table: "Historico",
                column: "DataHoraUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Historico_Entidade_EntidadeId",
                table: "Historico",
                columns: new[] { "Entidade", "EntidadeId" });

            migrationBuilder.CreateIndex(
                name: "IX_Macroprocessos_Nome",
                table: "Macroprocessos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Normas_Nome",
                table: "Normas",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlanoPeriodos_PlanoId",
                table: "PlanoPeriodos",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_Planos_AuditoriaId",
                table: "Planos",
                column: "AuditoriaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessoNormas_NormaId",
                table: "ProcessoNormas",
                column: "NormaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessoRequisitos_RequisitoId",
                table: "ProcessoRequisitos",
                column: "RequisitoId");

            migrationBuilder.CreateIndex(
                name: "IX_Processos_MacroprocessoId_Nome",
                table: "Processos",
                columns: new[] { "MacroprocessoId", "Nome" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Relatorios_AuditoriaId",
                table: "Relatorios",
                column: "AuditoriaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Requisitos_NormaId_Codigo",
                table: "Requisitos",
                columns: new[] { "NormaId", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NomeRede",
                table: "Usuarios",
                column: "NomeRede",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriaAreas");

            migrationBuilder.DropTable(
                name: "AuditoriaMacroprocessos");

            migrationBuilder.DropTable(
                name: "AuditoriaNormas");

            migrationBuilder.DropTable(
                name: "AuditoriaParticipantes");

            migrationBuilder.DropTable(
                name: "AuditoriaProcessos");

            migrationBuilder.DropTable(
                name: "ConfiguracoesVisuais");

            migrationBuilder.DropTable(
                name: "ConstatacaoRequisitos");

            migrationBuilder.DropTable(
                name: "Habilitacoes");

            migrationBuilder.DropTable(
                name: "Historico");

            migrationBuilder.DropTable(
                name: "PlanoPeriodos");

            migrationBuilder.DropTable(
                name: "ProcessoNormas");

            migrationBuilder.DropTable(
                name: "ProcessoRequisitos");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropTable(
                name: "AuditoriaRequisitos");

            migrationBuilder.DropTable(
                name: "Constatacoes");

            migrationBuilder.DropTable(
                name: "Auditores");

            migrationBuilder.DropTable(
                name: "Planos");

            migrationBuilder.DropTable(
                name: "Processos");

            migrationBuilder.DropTable(
                name: "Requisitos");

            migrationBuilder.DropTable(
                name: "Relatorios");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Macroprocessos");

            migrationBuilder.DropTable(
                name: "Normas");

            migrationBuilder.DropTable(
                name: "Auditorias");
        }
    }
}
