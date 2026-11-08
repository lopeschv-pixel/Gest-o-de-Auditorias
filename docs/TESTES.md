# Testes

## Automatizados (`dotnet test`) — 49 testes, todos aprovados

| Grupo | O que verifica |
|---|---|
| `RegrasTestes` | Ordenação numérica (4.1, 4.2, 4.2.1 … 10.1); níveis de código (aceita 4.1 e 4.4.1, rejeita 4 e 4.4.1.1); formato e sequência dos códigos; cobertura do relatório (observação/OM não contam); pontuação 2/1/0 sem duplicar; união de obrigatórios; prioridade de cores; períodos (horário, sobreposição, duplicidade); CNPJ; habilitação do líder em todas as normas |
| `ProgramacaoTestes` | Sequências por tipo/ano; exclusão sem renumerar; reinício em 001; só administrador programa/altera/exclui; requisitos escolhidos por processo (processo sem requisito bloqueado, mesmo requisito em dois processos, processo não auditável na norma recusado); alteração do processo não reescreve auditoria; fornecedor sem área/processo; habilitações e duplicidades; edição de auditor preserva participação; área usada não é excluída e renomear preserva histórico; bloqueio após início do relatório; concorrência otimista |
| `PermissoesTestes` | Acesso negado por troca de identificador; auditor não líder só visualiza; líder altera apenas datas/horários; objetivo/escopo só fornecedor; proteção do último administrador; operações administrativas bloqueadas para usuário; senha com hash e bloqueio por tentativas |
| `RelatorioTestes` | Bloqueio por requisito pendente com número/título e dados preservados; conclusão; bloqueio pós-conclusão; reabertura com motivo e histórico; nova conclusão; campos por classificação e descarte de ocultos; editar/excluir com renumeração; qualificação obrigatória de fornecedor; interna sem RQF; relatório exige plano |
| `ImportacaoTestes` | Leitura de .xlsx com títulos, células mescladas e macroprocesso em branco; sem títulos; CSV; recusa de .xls e arquivos inválidos; prévia; importação para a norma sem duplicar; bloqueio para usuário comum |
| `CoberturaTestes` | Processos e requisitos realizados, programados e não planejados por ano e por intervalo; requisito contado em processos diferentes; acesso só de administrador |
| `ConsultasTestes` | Ranqueamento por ano; filtro de várias normas sem duplicar; busca por fornecedor e auditor sem acento |
| `PdfTestes` | PDFs longos (todos os requisitos da ISO 9001, 12 períodos, 66 constatações, texto com 60 linhas e acentos) com várias páginas; programa vazio; validação e proporção do logotipo (PNG/WebP aceitos, SVG e PNG corrompido rejeitados) |
| `IntegracaoSqlServerTestes` | Em SQL Server real: aplica migrações, carga idempotente e **20 criações simultâneas** gerando AUDI‑I‑001…020 sem duplicidade (requer `NUCLAUD_TESTE_SQLSERVER`) |

## Testes de interface executados (Playwright, navegador Chromium, SQL Server 2022)
- Entrada, troca obrigatória de senha, menu por perfil (usuário vê apenas Calendário e Planejamento).
- Programar auditoria interna com duas normas, duas áreas e processo: requisitos obrigatórios carregados com aviso,
  obrigatório desabilitado, requisito adicional, líder único, auditores e observadores; salvar e gerar código.
- Plano: sobreposição bloqueada com mensagem; três períodos (dois no mesmo dia); local e gestor; salvo.
- Relatório: gerar, adicionar conformidade e observação, salvar, recarregar (dados preservados), tentativa de conclusão
  bloqueada listando os 4 requisitos pendentes, adicionar NC com vários requisitos e PAC, concluir.
- Usuário comum: não acessa telas administrativas nem auditoria alheia (tela e PDF retornam acesso negado — 403);
  líder com campos gerais do plano desabilitados e datas editáveis; salvar plano como líder.
- Logotipo PNG enviado, prévia e uso no PDF com proporção preservada.
- PDFs do plano, relatório (interna e fornecedor) e programa anual (ano com e sem auditorias) baixados e inspecionados:
  cabeçalho repetido, numeração “Página X de Y”, assinatura “Assinatura do auditor líder” somente no fim da última página,
  sem marca “NUCLAUD — NUCLEP”, sem endereço do servidor, acentos corretos.
