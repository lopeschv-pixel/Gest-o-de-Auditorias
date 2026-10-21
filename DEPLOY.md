# Publicar a demonstração no Render

Esta configuração serve para testar com dados fictícios. O servidor inclui login e controle de acesso; a prévia GitHub Pages é apenas uma simulação dos perfis. O banco da demonstração é temporário e pode ser apagado em reinícios/republicações do serviço gratuito.

1. Publique este projeto em um repositório GitHub da sua conta.
2. Em https://dashboard.render.com/ escolha New > Blueprint e conecte esse repositório.
3. O Render identifica render.yaml. Revise o plano gratuito e crie o serviço.
4. Aguarde o estado Live. Abra o endereço HTTPS fornecido pelo Render.

Após publicar no Render, consulte a variável NUCLAUD_SETUP_TOKEN gerada pelo provedor e use-a na tela de configuração inicial para criar seu administrador. Não há senha padrão. O campo nome de rede é o identificador da conta local, sem integração com AD. O monitoramento usa /healthz; /api/data retorna 401 antes do login.

A publicação exige uma conta Render e autorização para acessar o repositório. Não inclua dados pessoais ou corporativos nesta demonstração pública. O cadastro de auditores, normas e áreas usa o banco SQLite do servidor. O comando de execução é `python server.py`, respeitando a variável PORT fornecida pelo provedor.

Validação após publicar: abrir a interface, cadastrar uma área, uma norma e um auditor e conferir os registros nas listas. A resposta HTTP 200 em /api/data só verifica o funcionamento da API, não substitui o teste dos formulários.
