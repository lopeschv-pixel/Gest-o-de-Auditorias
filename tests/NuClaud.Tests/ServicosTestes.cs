using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dominio;
using NuClaud.Core.Pdf;
using NuClaud.Core.Servicos;

namespace NuClaud.Tests;

public class ProgramacaoTestes
{
    [Fact]
    public async Task Codigos_sequenciais_por_tipo_e_ano_e_reinicio_apos_excluir_todas()
    {
        using var amb = new Ambiente();
        var p = amb.Programacao;
        var i1 = await p.SalvarAsync(amb.Admin, amb.Interna());
        var i2 = await p.SalvarAsync(amb.Admin, amb.Interna());
        var f1 = await p.SalvarAsync(amb.Admin, amb.Fornecedor());
        var i2028 = await p.SalvarAsync(amb.Admin, amb.Interna(2028));
        string Cod(int id) => p.ObterDetalheAsync(amb.Admin, id).Result.Codigo;
        Assert.Equal("AUDI-I-001-2027", Cod(i1));
        Assert.Equal("AUDI-I-002-2027", Cod(i2));
        Assert.Equal("AUDI-F-001-2027", Cod(f1));
        Assert.Equal("AUDI-I-001-2028", Cod(i2028));

        // Exclui a 001: a próxima é 003 (não preenche lacuna enquanto existe número maior; não renumera).
        await p.ExcluirAsync(amb.Admin, i1);
        var i3 = await p.SalvarAsync(amb.Admin, amb.Interna());
        Assert.Equal("AUDI-I-003-2027", Cod(i3));
        Assert.Equal("AUDI-I-002-2027", Cod(i2));

        // Exclui todas as internas de 2027: a próxima volta a 001.
        await p.ExcluirAsync(amb.Admin, i2);
        await p.ExcluirAsync(amb.Admin, i3);
        var novo = await p.SalvarAsync(amb.Admin, amb.Interna());
        Assert.Equal("AUDI-I-001-2027", Cod(novo));
        Assert.NotEqual(i1, novo); // identificador interno é independente do código
    }