- Celular (iPhone 13): menu recolhível, sem rolagem horizontal.
- Persistência: aplicação reiniciada, dados mantidos no SQL Server.

## Critérios de aceitação

✔ = verificado (automatizado ou interface) · ◐ = implementado, verificação manual recomendada

| # | Critério | | # | Critério | |
|---|---|---|---|---|---|
| 1 | Área cadastrada aparece na programação | ✔ | 36 | Ausência de “Preparar e-mail” | ✔ |
| 2 | Editar área sem perder histórico | ✔ | 37 | Quatro classificações | ✔ |
| 3 | Norma e requisito com número e título | ✔ | 38 | Campos específicos | ✔ |
| 4 | Sem descrição adicional do requisito | ✔ | 39 | NC com vários requisitos | ✔ |
| 5 | Auditor com funções diferentes por norma | ✔ | 40 | Salvar e reabrir sem perda | ✔ |
| 6 | Editar auditor preservando participações | ✔ | 41 | Editar/excluir constatações | ✔ |
| 7 | Macroprocesso e processo | ✔ | 42 | Bloquear conclusão com pendência | ✔ |
| 8 | Várias normas e obrigatórios no processo | ✔ | 43 | Listar números e títulos pendentes | ✔ |
| 9 | Editar/excluir/inativar usuários, macros, processos | ✔ | 44 | Observação/OM não suprem | ✔ |
| 10 | Interna com várias áreas e normas | ✔ | 45 | Classificar todos e concluir | ✔ |
| 11 | Processo carrega requisitos | ✔ | 46 | Edição bloqueada ao usuário | ✔ |
| 12 | Requisitos extras | ✔ | 47 | Correção administrativa com histórico | ✔ |
| 13 | Apenas um líder | ✔ | 48 | Calendário cinza e “Realizada” | ✔ |
| 14 | Vários auditores e observadores | ✔ | 49 | Fornecedor: qualificação, observações, RQF | ✔ |
| 15 | Editar programação | ✔ | 50 | Interna sem conclusão e RQF | ✔ |
| 16 | Excluir só administrador | ✔ | 51 | Logotipo nos três PDFs | ✔ |
| 17 | Excluir todas e voltar a 001 | ✔ | 52 | Documentos longos sem cortes | ✔ |
| 18 | Sequências independentes | ✔ | 53 | Assinatura no fim da última página | ✔ |
| 19 | Fornecedor sem área/processo | ✔ | 54 | Assinatura afastada e sem data | ✔ |
| 20 | Nome, CNPJ, endereço e escopo | ✔ | 55 | Sem marca textual NUCLAUD — NUCLEP | ✔ |
| 21 | Códigos únicos com criações simultâneas | ✔ | 56 | Título “Relatório de Auditoria Interna” | ✔ |
| 22–24 | Filtrar uma, várias, voltar a Todas | ✔ | 57 | Computador e celular | ✔ |
| 25 | Cores e legenda | ✔ | 58 | Seleções alinhadas, letras confortáveis | ✔ |
| 26 | PDF com os mesmos filtros | ✔ | 59 | Bloqueio administrativo no servidor | ✔ |
| 27 | Ano e título do PDF | ✔ | 60 | Bloqueio de auditoria de outro líder | ✔ |
| 28 | Plano herda programação | ✔ | 61–62 | Pontuação 2/1/0 | ✔ |
| 29 | Vários dias e horários | ✔ | 63 | Ano e ordenação do ranqueamento | ◐ |
| 30 | Nome e e-mail do gestor | ✔ | 64 | Compilar e executar | ✔ |
| 31 | Objetivo/escopo só fornecedor | ✔ | 65 | Migrações em teste | ✔ |
| 32 | Fornecedor sem macro/processo | ✔ | 66 | Persistência após reinício | ✔ |
| 33 | Líder altera só datas/horários | ✔ | 67 | Sem segredos no repositório | ✔ |
| 34 | Administrador altera demais campos | ✔ | 68–69 | Publicação, backup e restauração documentados | ✔ |
| 35 | PDF sem rascunho | ✔ | 70 | Endereço real de teste | ◐ ver README (Codespaces) |
