using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Tests;

public class RegrasTestes
{
    [Fact]
    public void Ordenacao_numerica_por_segmentos()
    {
        var codigos = new[] { "10.1", "4.4.1", "5.1", "4.2.1", "4.1", "4.4", "4.3", "4.2" };
        var ordenados = OrdenacaoRequisitos.Ordenar(codigos, c => c).ToList();
        Assert.Equal(new[] { "4.1", "4.2", "4.2.1", "4.3", "4.4", "4.4.1", "5.1", "10.1" }, ordenados);
    }

    [Theory]
    [InlineData("4.1", true)]
    [InlineData("4.4.1", true)]
    [InlineData("10.3", true)]
    [InlineData("4", false)]
    [InlineData("4.4.1.1", false)]
    [InlineData("4.a", false)]
    [InlineData("", false)]
    public void Niveis_de_codigo_permitidos(string codigo, bool valido) =>
        Assert.Equal(valido, OrdenacaoRequisitos.CodigoValido(codigo));

    [Fact]
    public void Formato_do_codigo_da_auditoria()
    {
        Assert.Equal("AUDI-I-001-2027", CodigoAuditoria.Formatar(TipoAuditoria.Interna, 1, 2027));
        Assert.Equal("AUDI-F-012-2027", CodigoAuditoria.Formatar(TipoAuditoria.Fornecedor, 12, 2027));
        Assert.Equal("AUDI-I-1000-2027", CodigoAuditoria.Formatar(TipoAuditoria.Interna, 1000, 2027));
    }

    [Fact]
    public void Proxima_sequencia_usa_maior_existente_e_nao_preenche_lacunas()
    {
        Assert.Equal(1, CodigoAuditoria.ProximaSequencia(Array.Empty<int>()));
        Assert.Equal(6, CodigoAuditoria.ProximaSequencia(new[] { 1, 2, 5 }));
    }

    [Fact]
    public void Cobertura_exige_conformidade_ou_nao_conformidade()
    {
        // Plano: 4.1 (1), 4.2 (2), 8.4 (3). Conformidade: 4.1. NC: 8.4. Observação sobre 4.2 não conta.
        var r = RegrasRelatorio.Cobertura(new[] { 1, 2, 3 }, new[]
        {
            new ConstatacaoCobertura(ClassificacaoConstatacao.Conformidade, new[] { 1 }),
            new ConstatacaoCobertura(ClassificacaoConstatacao.NaoConformidade, new[] { 3 }),
            new ConstatacaoCobertura(ClassificacaoConstatacao.Observacao, new[] { 2 }),
            new ConstatacaoCobertura(ClassificacaoConstatacao.OportunidadeMelhoria, new[] { 2 }),
        });
        Assert.False(r.Completa);
        Assert.Equal(new[] { 2 }, r.Pendentes);
        Assert.Equal("2 de 3 requisitos classificados", r.Resumo);
    }

    [Fact]
    public void Pontuacao_lider_2_auditor_1_observador_0_sem_duplicar()
    {
        var parts = new[]
        {
            new ParticipacaoInfo(1, 10, FuncaoAuditoria.Lider),
            new ParticipacaoInfo(2, 10, FuncaoAuditoria.Auditor),
            new ParticipacaoInfo(3, 10, FuncaoAuditoria.Observador),
            new ParticipacaoInfo(3, 10, FuncaoAuditoria.Observador), // duplicidade não conta duas vezes
            new ParticipacaoInfo(1, 20, FuncaoAuditoria.Observador),
        };
        var r = RegrasPontuacao.Calcular(parts, new Dictionary<int, string> { [10] = "A", [20] = "B" });
        var a = r.Single(x => x.AuditorId == 10);
        Assert.Equal((1, 1, 1, 3, 3), (a.ComoLider, a.ComoAuditor, a.ComoObservador, a.Pontos, a.AuditoriasDistintas));
        var b = r.Single(x => x.AuditorId == 20);
        Assert.Equal(0, b.Pontos);
        Assert.Equal(1, b.AuditoriasDistintas);
    }