    [Fact]
    public async Task Somente_administrador_programa_altera_e_exclui()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        var usuario = await amb.UsuarioVinculado("lider1", amb.AudLider1);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Programacao.SalvarAsync(usuario, amb.Interna()));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Programacao.ExcluirAsync(usuario, id));
        var ed = (await amb.Programacao.ObterParaEdicaoAsync(amb.Admin, id)).Dados;
        ed.MesPrevisto = 9;
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Programacao.SalvarAsync(usuario, ed));
    }

    [Fact]
    public async Task Carrega_obrigatorios_dos_processos_e_impede_retirada()
    {
        using var amb = new Ambiente();
        var p = amb.Interna();
        p.NormaIds = new() { amb.Iso9001, amb.Iso14001 };
        p.ProcessoIds = new() { amb.ProcCompras, amb.ProcRecebimento };
        p.RequisitoIds = new() { amb.Req(amb.Iso9001, "4.1") }; // adicional; faltam obrigatórios
        var ex = await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Programacao.SalvarAsync(amb.Admin, p));
        Assert.Contains(ex.Erros, e => e.Contains("8.4") && e.Contains("obrigatório"));

        var dados = await amb.Programacao.ObterDadosFormularioAsync(amb.Admin);
        var obrig = RegrasRequisitos.Obrigatorios(dados.Processos.Where(x => p.ProcessoIds.Contains(x.Id)), p.NormaIds);
        p.RequisitoIds.UnionWith(obrig.Keys);
        var id = await amb.Programacao.SalvarAsync(amb.Admin, p);
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Equal(new[] { "4.1", "8.1", "8.4", "8.4.1", "8.6" }, a.Requisitos.Select(r => r.Codigo).OrderBy(c => c).ToArray());
        Assert.Equal(1, a.Requisitos.Count(r => r.Codigo == "8.4")); // sem duplicação entre processos
        var r84 = a.Requisitos.Single(r => r.Codigo == "8.4");
        Assert.True(r84.Obrigatorio);
        Assert.Contains("Compras", r84.Origem);
        Assert.Contains("Recebimento", r84.Origem);
        Assert.False(a.Requisitos.Single(r => r.Codigo == "4.1").Obrigatorio);
    }

    [Fact]
    public async Task Alterar_processo_nao_reescreve_auditoria_programada()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        var proc = (await amb.Cadastro.ListarProcessosAsync()).Single(x => x.Id == amb.ProcRecebimento);
        await amb.Cadastro.SalvarProcessoAsync(amb.Admin, new ProcessoEdicao
        {
            Id = proc.Id, Versao = proc.Versao, Nome = "Recebimento", MacroprocessoId = amb.Macro, NormaIds = { amb.Iso9001 },
            RequisitoIds = { amb.Req(amb.Iso9001, "8.7") }
        });
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Equal(new[] { "8.4", "8.6" }, a.Requisitos.Select(r => r.Codigo).OrderBy(c => c).ToArray());
        // Editar outro campo da programação mantém os obrigatórios registrados.
        var ed = (await amb.Programacao.ObterParaEdicaoAsync(amb.Admin, id)).Dados;
        ed.MesPrevisto = 11;
        await amb.Programacao.SalvarAsync(amb.Admin, ed);
        a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.True(a.Requisitos.Single(r => r.Codigo == "8.6").Obrigatorio);
    }

    [Fact]
    public async Task Fornecedor_nao_salva_area_nem_processo()
    {
        using var amb = new Ambiente();
        var p = amb.Fornecedor();
        p.AreaIds.Add(amb.AreaA);
        p.ProcessoIds.Add(amb.ProcCompras);
        var id = await amb.Programacao.SalvarAsync(amb.Admin, p);
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Empty(a.Areas);
        Assert.Empty(a.Processos);
        Assert.Empty(a.Macroprocessos);
        Assert.Equal("Fornecedor X", a.FornecedorNome);
        Assert.Equal("11.222.333/0001-81", a.FornecedorCnpj);
    }

    [Fact]
    public async Task Habilitacoes_e_duplicidades_sao_validadas()
    {
        using var amb = new Ambiente();
        var p = amb.Interna(lider: amb.AudAuditor); // sem habilitação de líder
        var ex = await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Programacao.SalvarAsync(amb.Admin, p));
        Assert.Contains(ex.Erros, e => e.Contains("Auditor Três") && e.Contains("líder"));

        p = amb.Interna();
        p.ObservadorIds.Add(amb.AudSemHab);
        ex = await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Programacao.SalvarAsync(amb.Admin, p));
        Assert.Contains(ex.Erros, e => e.Contains("Sem Habilitação"));

        p = amb.Interna();
        p.ObservadorIds.Add(amb.AudAuditor); // auditor e observador ao mesmo tempo
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Programacao.SalvarAsync(amb.Admin, p));

        // Líder repetido em Auditores é retirado (uma pessoa por função).
        p = amb.Interna();
        p.AuditorIds.Add(amb.AudLider1);
        var id = await amb.Programacao.SalvarAsync(amb.Admin, p);
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Single(a.Participantes, x => x.AuditorId == amb.AudLider1);
    }

    [Fact]
    public async Task Editar_auditor_preserva_participacoes_e_retrato()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        await amb.Planos.SalvarAsync(amb.Admin, new PlanoEdicao { AuditoriaId = id, Periodos = { new() { Data = new DateOnly(2027, 5, 3), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(9, 0) } } });
        await amb.Relatorios.IniciarAsync(amb.Admin, id);
        var aud = (await amb.Auditores.ListarAsync()).Single(a => a.Id == amb.AudAuditor);
        await amb.Auditores.SalvarAsync(amb.Admin, new AuditorEdicao { Id = aud.Id, Versao = aud.Versao, NomeCompleto = "Nome Novo", Ativo = false });
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Contains(a.Participantes, x => x.AuditorId == amb.AudAuditor && x.NomeAuditor == "Auditor Três");
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Auditores.ExcluirAsync(amb.Admin, amb.AudAuditor));
    }

    [Fact]
    public async Task Area_usada_nao_pode_ser_excluida_e_renomear_preserva_historico()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        await amb.Relatorios.IniciarAsync(amb.Admin, (await amb.ComPlano(amb.Interna())));
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Cadastro.ExcluirAreaAsync(amb.Admin, amb.AreaA));
        var area = (await amb.Cadastro.ListarAreasAsync()).Single(a => a.Id == amb.AreaA);
        await amb.Cadastro.SalvarAreaAsync(amb.Admin, new AreaEdicao { Id = area.Id, Versao = area.Versao, Nome = "Suprimentos e Logística" });
        // A auditoria com relatório iniciado mantém o nome histórico.
        var lista = await amb.Programacao.ListarAsync(amb.Admin, 2027);
        Assert.Contains(lista, x => x.Identificacao == "Suprimentos");
        var nova = await amb.Programacao.ObterDadosFormularioAsync(amb.Admin);
        Assert.Contains(nova.Areas, a => a.Nome == "Suprimentos e Logística");
    }

    [Fact]
    public async Task Estrutura_bloqueada_apos_inicio_do_relatorio()
    {
        using var amb = new Ambiente();
        var id = await amb.ComPlano(amb.Interna());
        await amb.Relatorios.IniciarAsync(amb.Admin, id);
        var ed = (await amb.Programacao.ObterParaEdicaoAsync(amb.Admin, id)).Dados;
        ed.RequisitoIds.Add(amb.Req(amb.Iso9001, "4.1"));
        var ex = await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Programacao.SalvarAsync(amb.Admin, ed));
        Assert.Contains(ex.Erros, e => e.Contains("relatório"));
        ed = (await amb.Programacao.ObterParaEdicaoAsync(amb.Admin, id)).Dados;
        ed.MesPrevisto = 12; // mês pode mudar
        await amb.Programacao.SalvarAsync(amb.Admin, ed);
    }

    [Fact]
    public async Task Concorrencia_otimista_impede_sobrescrita_silenciosa()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        var a = (await amb.Programacao.ObterParaEdicaoAsync(amb.Admin, id)).Dados;
        var b = (await amb.Programacao.ObterParaEdicaoAsync(amb.Admin, id)).Dados;
        a.MesPrevisto = 7;
        await amb.Programacao.SalvarAsync(amb.Admin, a);
        b.MesPrevisto = 8;
        await Assert.ThrowsAsync<ConcorrenciaException>(() => amb.Programacao.SalvarAsync(amb.Admin, b));
    }
}

