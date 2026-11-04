using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Core.Dados;

/// <summary>
/// Dados FICTÍCIOS para o ambiente de demonstração/testes. Só é executada quando
/// Demonstracao:Habilitada = true e o banco ainda não possui áreas nem auditorias.
/// Nunca habilite em produção (ver docs/IMPLANTACAO.md).
/// </summary>
public class SementeDemonstracao
{
    private readonly IDbContextFactory<NuClaudDbContext> _fabrica;
    private readonly IRelogio _relogio;

    public SementeDemonstracao(IDbContextFactory<NuClaudDbContext> fabrica, IRelogio relogio)
    {
        _fabrica = fabrica;
        _relogio = relogio;
    }

    public static string GerarCnpj(string base12)
    {
        int Dv(string s, int[] m) { int soma = 0; for (int i = 0; i < m.Length; i++) soma += (s[i] - '0') * m[i]; int r = soma % 11; return r < 2 ? 0 : 11 - r; }
        var d1 = Dv(base12, new[] { 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });
        var d2 = Dv(base12 + d1, new[] { 6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2 });
        return Validacoes.FormatarCnpj(base12 + d1 + d2);
    }

    public async Task<bool> AplicarAsync(string? senhaUsuarios)
    {
        await using (var db0 = await _fabrica.CreateDbContextAsync())
            if (await db0.Areas.AnyAsync() || await db0.Auditorias.AnyAsync()) return false;

        var s = Ator.Sistema;
        var cad = new CadastroServico(_fabrica, _relogio);
        var normasSrv = new NormaServico(_fabrica, _relogio);
        var audSrv = new AuditorServico(_fabrica, _relogio);
        var usuSrv = new UsuarioServico(_fabrica, _relogio);
        var prog = new ProgramacaoServico(_fabrica, _relogio);
        var planos = new PlanoServico(_fabrica, _relogio);
        var rels = new RelatorioServico(_fabrica, _relogio);

        var areas = new Dictionary<string, int>();
        foreach (var n in new[] { "Engenharia", "Suprimentos", "Produção — Caldeiraria", "Usinagem", "Garantia da Qualidade", "Segurança do Trabalho", "Meio Ambiente", "Recursos Humanos" })
            areas[n] = await cad.SalvarAreaAsync(s, new AreaEdicao { Nome = n });

        var normas = await normasSrv.ListarAsync(incluirRequisitos: true);
        int N(string nome) => normas.First(n => n.Nome == nome).Id;
        int R(string norma, string codigo) => normas.First(n => n.Nome == norma).Requisitos.First(r => r.Codigo == codigo).Id;
        var iso9001 = N("ISO 9001:2015"); var iso14001 = N("ISO 14001"); var iso45001 = N("ISO 45001");
        var ansn = N("ANSN 1.16"); var asme = N("ASME III"); var naval = N("Secretaria Naval");

        // Requisitos fictícios de exemplo para ASME III (para demonstrar cadastro manual).
        await normasSrv.SalvarRequisitoAsync(s, new RequisitoEdicao { NormaId = asme, Codigo = "1.1", Titulo = "Exemplo — Requisito de demonstração (cadastrar conforme documento aplicável)" });
        normas = await normasSrv.ListarAsync(incluirRequisitos: true);

        var macros = new Dictionary<string, int>();
        foreach (var n in new[] { "Suprimentos", "Produção", "Gestão", "Saúde, Meio Ambiente e Segurança" })
            macros[n] = await cad.SalvarMacroprocessoAsync(s, new MacroprocessoEdicao { Nome = n });

        async Task<int> Proc(string nome, string macro, params (int Norma, string Codigo)[] reqs)
        {
            var normasProc = reqs.Select(r => r.Norma).ToHashSet();
            return await cad.SalvarProcessoAsync(s, new ProcessoEdicao
            {
                Nome = nome, MacroprocessoId = macros[macro], NormaIds = normasProc,
                RequisitoIds = reqs.Select(r => R(normas.First(n => n.Id == r.Norma).Nome, r.Codigo)).ToHashSet()
            });
        }
        var compras = await Proc("Compras", "Suprimentos", (iso9001, "8.4"), (iso9001, "8.4.1"), (iso9001, "8.4.2"), (iso9001, "8.4.3"), (iso14001, "8.1"));
        var recebimento = await Proc("Recebimento e inspeção", "Suprimentos", (iso9001, "8.6"), (iso9001, "8.7"));
        var fabricacao = await Proc("Fabricação", "Produção", (iso9001, "8.5.1"), (iso9001, "8.5.2"), (iso9001, "8.5.4"), (iso45001, "8.1.2"));
        var documentos = await Proc("Controle de documentos", "Gestão", (iso9001, "7.5.1"), (iso9001, "7.5.2"), (iso9001, "7.5.3"));
        await Proc("Gestão de pessoas", "Gestão", (iso9001, "7.2"), (iso9001, "7.3"), (iso45001, "7.2"));
        var ambiental = await Proc("Gestão ambiental", "Saúde, Meio Ambiente e Segurança", (iso14001, "6.1.2"), (iso14001, "6.1.3"), (iso14001, "8.2"));
        var sso = await Proc("Saúde e segurança ocupacional", "Saúde, Meio Ambiente e Segurança", (iso45001, "5.4"), (iso45001, "6.1.2"), (iso45001, "8.2"));

        HabilitacaoEdicao H(int norma, bool l, bool a, bool o) => new() { NormaId = norma, PodeLiderar = l, PodeAuditar = a, PodeObservar = o };
        var ana = await audSrv.SalvarAsync(s, new AuditorEdicao { NomeCompleto = "Ana Ribeiro", Email = "ana.ribeiro@exemplo.invalid",
            Habilitacoes = { H(iso9001, true, true, false), H(iso14001, true, true, false), H(iso45001, true, true, false), H(ansn, false, true, false) } });
        var bruno = await audSrv.SalvarAsync(s, new AuditorEdicao { NomeCompleto = "Bruno Carvalho", Email = "bruno.carvalho@exemplo.invalid",
            Habilitacoes = { H(iso9001, true, true, false), H(iso14001, false, true, true), H(asme, true, true, false) } });
        var carla = await audSrv.SalvarAsync(s, new AuditorEdicao { NomeCompleto = "Carla Mendes", Email = "carla.mendes@exemplo.invalid",
            Habilitacoes = { H(iso9001, true, true, false), H(ansn, true, true, false), H(naval, true, true, false) } });
        var diego = await audSrv.SalvarAsync(s, new AuditorEdicao { NomeCompleto = "Diego Fernandes", Email = "diego.fernandes@exemplo.invalid",
            Habilitacoes = { H(iso9001, false, true, false), H(iso45001, false, true, false), H(asme, false, true, false) } });
        var elisa = await audSrv.SalvarAsync(s, new AuditorEdicao { NomeCompleto = "Elisa Moura", Email = "elisa.moura@exemplo.invalid",
            Habilitacoes = { H(iso9001, false, false, true), H(iso14001, false, false, true), H(iso45001, false, false, true) } });
        var felipe = await audSrv.SalvarAsync(s, new AuditorEdicao { NomeCompleto = "Felipe Araújo", Email = "felipe.araujo@exemplo.invalid",
            Habilitacoes = { H(iso9001, false, true, false), H(ansn, false, true, true), H(naval, false, true, false) } });

        if (UsuarioServico.ValidarSenha(senhaUsuarios).Count == 0)
        {
            await usuSrv.SalvarAsync(s, new UsuarioEdicao { NomeRede = "ana.ribeiro", NomeCompleto = "Ana Ribeiro", Email = "ana.ribeiro@exemplo.invalid", AuditorId = ana, SenhaInicial = senhaUsuarios });
            await usuSrv.SalvarAsync(s, new UsuarioEdicao { NomeRede = "carla.mendes", NomeCompleto = "Carla Mendes", Email = "carla.mendes@exemplo.invalid", AuditorId = carla, SenhaInicial = senhaUsuarios });
            await usuSrv.SalvarAsync(s, new UsuarioEdicao { NomeRede = "bruno.carvalho", NomeCompleto = "Bruno Carvalho", Email = "bruno.carvalho@exemplo.invalid", AuditorId = bruno, SenhaInicial = senhaUsuarios });
        }

        var ano = FusoHorario.AnoAtual(_relogio) + 1;
        HashSet<int> Reqs(params (int Norma, string Codigo)[] r) => r.Select(x => R(normas.First(n => n.Id == x.Norma).Nome, x.Codigo)).ToHashSet();
        async Task<int> Programar(ProgramacaoEdicao p)
        {
            // Inclui automaticamente os obrigatórios dos processos.
            var dados = await prog.ObterDadosFormularioAsync(s);
            var obrig = RegrasRequisitos.Obrigatorios(dados.Processos.Where(x => p.ProcessoIds.Contains(x.Id)), p.NormaIds);
            p.RequisitoIds.UnionWith(obrig.Keys);
            return await prog.SalvarAsync(s, p);
        }

        // 1) Interna concluída
        var a1 = await Programar(new ProgramacaoEdicao { Tipo = TipoAuditoria.Interna, Ano = ano, MesPrevisto = 3, NormaIds = { iso9001 },
            AreaIds = { areas["Suprimentos"], areas["Garantia da Qualidade"] }, ProcessoIds = { compras, recebimento },
            RequisitoIds = Reqs((iso9001, "7.5.3")), LiderId = ana, AuditorIds = { diego }, ObservadorIds = { elisa } });
        await planos.SalvarAsync(s, new PlanoEdicao { AuditoriaId = a1, Local = "Sede — Itaguaí/RJ, prédio administrativo", GestorNome = "Gestor fictício de Suprimentos",
            GestorEmail = "gestor.suprimentos@exemplo.invalid", Periodos = {
                new() { Data = new DateOnly(ano, 3, 16), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(12, 0) },
                new() { Data = new DateOnly(ano, 3, 16), HoraInicio = new TimeOnly(13, 0), HoraFim = new TimeOnly(17, 0) },
                new() { Data = new DateOnly(ano, 3, 17), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(12, 0) } } });
        await rels.IniciarAsync(s, a1);
        var det1 = await prog.ObterDetalheAsync(s, a1);
        var porCodigo = det1.Requisitos.ToDictionary(r => r.Codigo, r => r.Id);
        var ed1 = RelatorioServico.ParaEdicao(det1);
        ed1.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.Conformidade,
            AuditoriaRequisitoIds = det1.Requisitos.Where(r => r.Codigo != "8.7" && r.Codigo != "8.4.2").Select(r => r.Id).ToHashSet(),
            Evidencia = "Exemplo fictício: processo de compras e controle de documentos evidenciados por amostragem de pedidos e registros." });
        ed1.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.NaoConformidade,
            AuditoriaRequisitoIds = { porCodigo["8.7"], porCodigo["8.4.2"] },
            DescricaoNaoConformidade = "Exemplo fictício: saídas não conformes identificadas sem registro de disposição.",
            EvidenciasObjetivas = "Exemplo fictício: amostra de três relatórios de inspeção sem disposição registrada.", NumeroPac = "PAC-0000/DEMO" });
        ed1.Constatacoes.Add(new ConstatacaoEdicao { Classificacao = ClassificacaoConstatacao.OportunidadeMelhoria,
            OportunidadeMelhoria = "Exemplo fictício: avaliar indicadores de desempenho de fornecedores críticos." });
        await rels.ConcluirAsync(s, ed1);

        // 2) Interna com relatório em elaboração
        var a2 = await Programar(new ProgramacaoEdicao { Tipo = TipoAuditoria.Interna, Ano = ano, MesPrevisto = 4, NormaIds = { iso14001, iso45001 },
            AreaIds = { areas["Meio Ambiente"], areas["Segurança do Trabalho"] }, ProcessoIds = { ambiental, sso },
            LiderId = ana, AuditorIds = { diego }, ObservadorIds = { bruno } });
        await planos.SalvarAsync(s, new PlanoEdicao { AuditoriaId = a2, Local = "Pátio industrial", GestorNome = "Gestor fictício de SMS", GestorEmail = "gestor.sms@exemplo.invalid",
            Periodos = { new() { Data = new DateOnly(ano, 4, 13), HoraInicio = new TimeOnly(8, 0), HoraFim = new TimeOnly(16, 0) } } });
        await rels.IniciarAsync(s, a2);

        // 3) Fornecedores com plano emitido
        var a3 = await Programar(new ProgramacaoEdicao { Tipo = TipoAuditoria.Fornecedor, Ano = ano, MesPrevisto = 5, NormaIds = { iso9001, asme },
            FornecedorNome = "Fornecedor Exemplo de Aços Ltda. (fictício)", FornecedorCnpj = GerarCnpj("112223330001"),
            FornecedorEndereco = "Rua Fictícia, 100 — Distrito Industrial — Cidade Exemplo/RJ",
            EscopoFornecimento = "Fornecimento de chapas e forjados para vasos de pressão (exemplo fictício).",
            RequisitoIds = Reqs((iso9001, "8.4"), (iso9001, "8.5.2"), (iso9001, "8.6")).Append(R("ASME III", "1.1")).ToHashSet(),
            LiderId = bruno, AuditorIds = { diego } });
        await planos.SalvarAsync(s, new PlanoEdicao { AuditoriaId = a3, Local = "Instalações do fornecedor", GestorNome = "Representante fictício do fornecedor",
            GestorEmail = "qualidade@fornecedor-exemplo.invalid",
            Objetivo = "Avaliar a capacidade do fornecedor em atender aos requisitos contratuais e normativos aplicáveis.",
            Escopo = "Processos de fabricação, inspeção e rastreabilidade de materiais.",
            Periodos = { new() { Data = new DateOnly(ano, 5, 11), HoraInicio = new TimeOnly(9, 0), HoraFim = new TimeOnly(17, 0) },
                         new() { Data = new DateOnly(ano, 5, 12), HoraInicio = new TimeOnly(9, 0), HoraFim = new TimeOnly(12, 0) } } });

        // 4) Interna nuclear/naval programada
        await Programar(new ProgramacaoEdicao { Tipo = TipoAuditoria.Interna, Ano = ano, MesPrevisto = 6, NormaIds = { ansn, naval },
            AreaIds = { areas["Engenharia"] }, LiderId = carla, AuditorIds = { felipe } });

        // 5) Fornecedores programada
        await Programar(new ProgramacaoEdicao { Tipo = TipoAuditoria.Fornecedor, Ano = ano, MesPrevisto = 8, NormaIds = { iso9001 },
            FornecedorNome = "Usinagem Modelo S.A. (fictício)", FornecedorCnpj = GerarCnpj("445556660001"),
            FornecedorEndereco = "Avenida Exemplo, 2000 — Cidade Modelo/SP", EscopoFornecimento = "Serviços de usinagem de precisão (exemplo fictício).",
            RequisitoIds = Reqs((iso9001, "8.4.1"), (iso9001, "8.5.1")), LiderId = carla, AuditorIds = { felipe } });

        // 6) Interna com processo de documentos
        await Programar(new ProgramacaoEdicao { Tipo = TipoAuditoria.Interna, Ano = ano, MesPrevisto = 9, NormaIds = { iso9001 },
            AreaIds = { areas["Engenharia"], areas["Produção — Caldeiraria"] }, ProcessoIds = { documentos, fabricacao },
            LiderId = carla, AuditorIds = { felipe, diego } });

        return true;
    }
}
