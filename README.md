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
- Cadastre normas e requisitos. Cada requisito tem norma, código e título, sem campo descrição. Os títulos pré-carregados são resumos em português dos tópicos das fontes; subitens ANSN sem título próprio herdam o tópico superior. Administradores podem editar o título sem alterar norma/código. O catálogo inicial já contém 339 códigos de fontes oficiais; veja edições e abrangência em [REQUIREMENTS.md](REQUIREMENTS.md).
- Cadastre auditores com habilitações independentes por norma: Auditor líder, Auditor e Observador. Funções antigas foram preservadas na migração.
- Na programação, selecione tipo, ano, uma ou mais áreas, uma ou mais normas, mês, líder, equipe, observadores e requisitos cadastrados. Não há título nem cadastro de áreas dentro da programação. Códigos automáticos: AUDI-I-001-2027 / AUDI-F-001-2027, com sequência por tipo e ano. Tipo e ano ficam preservados após gerar o código.
- O líder precisa ter habilitação de líder em todas as normas; membros/observadores precisam da respectiva função em pelo menos uma norma. Uma pessoa não ocupa duas funções na mesma auditoria. Pelo menos um requisito por norma deve ser selecionado.
- Pelo calendário, gere o plano. Ele aproveita a programação, permite alterar a equipe e os requisitos, adicionar vários dias/períodos com horário de início/fim por dia, objetivo/escopo, local e e-mail do gestor. É possível ter períodos separados no mesmo dia; sobreposição e fim anterior ao início são bloqueados.
- Confirmar o plano habilita PDF e preparação de e-mail. O PDF usa a impressão do navegador. O e-mail abre no aplicativo configurado, já preenchido; envio é manual, assim como anexar PDF.
- Gerar relatório fica dentro da auditoria, acessada pelo calendário. Não há menu lateral de relatório. Registre participantes e constatações classificadas como conformidade, não conformidade, observação ou oportunidade de melhoria. Conclusão é exclusiva de auditorias de fornecedores. Os nomes da equipe vêm do plano de auditoria. Não conformidades exigem o número do PAC; tratamento em sistema externo.

## Planejamento e ranqueamento

Planejamento de auditores mostra, por pessoa e ano do programa, as auditorias designadas, função e dias/horários (ou mês previsto quando não há plano). Ranqueamento de auditores é o subitem, com ordenação por mais/menos pontos.

- Líder: 2 pontos por auditoria; auditor: 1; observador: 0.
- Uma auditoria é contada uma única vez por pessoa, sem multiplicar por dias, períodos ou normas.
- O plano salvo, inclusive rascunho, substitui a equipe da programação para o cálculo. Não há soma duplicada entre fases.
- Todos os auditores cadastrados aparecem no ranqueamento, incluindo os com zero pontos. Os totais são calculados dos registros atuais; alterações de equipe não acumulam pontos antigos.

## Demonstração no GitHub Pages

`python scripts/build_preview.py` gera `/tmp/nuclaud-preview/NUCLAUD-v12.html` a partir da mesma interface do servidor. Publicado como index.html na branch gh-pages. Ativação do Pages depende das configurações do repositório; publicação só é confirmada quando o provedor concluir a implantação.

A demonstração usa armazenamento local do navegador, separado do SQLite. Mantém a mesma chave de dados das prévias anteriores. Se o navegador bloquear armazenamento, os registros ficam apenas na sessão e há um aviso. Não limpar/resetar o armazenamento para atualizar o aplicativo.

Planos antigos com uma data continuam visíveis, sem inventar horários; ao editar, informe os horários. Descrições antigas de requisitos ficam preservadas nos registros históricos, mas não são exigidas ou exibidas no cadastro novo.

## Verificação

- `python -m unittest discover -s tests -v`: API, banco temporário, requisitos sem descrição e períodos múltiplos/invalidade.
- `node tests/test_planning.cjs`: pontuação, troca da equipe no plano, filtros anuais, zeros, ordenação, compatibilidade e períodos sobrepostos.
- `python scripts/build_preview.py && python scripts/check_preview.py`: teste no Chromium/Playwright dos cadastros, seleção múltipla, plano com dois dias, PDF/e-mail, planejamento, ranking e persistência, inclusive armazenamento bloqueado. Requer Playwright e `/usr/bin/chromium`.

## Limites

Protótipo com autenticação local e autorização no servidor; a demonstração estática Pages apenas simula perfis. Ainda não há integração com o diretório corporativo, anexos, revisão formal, exclusão, edição geral de usuários/auditores/áreas ou edição de constatações. Use dados fictícios na demonstração pública. Não há integração com o sistema de PACs. O catálogo é um índice de códigos: nas ISO, a abrangência é a do sumário da prévia oficial; não é o texto integral das normas. ASME III e Secretaria Naval não foram pré-carregadas por orientação do usuário.

## Administração e acesso

O menu Administração do sistema agrupa Cadastrar usuários, Cadastrar áreas, Cadastrar auditores e Cadastrar normas e requisitos. Usuários contêm nome de rede, nome completo, e-mail, perfil (Administrador ou Usuário) e vínculo ao auditor, obrigatório no perfil Usuário. Esse auditor precisa ter habilitação de líder.

- Administrador: acesso a todos os módulos e cadastros.
- Usuário: calendário somente com auditorias em que é líder, geração/edição dos dias e horários e confirmação do plano, emissão de plano e elaboração do relatório/constatações dessas auditorias. Não programa auditorias, altera cadastros, transfere liderança ou acessa auditorias de outro líder.
- O servidor aplica as permissões também nos endpoints; esconder menus não é o único controle.

### Servidor