public class PermissoesTestes
{
    [Fact]
    public async Task Usuario_sem_participacao_nao_acessa_auditoria_por_identificador()
    {
        using var amb = new Ambiente();
        var id = await amb.ComPlano(amb.Interna());
        var outro = await amb.UsuarioVinculado("lider2", amb.AudLider2);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Programacao.ObterDetalheAsync(outro, id));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Planos.SalvarAsync(outro, new PlanoEdicao { AuditoriaId = id }));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Relatorios.IniciarAsync(outro, id));
        var cal = await amb.Consultas.CalendarioAsync(outro, new FiltroCalendario { Ano = 2027 });
        Assert.Empty(cal);
    }

    [Fact]
    public async Task Auditor_nao_lider_visualiza_mas_nao_edita()
    {
        using var amb = new Ambiente();
        var id = await amb.ComPlano(amb.Interna());
        var auditor = await amb.UsuarioVinculado("aud3", amb.AudAuditor);
        var a = await amb.Programacao.ObterDetalheAsync(auditor, id);
        Assert.False(Permissoes.PodeEditarPlano(auditor, a));
        var ed = PlanoServico.ParaEdicao(a);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Planos.SalvarAsync(auditor, ed));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Relatorios.IniciarAsync(auditor, id));
    }

    [Fact]
    public async Task Lider_altera_somente_datas_e_horarios_do_plano()
    {
        using var amb = new Ambiente();
        var id = await amb.ComPlano(amb.Interna());
        var lider = await amb.UsuarioVinculado("lider1", amb.AudLider1);
        var a = await amb.Programacao.ObterDetalheAsync(lider, id);
        var ed = PlanoServico.ParaEdicao(a);
        ed.Periodos.Add(new PeriodoEdicao { Data = new DateOnly(2027, 5, 11), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(10, 0) });
        await amb.Planos.SalvarAsync(lider, ed);
        ed = PlanoServico.ParaEdicao(await amb.Programacao.ObterDetalheAsync(lider, id));
        Assert.Equal(2, ed.Periodos.Count);
        ed.Local = "Outro local";
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Planos.SalvarAsync(lider, ed));
        // Administrador altera os demais campos.
        await amb.Planos.SalvarAsync(amb.Admin, ed);
        Assert.Equal("Outro local", (await amb.Programacao.ObterDetalheAsync(amb.Admin, id)).Plano!.Local);
    }

    [Fact]
    public async Task Objetivo_e_escopo_somente_para_fornecedor()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        await amb.Planos.SalvarAsync(amb.Admin, new PlanoEdicao
        {
            AuditoriaId = id, Objetivo = "x", Escopo = "y",
            Periodos = { new() { Data = new DateOnly(2027, 5, 3), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(9, 0) } }
        });
        var p = (await amb.Programacao.ObterDetalheAsync(amb.Admin, id)).Plano!;
        Assert.Null(p.Objetivo);
        Assert.Null(p.Escopo);
    }

    [Fact]
    public async Task Ultimo_administrador_ativo_e_protegido()
    {
        using var amb = new Ambiente();
        var adminId = await amb.Usuarios.SalvarAsync(amb.Admin, new UsuarioEdicao
        {
            NomeRede = "chefe", NomeCompleto = "Chefe", Email = "chefe@exemplo.invalid", Perfil = PerfilUsuario.Administrador, SenhaInicial = "SenhaTeste123"
        });
        var u = (await amb.Usuarios.ListarAsync(amb.Admin)).Single(x => x.Id == adminId);
        var ed = new UsuarioEdicao { Id = u.Id, Versao = u.Versao, NomeRede = u.NomeRede, NomeCompleto = u.NomeCompleto, Email = u.Email, Perfil = PerfilUsuario.Usuario, Ativo = true };
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Usuarios.SalvarAsync(amb.Admin, ed));
        ed.Perfil = PerfilUsuario.Administrador; ed.Ativo = false;
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Usuarios.SalvarAsync(amb.Admin, ed));
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Usuarios.ExcluirAsync(amb.Admin, adminId));
        // Com outro administrador ativo, a alteração é permitida.
        await amb.Usuarios.SalvarAsync(amb.Admin, new UsuarioEdicao { NomeRede = "chefe2", NomeCompleto = "Chefe 2", Email = "c2@exemplo.invalid", Perfil = PerfilUsuario.Administrador, SenhaInicial = "SenhaTeste123" });
        await amb.Usuarios.SalvarAsync(amb.Admin, ed);
    }

    [Fact]
    public async Task Usuario_comum_nao_executa_operacoes_administrativas()
    {
        using var amb = new Ambiente();
        var u = await amb.UsuarioVinculado("comum", amb.AudAuditor);
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Cadastro.SalvarAreaAsync(u, new AreaEdicao { Nome = "X" }));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Usuarios.ListarAsync(u));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Normas.SalvarRequisitoAsync(u, new RequisitoEdicao { NormaId = amb.Iso9001, Codigo = "1.1", Titulo = "x" }));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Auditores.SalvarAsync(u, new AuditorEdicao { NomeCompleto = "x" }));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Consultas.RankingAsync(u, 2027));
    }

    [Fact]
    public async Task Autenticacao_com_senha_com_hash_e_bloqueio()
    {
        using var amb = new Ambiente();
        await amb.UsuarioVinculado("joao", amb.AudAuditor);
        using (var db = amb.Fabrica.CreateDbContext())
        {
            var u = db.Usuarios.Single(x => x.NomeRede == "joao");
            Assert.DoesNotContain("SenhaTeste123", u.SenhaHash);
        }
        Assert.True((await amb.Usuarios.AutenticarAsync("JOAO", "SenhaTeste123")).Sucesso);
        for (int i = 0; i < UsuarioServico.TentativasAntesBloqueio; i++)
            Assert.False((await amb.Usuarios.AutenticarAsync("joao", "errada")).Sucesso);
        var r = await amb.Usuarios.AutenticarAsync("joao", "SenhaTeste123");
        Assert.False(r.Sucesso);
        Assert.Contains("bloqueado", r.Mensagem);
    }
}

