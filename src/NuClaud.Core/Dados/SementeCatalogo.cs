using Microsoft.EntityFrameworkCore;
using NuClaud.Core.Dominio;
using NuClaud.Core.Servicos;

namespace NuClaud.Core.Dados;

/// <summary>
/// Carga inicial de normas e requisitos (números e títulos de seções — sem texto das normas).
/// Idempotente: só insere normas ausentes e só carrega requisitos em normas ainda sem requisitos.
/// Detalhes, fontes e pendências: docs/CARGA-NORMAS.md.
/// </summary>
public static class SementeCatalogo
{
    private const string NotaConferencia =
        "Números e títulos das seções conforme o sumário da edição brasileira (ABNT NBR), conferidos em fontes públicas. " +
        "Somente títulos de seções foram carregados (não há texto da norma). Itens sem título próprio (ex.: 4.4.1) e " +
        "itens com quatro níveis não foram incluídos. Recomenda-se conferência final contra o exemplar licenciado da empresa.";

    public sealed record NormaInicial(string Nome, string? Edicao, GrupoCorNorma Grupo, string? Fonte, (string Codigo, string Titulo)[] Requisitos);

    public static readonly NormaInicial[] Normas =
    {
        new("ISO 9001:2015", "ABNT NBR ISO 9001:2015 (ISO 9001:2015)", GrupoCorNorma.Qualidade, NotaConferencia, new[]
        {
            ("4.1", "Entendendo a organização e seu contexto"),
            ("4.2", "Entendendo as necessidades e expectativas de partes interessadas"),
            ("4.3", "Determinando o escopo do sistema de gestão da qualidade"),
            ("4.4", "Sistema de gestão da qualidade e seus processos"),
            ("5.1", "Liderança e comprometimento"),
            ("5.1.1", "Generalidades"),
            ("5.1.2", "Foco no cliente"),
            ("5.2", "Política"),
            ("5.2.1", "Desenvolvendo a política da qualidade"),
            ("5.2.2", "Comunicando a política da qualidade"),
            ("5.3", "Papéis, responsabilidades e autoridades organizacionais"),
            ("6.1", "Ações para abordar riscos e oportunidades"),
            ("6.2", "Objetivos da qualidade e planejamento para alcançá-los"),
            ("6.3", "Planejamento de mudanças"),
            ("7.1", "Recursos"),
            ("7.1.1", "Generalidades"),
            ("7.1.2", "Pessoas"),
            ("7.1.3", "Infraestrutura"),
            ("7.1.4", "Ambiente para a operação dos processos"),
            ("7.1.5", "Recursos de monitoramento e medição"),
            ("7.1.6", "Conhecimento organizacional"),
            ("7.2", "Competência"),
            ("7.3", "Conscientização"),
            ("7.4", "Comunicação"),
            ("7.5", "Informação documentada"),
            ("7.5.1", "Generalidades"),
            ("7.5.2", "Criando e atualizando"),
            ("7.5.3", "Controle de informação documentada"),
            ("8.1", "Planejamento e controle operacionais"),
            ("8.2", "Requisitos para produtos e serviços"),
            ("8.2.1", "Comunicação com o cliente"),
            ("8.2.2", "Determinação de requisitos relativos a produtos e serviços"),
            ("8.2.3", "Análise crítica de requisitos relativos a produtos e serviços"),
            ("8.2.4", "Mudanças nos requisitos para produtos e serviços"),
            ("8.3", "Projeto e desenvolvimento de produtos e serviços"),
            ("8.3.1", "Generalidades"),
            ("8.3.2", "Planejamento de projeto e desenvolvimento"),
            ("8.3.3", "Entradas de projeto e desenvolvimento"),
            ("8.3.4", "Controles de projeto e desenvolvimento"),
            ("8.3.5", "Saídas de projeto e desenvolvimento"),
            ("8.3.6", "Mudanças de projeto e desenvolvimento"),
            ("8.4", "Controle de processos, produtos e serviços providos externamente"),
            ("8.4.1", "Generalidades"),
            ("8.4.2", "Tipo e extensão do controle"),
            ("8.4.3", "Informação para provedores externos"),
            ("8.5", "Produção e provisão de serviço"),
            ("8.5.1", "Controle de produção e de provisão de serviço"),
            ("8.5.2", "Identificação e rastreabilidade"),
            ("8.5.3", "Propriedade pertencente a clientes ou provedores externos"),
            ("8.5.4", "Preservação"),
            ("8.5.5", "Atividades pós-entrega"),
            ("8.5.6", "Controle de mudanças"),
            ("8.6", "Liberação de produtos e serviços"),
            ("8.7", "Controle de saídas não conformes"),
            ("9.1", "Monitoramento, medição, análise e avaliação"),
            ("9.1.1", "Generalidades"),
            ("9.1.2", "Satisfação do cliente"),
            ("9.1.3", "Análise e avaliação"),
            ("9.2", "Auditoria interna"),
            ("9.3", "Análise crítica pela direção"),
            ("9.3.1", "Generalidades"),
            ("9.3.2", "Entradas de análise crítica pela direção"),
            ("9.3.3", "Saídas de análise crítica pela direção"),
            ("10.1", "Generalidades"),
            ("10.2", "Não conformidade e ação corretiva"),
            ("10.3", "Melhoria contínua"),
        }),
        new("ISO 14001", "ABNT NBR ISO 14001:2015 (ISO 14001:2015)", GrupoCorNorma.AmbienteSso, NotaConferencia, new[]
        {
            ("4.1", "Entendendo a organização e seu contexto"),
            ("4.2", "Entendendo as necessidades e expectativas de partes interessadas"),
            ("4.3", "Determinando o escopo do sistema de gestão ambiental"),
            ("4.4", "Sistema de gestão ambiental"),
            ("5.1", "Liderança e comprometimento"),
            ("5.2", "Política ambiental"),
            ("5.3", "Papéis, responsabilidades e autoridades organizacionais"),
            ("6.1", "Ações para abordar riscos e oportunidades"),
            ("6.1.1", "Generalidades"),
            ("6.1.2", "Aspectos ambientais"),
            ("6.1.3", "Requisitos legais e outros requisitos"),
            ("6.1.4", "Planejamento de ações"),
            ("6.2", "Objetivos ambientais e planejamento para alcançá-los"),
            ("6.2.1", "Objetivos ambientais"),
            ("6.2.2", "Planejamento de ações para alcançar os objetivos ambientais"),
            ("7.1", "Recursos"),
            ("7.2", "Competência"),
            ("7.3", "Conscientização"),
            ("7.4", "Comunicação"),
            ("7.4.1", "Generalidades"),
            ("7.4.2", "Comunicação interna"),
            ("7.4.3", "Comunicação externa"),
            ("7.5", "Informação documentada"),
            ("7.5.1", "Generalidades"),
            ("7.5.2", "Criando e atualizando"),
            ("7.5.3", "Controle de informação documentada"),
            ("8.1", "Planejamento e controle operacionais"),
            ("8.2", "Preparação e resposta a emergências"),
            ("9.1", "Monitoramento, medição, análise e avaliação"),
            ("9.1.1", "Generalidades"),
            ("9.1.2", "Avaliação do atendimento aos requisitos legais e outros requisitos"),
            ("9.2", "Auditoria interna"),
            ("9.2.1", "Generalidades"),
            ("9.2.2", "Programa de auditoria interna"),
            ("9.3", "Análise crítica pela direção"),
            ("10.1", "Generalidades"),
            ("10.2", "Não conformidade e ação corretiva"),
            ("10.3", "Melhoria contínua"),
        }),
        new("ISO 45001", "ABNT NBR ISO 45001 (ISO 45001:2018)", GrupoCorNorma.AmbienteSso, NotaConferencia +
            " Itens 6.1.2.1 a 6.1.2.3 (quatro níveis) não foram incluídos; use 6.1.2.", new[]
        {
            ("4.1", "Entendendo a organização e seu contexto"),
            ("4.2", "Entendendo as necessidades e expectativas de trabalhadores e de outras partes interessadas"),
            ("4.3", "Determinando o escopo do sistema de gestão de SSO"),
            ("4.4", "Sistema de gestão de SSO"),
            ("5.1", "Liderança e comprometimento"),
            ("5.2", "Política de SSO"),
            ("5.3", "Papéis, responsabilidades e autoridades organizacionais"),
            ("5.4", "Consulta e participação dos trabalhadores"),
            ("6.1", "Ações para abordar riscos e oportunidades"),
            ("6.1.1", "Generalidades"),
            ("6.1.2", "Identificação de perigos e avaliação de riscos e oportunidades"),
            ("6.1.3", "Determinação de requisitos legais e outros requisitos"),
            ("6.1.4", "Planejamento de ações"),
            ("6.2", "Objetivos de SSO e planejamento para alcançá-los"),
            ("6.2.1", "Objetivos de SSO"),
            ("6.2.2", "Planejamento para alcançar os objetivos de SSO"),
            ("7.1", "Recursos"),
            ("7.2", "Competência"),
            ("7.3", "Conscientização"),
            ("7.4", "Comunicação"),
            ("7.4.1", "Generalidades"),
            ("7.4.2", "Comunicação interna"),
            ("7.4.3", "Comunicação externa"),
            ("7.5", "Informação documentada"),
            ("7.5.1", "Generalidades"),
            ("7.5.2", "Criando e atualizando"),
            ("7.5.3", "Controle de informação documentada"),
            ("8.1", "Planejamento e controle operacionais"),
            ("8.1.1", "Generalidades"),
            ("8.1.2", "Eliminar perigos e reduzir riscos de SSO"),
            ("8.1.3", "Gestão de mudanças"),
            ("8.1.4", "Aquisição"),
            ("8.2", "Preparação e resposta a emergências"),
            ("9.1", "Monitoramento, medição, análise e avaliação de desempenho"),
            ("9.1.1", "Generalidades"),
            ("9.1.2", "Avaliação do atendimento aos requisitos legais e outros requisitos"),
            ("9.2", "Auditoria interna"),
            ("9.2.1", "Generalidades"),
            ("9.2.2", "Programa de auditoria interna"),
            ("9.3", "Análise crítica pela direção"),
            ("10.1", "Generalidades"),
            ("10.2", "Incidente, não conformidade e ação corretiva"),
            ("10.3", "Melhoria contínua"),
        }),
        new("ANSN 1.16", null, GrupoCorNorma.NuclearNaval,
            "PENDENTE: Garantia da Qualidade para a Segurança de Usinas Nucleoelétricas e Outras Instalações (antiga CNEN NN 1.16, " +
            "Resolução CNEN 15/99, Portaria CNEN 17/00). O texto oficial vigente (gov.br) não pôde ser consultado no ambiente de " +
            "desenvolvimento; edição e requisitos não foram carregados para não associar títulos sem verificação. Cadastre manualmente.",
            Array.Empty<(string, string)>()),
        new("ASME III", null, GrupoCorNorma.Asme,
            "Edição/divisão e documento específico a definir. Requisitos para cadastro manual.", Array.Empty<(string, string)>()),
        new("Secretaria Naval", null, GrupoCorNorma.NuclearNaval,
            "Documento específico a definir. Requisitos para cadastro manual.", Array.Empty<(string, string)>()),
    };

