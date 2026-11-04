# Regras de negócio implementadas

## Perfis e permissões (validadas no servidor)

| Ação | Administrador | Usuário (auditor líder designado) | Usuário (auditor/observador) | Usuário sem participação |
|---|---|---|---|---|
| Cadastros, usuários, logotipo, histórico geral | ✔ | — | — | — |
| Programar, editar e excluir programação | ✔ | — | — | — |
| Ver auditoria, plano, relatório e PDFs | ✔ (todas) | ✔ | ✔ | — (403) |
| Gerar/salvar plano | ✔ todos os campos | ✔ **somente datas e horários** | — | — |
| Gerar/editar relatório e constatações | ✔ | ✔ antes da conclusão | — | — |
| Concluir relatório | ✔ | ✔ | — | — |
| Corrigir relatório concluído (reabrir) | ✔ (com motivo) | — | — | — |
| Planejamento de auditores | ✔ todos | só a própria agenda | só a própria agenda | — |
| Ranqueamento | ✔ | — | — | — |

- Estar cadastrado como auditor **não** concede acesso; o acesso exige usuário ativo. O vínculo usuário ↔ auditor define
  em quais auditorias o usuário atua.
- O último administrador ativo não pode ser excluído, inativado nem rebaixado.

## Código das auditorias
- `AUDI-I-NNN-AAAA` (interna) e `AUDI-F-NNN-AAAA` (fornecedores), sequência por tipo e ano com no mínimo 3 dígitos.
- Próxima sequência = **maior sequência existente do tipo/ano + 1** (001 se não houver). Não renumera, não preenche lacunas
  internas; se todas forem excluídas, recomeça em 001.
- Gerado no servidor, dentro de transação com bloqueio de aplicação do SQL Server (`sp_getapplock` por tipo/ano), índice único
  e nova tentativa automática. Tipo e ano não podem ser alterados após a criação (definem o código).

## Habilitações para auditorias com várias normas (regra adotada)
- **Auditor líder:** habilitação “Auditor líder” em **todas** as normas selecionadas.
- **Auditores:** habilitação “Auditor” ou “Auditor líder” em **ao menos uma** norma selecionada.
- **Observadores:** habilitação “Observador” em ao menos uma norma selecionada.
- **Cobertura:** cada norma precisa ter ao menos um membro da equipe (líder ou auditores) habilitado como auditor nela.
- Candidatos incompatíveis aparecem desabilitados com o motivo; o servidor rejeita qualquer designação sem habilitação.
- Uma pessoa ocupa uma única função por auditoria (o líder é retirado automaticamente de Auditores/Observadores).

## Requisitos da programação
- Ao selecionar processos, os requisitos obrigatórios (cadastro do processo) das **normas selecionadas** são incluídos,
  unidos sem duplicação e identificados com a origem (“Obrigatório · Compras”). Requisitos inativos não são incluídos.
- Obrigatórios não podem ser retirados (tela e servidor). Requisitos adicionais podem ser marcados livremente.
- Trocar normas/processos atualiza a lista e mostra aviso do que foi incluído/retirado.
- Se normas e processos não mudarem, a auditoria mantém os obrigatórios registrados na programação, mesmo que o cadastro do
  processo seja alterado depois (retrato histórico).
- Fornecedores: sem área, macroprocesso ou processo (valores ocultos são descartados no servidor).

## Bloqueios por documentos
- Após o **início do relatório**, normas, requisitos, equipe, áreas, processos e dados do fornecedor ficam bloqueados (só o mês muda).
  Em revisão (reaberto pelo administrador) a estrutura pode ser ajustada, exceto retirar requisitos usados em constatações.
- Após a **conclusão**, plano e relatório ficam somente leitura; correção apenas por administrador via “Corrigir relatório”,
  que exige motivo, grava no histórico o retrato da versão concluída, muda a situação para “Relatório em revisão” e exige nova conclusão.
- Alterações no plano depois do início do relatório são permitidas ao líder/administrador, mas registradas no histórico.

## Conclusão do relatório
- Todos os requisitos auditados precisam estar em ao menos uma constatação de **Conformidade** ou **Não conformidade**.
  Observação e Oportunidade de melhoria não contam.
- Fornecedores: qualificação obrigatória (uma opção). Observações e RQF são opcionais.
- Ao clicar em “Concluir relatório”, os dados são salvos e validados numa única transação. Se houver pendências, os dados
  ficam salvos, a conclusão é bloqueada e os requisitos pendentes (número e título) são listados.
- Registra autor e data/hora; o calendário passa a cinza com situação “Realizada”.

## Constatações
- Conformidade: requisitos + “Evidência encontrada”. Não conformidade: requisitos (um ou vários) + descrição + evidências
  objetivas + “Número do PAC” (opcional, apenas referência). Observação e Oportunidade de melhoria: um campo de texto.
- Ao trocar a classificação, o sistema avisa quais campos preenchidos serão descartados ao salvar; voltar à classificação
  anterior recupera o conteúdo antes de salvar.
- Constatações são renumeradas sequencialmente (01, 02…) ao excluir.

## Ranqueamento
- Pontos por designação nas auditorias **programadas** no ano: líder 2, auditor 1, observador 0; cada auditoria conta uma
  vez por pessoa. Auditores ativos sem designação aparecem com zero. Finalidade: equilíbrio de carga, não avaliação de desempenho.

## Calendário e PDF do programa
- Filtros de ano, normas (várias = “contém ao menos uma”, sem duplicar) e busca (código, área, fornecedor, auditor; sem
  diferenciar acentos). O PDF usa **a mesma função de filtro e as mesmas permissões** da tela.
- Cores: Realizada (cinza) > ASME III (amarelo) > ANSN 1.16/Secretaria Naval (laranja) > ISO 14001/45001 (verde) > ISO 9001 (azul).
  O grupo de cor é configurável em cada norma. A situação também aparece em texto.
- O título do PDF é sempre “Programa de Auditorias Internas — AAAA”, conforme solicitado.
