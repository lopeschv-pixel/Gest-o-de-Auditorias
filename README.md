# NUCLAUD — NUCLEP

Gestão de auditorias: programação anual → calendário → geração e confirmação do plano → emissão do plano → relatório.

## Executar

Requer Python 3.12+, sem dependências externas.

```sh
cd /workspace/Gest-o-de-Auditorias
python server.py
```

Porta padrão 8000 (`PORT` para alterar). SQLite em `data/nuclaud.db`, ignorado pelo Git (`NUCLAUD_DB` para alterar). Faça backup com o serviço parado.

## Fluxo

- **Programar auditorias:** tipo (Interna ou Fornecedores), ano (2000–2100), normas selecionáveis, área cadastrada, mês previsto, auditor líder, auditores e requisitos. Não há título. O código é gerado ao salvar: AUDI-I-001-2027 ou AUDI-F-001-2027; a sequência é independente por tipo e ano. Tipo e ano ficam preservados após gerar o código. O menu Cadastrar áreas permite incluir as opções disponíveis. Referenciais: ISO 9001:2015, ANSN 1.16, ISO 14001, ISO 45001 e ASME III.
- **Calendário:** selecione o ano e use Gerar plano na auditoria. O plano copia a programação, permite ajustar data específica, auditores e requisitos, preservando a programação original.
- **Confirmar plano:** após conferir, confirme para habilitar emissão e relatório. Alterações no plano antes do relatório exigem nova confirmação. Planos com relatório iniciado ficam preservados.
- **PDF:** Gerar PDF abre a impressão do navegador; escolha Salvar como PDF. O documento contém somente o plano ou o relatório escolhido.
- **E-mail:** Preparar e-mail abre o aplicativo de e-mail configurado com destinatário, assunto e plano no corpo. Não envia automaticamente e não anexa PDF; anexe o PDF manualmente se desejar.
- **Gerar relatório:** disponível após confirmar o plano, com participantes, conclusão e constatações. Não conformidades exigem número do PAC; o tratamento ocorre no sistema externo.

## Prévia sem instalação

`python scripts/build_preview.py` gera `/tmp/nuclaud-preview/NUCLAUD-v8.html`. Baixe e abra no navegador. Essa versão usa armazenamento local do navegador, separado do SQLite e dos arquivos de prévia anteriores. A fonte de interface é a mesma do servidor. Referenciais antigos são atualizados nos registros carregados; não há migração automática de planos legados.

## Verificação

`python -m unittest discover -s tests -v` testa API com banco temporário. `node --check static/app.js` verifica sintaxe.

## Limites

Protótipo para avaliação em ambiente restrito: sem autenticação, perfis de acesso, anexos, revisão formal, exclusão ou edição de constatações. Requisitos informados por texto, sem reproduzir documentos licenciados. E-mails dependem de aplicativo configurado no dispositivo. Não há integração com o sistema de PACs.

A programação usa um campo Áreas e um campo Normas, ambos de seleção múltipla. Abra o campo e marque as opções cadastradas. Os cadastros ficam exclusivamente nos menus laterais. As áreas selecionadas persistem em area_names; area mantém um resumo legível para documentos e compatibilidade.

Teste de regressão da prévia: `python scripts/build_preview.py && python scripts/check_preview.py` (requer Playwright e Chromium em `/usr/bin/chromium`). Verifica cadastro, seleção de áreas, programação, recarga e armazenamento bloqueado.

## Cadastros de auditores e requisitos

Cadastrar auditores: nome e habilitação por norma (não habilitado, auditor líder ou somente auditor). O líder também pode integrar equipes como auditor. Cadastrar requisitos: norma, código e descrição; códigos iguais são permitidos em normas diferentes, mas não duplicados na mesma norma.

Na programação e no plano, selecione primeiro as normas. O líder deve estar habilitado como líder em todas as normas selecionadas; a equipe admite pessoas habilitadas em pelo menos uma delas. A mesma pessoa não pode ser líder e membro da equipe simultaneamente. Selecione pelo menos um requisito por norma. Os dados são copiados da programação para o plano. Identificadores dos cadastros e textos selecionados são salvos juntos, preservando a leitura dos registros antigos; ao editar seleções antigas, associe-as aos novos cadastros.

Os novos cadastros persistem no SQLite do servidor ou no armazenamento da prévia. Ainda não há edição/exclusão dos cadastros. `scripts/check_preview.py` valida cadastro e salvamento, filtros de habilitação, requisitos por norma, plano e recarga, incluindo o cenário de armazenamento bloqueado.

## Normas e funções independentes

O menu Cadastrar normas e requisitos permite incluir novas normas além das iniciais, que agora incluem Secretaria Naval. Normas cadastradas aparecem na habilitação dos auditores, no cadastro de requisitos, na programação e no plano.

Por norma, cada pessoa pode ter Auditor líder, Auditor e/ou Observador em caixas independentes. Apenas a função Auditor habilita a seleção na equipe de auditores; Observador habilita a lista separada de observadores. Uma pessoa não ocupa duas funções na mesma auditoria. Para preservar o comportamento anterior, registros antigos com a função líder são migrados para líder e auditor; os novos registros respeitam exclusivamente as caixas marcadas.

Cadastros de áreas e normas: os formulários ficam diretamente na página do menu lateral, sem janela modal. Preencha Nome da área ou Nome da norma e clique em Salvar. A confirmação aparece junto ao campo e o registro aparece na lista. Erros, como duplicidade, são exibidos no próprio formulário.