public class RelatorioTestes
{
    private static async Task<(Ambiente amb, int id, Auditoria a)> Preparar(ProgramacaoEdicao? p = null)
    {
        var amb = new Ambiente();
        var id = await amb.ComPlano(p ?? amb.Interna());
        await amb.Relatorios.IniciarAsync(amb.Admin, id);
        return (amb, id, await amb.Programacao.ObterDetalheAsync(amb.Admin, id));
    }

    private static int R(Auditoria a, string codigo) => a.Requisitos.Single(r => r.Codigo == codigo).Id;

    [Fact]
    public async Task Bloqueia_conclusao_com_requisito_pendente_e_preserva_dados()
    {
        var (amb, id, a) = await Preparar();
        using var _ = amb;
        var dto = RelatorioServico.ParaEdicao(a);
        dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade, AuditoriaRequisitoIds = { R(a, "8.4") }, Evidencia = "ok" });
        dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Observacao, Observacao = "obs sobre 8.6" });
        var r = await amb.Relatorios.ConcluirAsync(amb.Admin, dto);
        Assert.False(r.Concluido);
        var pend = Assert.Single(r.Pendentes);
        Assert.Equal("8.6", pend.Codigo);
        Assert.Equal("Liberação de produtos e serviços", pend.Titulo);
        Assert.Equal("1 de 2 requisitos classificados", r.Cobertura.Resumo);
        var salvo = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Equal(SituacaoRelatorio.EmElaboracao, salvo.Relatorio!.Situacao);
        Assert.Equal(2, salvo.Relatorio.Constatacoes.Count);
    }

    [Fact]
    public async Task Conclui_e_bloqueia_usuario_comum_e_admin_reabre_com_historico()
    {
        var (amb, id, a) = await Preparar();
        using var _ = amb;
        var lider = await amb.UsuarioVinculado("lider1", amb.AudLider1);
        var dto = RelatorioServico.ParaEdicao(a);
        dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade, AuditoriaRequisitoIds = { R(a, "8.4") }, Evidencia = "ok" });
        dto.Constatacoes.Add(new ConstatacaoEdicao
        {
            Classificacao = ClassificacaoConstatacao.NaoConformidade, AuditoriaRequisitoIds = { R(a, "8.6"), R(a, "8.4") },
            DescricaoNaoConformidade = "nc", EvidenciasObjetivas = "ev", NumeroPac = "PAC-1"
        });
        var r = await amb.Relatorios.ConcluirAsync(lider, dto);
        Assert.True(r.Concluido);

        var c = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Equal(SituacaoAuditoria.Realizada, c.Situacao);
        Assert.Equal(CorCartao.Cinza, RegrasCalendario.Cor(c.Situacao, c.Normas.Select(n => n.GrupoCor)));
        Assert.Contains("lider1", c.Relatorio!.ConcluidoPor);
        Assert.Equal(2, c.Relatorio.Constatacoes.Single(x => x.Classificacao == ClassificacaoConstatacao.NaoConformidade).Requisitos.Count);

        // Após conclusão: líder não altera relatório nem plano.
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Relatorios.SalvarAsync(lider, RelatorioServico.ParaEdicao(c)));
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Planos.SalvarAsync(lider, PlanoServico.ParaEdicao(c)));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Relatorios.ReabrirAsync(lider, id, "x"));

        // Correção administrativa: reabre para revisão, exige nova conclusão e registra histórico.
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Relatorios.ReabrirAsync(amb.Admin, id, " "));
        await amb.Relatorios.ReabrirAsync(amb.Admin, id, "Corrigir evidência");
        c = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        Assert.Equal(SituacaoAuditoria.EmRevisao, c.Situacao);
        Assert.NotEqual(CorCartao.Cinza, RegrasCalendario.Cor(c.Situacao, c.Normas.Select(n => n.GrupoCor)));
        await Assert.ThrowsAsync<AcessoNegadoException>(() => amb.Relatorios.SalvarAsync(lider, RelatorioServico.ParaEdicao(c)));
        var ed = RelatorioServico.ParaEdicao(c);
        ed.Constatacoes[0].Evidencia = "evidência corrigida";
        Assert.True((await amb.Relatorios.ConcluirAsync(amb.Admin, ed)).Concluido);
        var hist = await amb.Consultas.HistoricoAuditoriaAsync(amb.Admin, c);
        Assert.Contains(hist, h => h.Acao == "Reabertura para revisão" && h.DadosJson!.Contains("versaoConcluidaAnterior"));
        Assert.Equal(2, hist.Count(h => h.Acao == "Conclusão"));
    }

    [Fact]
    public async Task Campos_por_classificacao_e_descarte_de_ocultos()
    {
        var (amb, id, a) = await Preparar();
        using var _ = amb;
        var dto = RelatorioServico.ParaEdicao(a);
        dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade, AuditoriaRequisitoIds = { R(a, "8.4") } });
        var ex = await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Relatorios.SalvarAsync(amb.Admin, dto));
        Assert.Contains(ex.Erros, e => e.Contains("Evidência encontrada"));

        var c = new ConstatacaoEdicao
        {
            Classificacao = ClassificacaoConstatacao.Observacao, Observacao = "texto", Evidencia = "sobra", NumeroPac = "PAC", AuditoriaRequisitoIds = { R(a, "8.4") }
        };
        Assert.Equal(new[] { "Evidência encontrada", "Número do PAC", "Requisitos selecionados" }, c.CamposQueSeraoDescartados(ClassificacaoConstatacao.Observacao));
        dto.Constatacoes.Clear();
        dto.Constatacoes.Add(c);
        await amb.Relatorios.SalvarAsync(amb.Admin, dto);
        var salvo = (await amb.Programacao.ObterDetalheAsync(amb.Admin, id)).Relatorio!.Constatacoes.Single();
        Assert.Null(salvo.Evidencia);
        Assert.Null(salvo.NumeroPac);
        Assert.Empty(salvo.Requisitos);
        Assert.Equal("texto", salvo.Observacao);
        Assert.Equal("texto", c.Observacao); // o objeto da interface não é alterado pelo serviço
        Assert.Equal("sobra", c.Evidencia);
    }

    [Fact]
    public async Task Editar_e_excluir_constatacoes_renumera()
    {
        var (amb, id, a) = await Preparar();
        using var _ = amb;
        var dto = RelatorioServico.ParaEdicao(a);
        for (int i = 0; i < 3; i++) dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.OportunidadeMelhoria, OportunidadeMelhoria = $"om{i}" });
        await amb.Relatorios.SalvarAsync(amb.Admin, dto);
        var ed = RelatorioServico.ParaEdicao(await amb.Programacao.ObterDetalheAsync(amb.Admin, id));
        ed.Constatacoes.RemoveAt(0);
        ed.Constatacoes[0].OportunidadeMelhoria = "editada";
        await amb.Relatorios.SalvarAsync(amb.Admin, ed);
        var c = (await amb.Programacao.ObterDetalheAsync(amb.Admin, id)).Relatorio!.Constatacoes;
        Assert.Equal(new[] { 1, 2 }, c.Select(x => x.Numero));
        Assert.Equal(new[] { "editada", "om2" }, c.Select(x => x.OportunidadeMelhoria));
    }

    [Fact]
    public async Task Fornecedor_exige_qualificacao_e_interna_nao_guarda_rqf()
    {
        var (amb, id, a) = await Preparar(new Ambiente().Fornecedor());
        using var _ = amb;
        var dto = RelatorioServico.ParaEdicao(a);
        dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade, AuditoriaRequisitoIds = { R(a, "8.4") }, Evidencia = "ok" });
        var r = await amb.Relatorios.ConcluirAsync(amb.Admin, dto);
        Assert.False(r.Concluido);
        Assert.Contains(r.Erros, e => e.Contains("qualificação"));
        dto.Qualificacao = QualificacaoFornecedor.QualificadoTresAnos;
        dto.NumeroRqf = "RQF-77";
        dto.Versao = (await amb.Programacao.ObterDetalheAsync(amb.Admin, id)).Relatorio!.Versao;
        dto.Constatacoes = RelatorioServico.ParaEdicao(await amb.Programacao.ObterDetalheAsync(amb.Admin, id)).Constatacoes;
        Assert.True((await amb.Relatorios.ConcluirAsync(amb.Admin, dto)).Concluido);

        var id2 = await amb.ComPlano(amb.Interna());
        await amb.Relatorios.IniciarAsync(amb.Admin, id2);
        var a2 = await amb.Programacao.ObterDetalheAsync(amb.Admin, id2);
        var d2 = RelatorioServico.ParaEdicao(a2);
        d2.NumeroRqf = "RQF-1"; d2.Qualificacao = QualificacaoFornecedor.NaoQualificado; d2.ObservacoesConclusao = "x";
        await amb.Relatorios.SalvarAsync(amb.Admin, d2);
        var rel2 = (await amb.Programacao.ObterDetalheAsync(amb.Admin, id2)).Relatorio!;
        Assert.Null(rel2.NumeroRqf);
        Assert.Null(rel2.Qualificacao);
        Assert.Null(rel2.ObservacoesConclusao);
    }

    [Fact]
    public async Task Relatorio_exige_plano_salvo()
    {
        using var amb = new Ambiente();
        var id = await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna());
        await Assert.ThrowsAsync<RegraNegocioException>(() => amb.Relatorios.IniciarAsync(amb.Admin, id));
    }
}