    public static async Task AplicarAsync(NuClaudDbContext db, DateTime agoraUtc)
    {
        var ator = Ator.Sistema;
        var existentes = await db.Normas.Include(n => n.Requisitos).ToDictionaryAsync(n => n.Nome);
        int ordem = existentes.Count == 0 ? 0 : existentes.Values.Max(n => n.Ordem);
        foreach (var ni in Normas)
        {
            if (!existentes.TryGetValue(ni.Nome, out var norma))
            {
                norma = new Norma { Nome = ni.Nome, Edicao = ni.Edicao, GrupoCor = ni.Grupo, FonteCarga = ni.Fonte, Ordem = ++ordem, Ativo = true };
                Historico.MarcarCriacao(norma, ator, agoraUtc);
                db.Normas.Add(norma);
                Historico.Registrar(db, ator, agoraUtc, "Norma", ni.Nome, "Carga inicial", $"Norma \"{ni.Nome}\" incluída pela carga inicial.");
            }
            if (norma.Requisitos.Count == 0 && ni.Requisitos.Length > 0)
            {
                foreach (var (codigo, titulo) in ni.Requisitos)
                {
                    var r = new Requisito { Codigo = codigo, Titulo = titulo, ChaveOrdenacao = OrdenacaoRequisitos.Chave(codigo), Ativo = true };
                    Historico.MarcarCriacao(r, ator, agoraUtc);
                    norma.Requisitos.Add(r);
                }
                Historico.Registrar(db, ator, agoraUtc, "Norma", ni.Nome, "Carga inicial", $"{ni.Requisitos.Length} requisitos carregados para \"{ni.Nome}\" ({ni.Edicao}).");
            }
        }
        await db.SaveChangesAsync();
    }
}
