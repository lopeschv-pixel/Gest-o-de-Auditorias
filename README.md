# NUCLAUD — NUCLEP

Gestão de auditorias: programação anual → calendário → plano com dias e horários → confirmação → emissão → relatório com constatações e referência ao PAC externo.

## Executar

Python 3.12+, sem dependências externas:

```sh
cd /workspace/Gest-o-de-Auditorias
python server.py
```

Porta padrão 8000 (`PORT` para alterar). SQLite em `data/nuclaud.db` (`NUCLAUD_DB` para alterar), ignorado pelo Git. Faça backup com o serviço parado.

## Fluxo e cadastros

- Cadastre áreas no menu lateral. Os formulários de áreas e normas ficam na própria página, com confirmação/erro de salvamento.
- Cadastre normas e requisitos. Cada requisito tem apenas norma e código, sem campo descrição. Os requisitos oficiais ainda não foram importados: veja [REQUIREMENTS.md](REQUIREMENTS.md).
- Cadastre auditores com habilitações independentes por norma: Auditor líder, Auditor e Observador. Funções antigas foram preservadas na migração.
- Na programação, selecione tipo, ano, uma ou mais áreas, uma ou mais normas, mês, líder, equipe, observadores e requisitos cadastrados. Não há título nem cadastro de áreas dentro da programação. Códigos automáticos: AUDI-I-001-2027 / AUDI-F-001-2027, com sequência por tipo e ano. Tipo e ano ficam preservados após gerar o código.
- O líder precisa ter habilitação de líder em todas as normas; membros/observadores precisam da respectiva função em pelo menos uma norma. Uma pessoa não ocupa duas funções na mesma auditoria. Pelo menos um requisito por norma deve ser selecionado.
- Pelo calendário, gere o plano. Ele aproveita a programação, permite alterar a equipe e os requisitos, adicionar vários dias/períodos com horário de início/fim por dia, objetivo/escopo, local e e-mail do gestor. É possível ter períodos separados no mesmo dia; sobreposição e fim anterior ao início são bloqueados.
- Confirmar o plano habilita PDF e preparação de e-mail. O PDF usa a impressão do navegador. O e-mail abre no aplicativo configurado, já preenchido; envio é manual, assim como anexar PDF.
- Gerar relatório fica dentro da auditoria, acessada pelo calendário. Não há menu lateral de relatório. Registre participantes, conclusão, constatações e evidências. Não conformidades exigem o número do PAC; tratamento em sistema externo.

## Planejamento e ranqueamento

Planejamento de auditores mostra, por pessoa e ano do programa, as auditorias designadas, função e dias/horários (ou mês previsto quando não há plano). Ranqueamento de auditores é o subitem, com ordenação por mais/menos pontos.

- Líder: 2 pontos por auditoria; auditor: 1; observador: 0.
- Uma auditoria é contada uma única vez por pessoa, sem multiplicar por dias, períodos ou normas.
- O plano salvo, inclusive rascunho, substitui a equipe da programação para o cálculo. Não há soma duplicada entre fases.
- Todos os auditores cadastrados aparecem no ranqueamento, incluindo os com zero pontos. Os totais são calculados dos registros atuais; alterações de equipe não acumulam pontos antigos.

## Demonstração no GitHub Pages

`python scripts/build_preview.py` gera `/tmp/nuclaud-preview/NUCLAUD-v9.html` a partir da mesma interface do servidor. Publicado como index.html na branch gh-pages. Ativação do Pages depende das configurações do repositório; publicação só é confirmada quando o provedor concluir a implantação.

A demonstração usa armazenamento local do navegador, separado do SQLite. Mantém a mesma chave de dados das prévias anteriores. Se o navegador bloquear armazenamento, os registros ficam apenas na sessão e há um aviso. Não limpar/resetar o armazenamento para atualizar o aplicativo.

Planos antigos com uma data continuam visíveis, sem inventar horários; ao editar, informe os horários. Descrições antigas de requisitos ficam preservadas nos registros históricos, mas não são exigidas ou exibidas no cadastro novo.

## Verificação

- `python -m unittest discover -s tests -v`: API, banco temporário, requisitos sem descrição e períodos múltiplos/invalidade.
- `node tests/test_planning.cjs`: pontuação, troca da equipe no plano, filtros anuais, zeros, ordenação, compatibilidade e períodos sobrepostos.
- `python scripts/build_preview.py && python scripts/check_preview.py`: teste no Chromium/Playwright dos cadastros, seleção múltipla, plano com dois dias, PDF/e-mail, planejamento, ranking e persistência, inclusive armazenamento bloqueado. Requer Playwright e `/usr/bin/chromium`.

## Limites

Protótipo sem autenticação, perfis de acesso, anexos, revisão formal, exclusão ou edição de cadastros/constatações. Use dados fictícios na demonstração pública. Não há integração com o sistema de PACs. Requisitos oficiais ainda pendentes, conforme REQUIREMENTS.md.