public class ConsultasTestes
{
    [Fact]
    public async Task Ranking_conta_programadas_no_ano_com_pontuacao()
    {
        using var amb = new Ambiente();
        var p1 = amb.Interna(); p1.ObservadorIds.Add(amb.AudObservador);
        await amb.Programacao.SalvarAsync(amb.Admin, p1);
        await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna(lider: amb.AudLider2));
        await amb.Programacao.SalvarAsync(amb.Admin, amb.Interna(2028));
        var r = await amb.Consultas.RankingAsync(amb.Admin, 2027);
        var l1 = r.Single(x => x.AuditorId == amb.AudLider1);
        Assert.Equal((1, 2), (l1.ComoLider, l1.Pontos));
        var aud = r.Single(x => x.AuditorId == amb.AudAuditor);
        Assert.Equal((2, 2, 2), (aud.ComoAuditor, aud.Pontos, aud.AuditoriasDistintas));
        var obs = r.Single(x => x.AuditorId == amb.AudObservador);
        Assert.Equal((1, 0), (obs.ComoObservador, obs.Pontos));
    }

    [Fact]
    public async Task Filtro_de_normas_nao_duplica_e_busca_por_texto()
    {
        using var amb = new Ambiente();
        var p = amb.Interna(); p.NormaIds.Add(amb.Iso14001);
        p.LiderId = amb.AudLider1;
        await amb.Programacao.SalvarAsync(amb.Admin, p);
        await amb.Programacao.SalvarAsync(amb.Admin, amb.Fornecedor());
        var todas = await amb.Consultas.CalendarioAsync(amb.Admin, new FiltroCalendario { Ano = 2027 });
        Assert.Equal(2, todas.Count);
        var duas = await amb.Consultas.CalendarioAsync(amb.Admin, new FiltroCalendario { Ano = 2027, NormaIds = { amb.Iso9001, amb.Iso14001 } });
        Assert.Equal(2, duas.Count);
        var so14001 = await amb.Consultas.CalendarioAsync(amb.Admin, new FiltroCalendario { Ano = 2027, NormaIds = { amb.Iso14001 } });
        Assert.Single(so14001);
        var busca = await amb.Consultas.CalendarioAsync(amb.Admin, new FiltroCalendario { Ano = 2027, Busca = "fornecedor x" });
        Assert.Single(busca);
        var buscaAuditor = await amb.Consultas.CalendarioAsync(amb.Admin, new FiltroCalendario { Ano = 2027, Busca = "auditor tres" });
        Assert.Single(buscaAuditor);
    }
}