No primeiro acesso, configure o administrador inicial pela interface. Não existe conta/senha padrão. Em localhost sem encaminhamento de proxy, a configuração inicial é permitida. Para instalação remota ou atrás de proxy, configure NUCLAUD_SETUP_TOKEN no servidor e informe essa chave no formulário inicial; o Render gera uma chave automaticamente pela configuração render.yaml. Não compartilhe a chave em chat nem a coloque no Git.

O login usa nome de rede e senha **local**; não autentica no Active Directory. Cadastros feitos pelo administrador geram senha temporária exibida uma vez. O usuário pode alterar sua senha após entrar. Senhas são derivadas com PBKDF2-SHA256 e salt; a API não retorna hashes. Sessões em cookies HttpOnly/SameSite com validade de oito horas, encerradas ao sair; mudança de senha invalida as sessões anteriores. Use HTTPS para implantação remota. /healthz é público para monitoramento; /api/data exige login e filtra registros conforme o perfil.

### Demonstração GitHub Pages

Não existe backend no Pages. O administrador da demonstração é um perfil fictício local, sem senha, criado apenas nesse modo. O cabeçalho exibe “Prévia: perfis simulados”. Use Perfil de teste para alternar entre contas cadastradas e testar menus e fluxos. Esses controles no navegador não protegem dados contra quem alterar o armazenamento local; não são autenticação de produção e não sincronizam registros entre computadores.

### Testes de acesso

`python -m unittest discover -s tests -v` também testa login, dados inacessíveis sem sessão, permissões em chamadas diretas, programação imutável para usuários, auditorias de outros líderes, criação de usuários, senhas, logout e expiração.

`node tests/test_access_policy.cjs` testa a política da demonstração. `python scripts/check_preview.py` testa Administração, cadastro de usuário, títulos e alternância de perfis. `python scripts/check_server_access.py` testa no navegador o servidor real: primeiro administrador, login, logout e plano/relatório com perfil Usuário.

Na Administração, cadastre macroprocessos e seus processos vinculados. A programação exige selecionar um ou mais macroprocessos e pelo menos um processo de cada macroprocesso escolhido. O plano e o relatório apresentam essas informações. Registros antigos são preservados; ao editar a programação, complete os novos campos.

`node tests/test_reporting.cjs` valida as classificações e a composição dos documentos. `python scripts/check_reports_pdf.py` verifica os formulários no navegador e gera PDFs A4 de plano, relatório interno longo e relatório de fornecedor, conferindo conteúdo e margens.

A programação e o plano oferecem requisitos de dois níveis (ex.: 4.1), ordenados numericamente por norma. O auditor líder é escolhido por marcação única. Seletores múltiplos fecham ao clicar fora ou em Concluir seleção. O plano inclui nome/e-mail do gestor; objetivo e escopo aparecem apenas para fornecedores.

Conformidades registram requisitos e evidência encontrada, sem descrição adicional. O botão Concluir relatório exige que todos os IDs de requisitos do plano tenham uma conformidade ou não conformidade; observações e oportunidades não substituem essa classificação. O servidor aplica a mesma regra. O PDF inclui espaço para assinatura do auditor líder e data.

Preparar e-mail baixa um rascunho `.eml` com destinatário, mensagem e plano PDF anexado, gerado localmente com jsPDF 4.2.1 (MIT, licença preservada em static/vendor). Abra o arquivo em um aplicativo de e-mail compatível para revisar e enviar; a abertura automática de rascunhos depende do aplicativo/sistema operacional. Não envia e-mails pelo servidor nem usa mailto para anexos.

Relatórios em elaboração oferecem Salvar relatório, Editar constatação e Excluir constatação em cada registro, inclusive dentro do editor do relatório. Alterações em constatações preservam seu ID, verificam a liderança da auditoria e recalculam a cobertura dos requisitos. A conclusão apresenta os códigos/títulos pendentes antes de verificar os demais campos; não exige salvar um rascunho vazio previamente. Relatórios concluídos ficam bloqueados para usuários comuns na interface e na API. Administradores podem corrigir ou excluir constatações; a alteração reabre o relatório para uma nova conclusão com validação de todos os requisitos. A classificação acompanha o número da constatação; botões de edição não aparecem nos documentos impressos.

O calendário oferece filtro por norma (incluindo Todas as normas), combinado com o ano e a busca. Usa as normas do plano quando existente, ou da programação. Cores: concluída cinza, ISO 9001 azul, ANSN 1.16 + Secretaria Naval laranja, ISO 14001 + ISO 45001 verde e ASME III amarelo. Quando há mais de uma família na mesma auditoria, a prioridade é concluída, ASME, ANSN/Naval, SMS e ISO 9001. Outras combinações conservam a cor padrão.

`python scripts/check_calendar_reports.py` testa filtro, cores, exclusão/cancelamento, correção administrativa e bloqueio de edição por usuários comuns após a conclusão.

Editar programação está disponível na lista anual, no cartão do calendário e na auditoria para administradores, preservando código, tipo e ano. Cada auditor cadastrado oferece Editar auditor para corrigir nome e habilitações; o ID permanece, preservando vínculos de usuários e auditorias. Documentos existentes mantêm seus dados históricos; os cadastros atualizados alimentam novas seleções.

No plano, administradores podem editar todos os campos. O líder tem um formulário apenas de dias/horários, com os demais dados visíveis para consulta. A API também rejeita alterações em normas, requisitos, equipe, local e gestor feitas por usuários comuns. O primeiro plano do líder herda a programação; novas datas exigem confirmar novamente. Planos de relatórios iniciados continuam preservados para rastreabilidade.