    [Fact]
    public void Requisitos_sugeridos_do_processo_filtrados_por_norma_e_situacao()
    {
        var p = new ProcessoInfo(1, "Compras", 1, new[] { 1, 2 }, new[] { new RequisitoProcessoInfo(100, 1, true), new RequisitoProcessoInfo(200, 2, true), new RequisitoProcessoInfo(101, 1, false) });
        Assert.Equal(new[] { 100 }, RegrasRequisitos.Sugeridos(p, new HashSet<int> { 1 })); // 200 é de norma não selecionada; 101 inativo
        Assert.Equal(new[] { 100, 200 }, RegrasRequisitos.Sugeridos(p, new HashSet<int> { 1, 2 }));
    }

    [Fact]
    public void Cores_do_calendario_respeitam_prioridade()
    {
        Assert.Equal(CorCartao.Cinza, RegrasCalendario.Cor(SituacaoAuditoria.Realizada, new[] { GrupoCorNorma.Asme }));
        Assert.Equal(CorCartao.Amarelo, RegrasCalendario.Cor(SituacaoAuditoria.Programada, new[] { GrupoCorNorma.Qualidade, GrupoCorNorma.Asme, GrupoCorNorma.NuclearNaval }));
        Assert.Equal(CorCartao.Laranja, RegrasCalendario.Cor(SituacaoAuditoria.Planejada, new[] { GrupoCorNorma.AmbienteSso, GrupoCorNorma.NuclearNaval }));
        Assert.Equal(CorCartao.Verde, RegrasCalendario.Cor(SituacaoAuditoria.EmRevisao, new[] { GrupoCorNorma.Qualidade, GrupoCorNorma.AmbienteSso }));
        Assert.Equal(CorCartao.Azul, RegrasCalendario.Cor(SituacaoAuditoria.Programada, new[] { GrupoCorNorma.Qualidade }));
    }

    [Fact]
    public void Periodos_do_plano_validam_horario_e_sobreposicao()
    {
        var d = new DateOnly(2027, 5, 12);
        PeriodoEdicao P(int h1, int h2, DateOnly? data = null) => new() { Data = data ?? d, HoraInicio = new TimeOnly(h1, 0), HoraFim = new TimeOnly(h2, 0) };
        Assert.Empty(PlanoServico.ValidarPeriodos(new[] { P(8, 12), P(13, 17), P(8, 12, d.AddDays(1)) }));
        Assert.Contains(PlanoServico.ValidarPeriodos(new[] { P(12, 8) }), e => e.Contains("posterior"));
        Assert.Contains(PlanoServico.ValidarPeriodos(new[] { P(8, 12), P(11, 14) }), e => e.Contains("sobrepõem"));
        Assert.Contains(PlanoServico.ValidarPeriodos(new[] { P(8, 12), P(8, 12) }), e => e.Contains("duplicados"));
        Assert.NotEmpty(PlanoServico.ValidarPeriodos(Array.Empty<PeriodoEdicao>()));
    }

    [Fact]
    public void Cnpj_valido()
    {
        Assert.True(Validacoes.CnpjValido("11.222.333/0001-81"));
        Assert.False(Validacoes.CnpjValido("11.222.333/0001-82"));
        Assert.False(Validacoes.CnpjValido("11111111111111"));
    }

    [Fact]
    public void Lider_precisa_de_habilitacao_em_todas_as_normas()
    {
        var a = new AuditorInfo(1, "A", true, new[] { new HabilitacaoInfo(1, true, true, false), new HabilitacaoInfo(2, false, true, false) });
        var normas = new[] { new NormaInfo(1, "ISO 9001"), new NormaInfo(2, "ISO 14001") };
        var c = RegrasHabilitacao.Verificar(a, FuncaoAuditoria.Lider, normas);
        Assert.False(c.Compativel);
        Assert.Contains("ISO 14001", c.Motivo);
        Assert.True(RegrasHabilitacao.Verificar(a, FuncaoAuditoria.Auditor, normas).Compativel);
        Assert.False(RegrasHabilitacao.Verificar(a, FuncaoAuditoria.Observador, normas).Compativel);
    }
}