public class PdfTestes
{
    [Fact]
    public async Task Gera_pdfs_longos_sem_erro_com_varias_paginas()
    {
        using var amb = new Ambiente();
        var normas = await amb.Normas.ListarAsync(incluirRequisitos: true);
        var todos = normas.Single(n => n.Id == amb.Iso9001).Requisitos.Select(r => r.Id).ToHashSet();
        var p = amb.Interna(); p.RequisitoIds.UnionWith(todos);
        var id = await amb.Programacao.SalvarAsync(amb.Admin, p);
        var plano = new PlanoEdicao { AuditoriaId = id, Local = new string('L', 250), GestorNome = "Gestor", GestorEmail = "g@exemplo.invalid" };
        for (int d = 1; d <= 6; d++)
        {
            plano.Periodos.Add(new() { Data = new DateOnly(2027, 5, d), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(12, 0) });
            plano.Periodos.Add(new() { Data = new DateOnly(2027, 5, d), HoraInicio = new TimeOnly(13, 0), HoraFim = new TimeOnly(17, 0) });
        }
        await amb.Planos.SalvarAsync(amb.Admin, plano);
        await amb.Relatorios.IniciarAsync(amb.Admin, id);
        var a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);
        var dto = RelatorioServico.ParaEdicao(a);
        var textoLongo = string.Join("\n", Enumerable.Range(1, 60).Select(i => $"Linha {i} da evidência com acentuação: ação, inspeção, não conformidade, coração."));
        foreach (var r in a.Requisitos)
            dto.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade, AuditoriaRequisitoIds = { r.Id }, Evidencia = r.Codigo == "4.1" ? textoLongo : "Evidência." });
        Assert.True((await amb.Relatorios.ConcluirAsync(amb.Admin, dto)).Concluido);
        a = await amb.Programacao.ObterDetalheAsync(amb.Admin, id);

        var pdfPlano = PdfDocumentos.Plano(a, null);
        var pdfRel = PdfDocumentos.Relatorio(a, null);
        var pdfProg = PdfDocumentos.Programa(2027, new[] { a }, "Normas: Todas", null);
        var vazio = PdfDocumentos.Programa(2030, Array.Empty<Auditoria>(), "Normas: Todas", null);
        foreach (var pdf in new[] { pdfPlano, pdfRel, pdfProg, vazio })
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.True(Paginas(pdfRel) > 3);
        Assert.True(Paginas(pdfPlano) >= 2);
    }

    [Fact]
    public void Logotipo_valida_formato_e_preserva_proporcao()
    {
        using var bmp = new SkiaSharp.SKBitmap(400, 100);
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        var png = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100).ToArray();
        var webp = img.Encode(SkiaSharp.SKEncodedImageFormat.Webp, 90).ToArray();
        var (saida, w, h, tipo) = IdentidadeVisualServico.Processar(png);
        Assert.Equal((400, 100, "image/png"), (w, h, tipo));
        Assert.Equal("image/webp", IdentidadeVisualServico.Processar(webp).Tipo);
        Assert.Throws<RegraNegocioException>(() => IdentidadeVisualServico.Processar(System.Text.Encoding.UTF8.GetBytes("<svg></svg>")));
        Assert.Throws<RegraNegocioException>(() => IdentidadeVisualServico.Processar(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 }));
        var pdf = PdfDocumentos.Programa(2027, Array.Empty<Auditoria>(), "x", new LogoPdf(saida, w, h));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static int Paginas(byte[] pdf)
    {
        using var ms = new MemoryStream(pdf);
        using var doc = PdfSharp.Pdf.IO.PdfReader.Open(ms, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }
}
