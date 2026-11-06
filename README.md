# NUCLAUD — Gestão de auditorias da NUCLEP

Sistema web para o ciclo de auditorias internas e de fornecedores: cadastros, programação anual,
calendário, plano de auditoria, relatório com constatações, conclusão controlada, PDFs e planejamento/ranqueamento de auditores.

- **Linguagem/plataforma:** C# · ASP.NET Core 10 (LTS) · Blazor Web App (Interactive Server)
- **Persistência:** Entity Framework Core 10 · SQL Server (migrações versionadas)
- **PDF:** PDFsharp/MigraDoc 6.2.4 (licença MIT) · fontes embutidas (Carlito, OFL)
- **Autenticação:** local (cookie seguro + hash PBKDF2), preparada para AD/Entra ID
- **Interface:** português do Brasil

> O NUCLAUD apenas **registra** o número do PAC e do RQF. O tratamento desses documentos acontece em outros sistemas.

## Sumário da documentação

| Documento | Conteúdo |
|---|---|
| [docs/ARQUITETURA.md](docs/ARQUITETURA.md) | Arquitetura, versões, decisões técnicas e licenças |
| [docs/IMPLANTACAO.md](docs/IMPLANTACAO.md) | Requisitos do servidor, configuração, publicação, migrações, backup, restauração, atualização e **checklist para a TI** |
| [docs/REGRAS.md](docs/REGRAS.md) | Regras de negócio e permissões implementadas |
| [docs/CARGA-NORMAS.md](docs/CARGA-NORMAS.md) | Carga inicial de normas e requisitos: fontes, edições e pendências |
| [docs/TESTES.md](docs/TESTES.md) | Testes automatizados, testes de interface e situação de cada critério de aceitação |

## Estrutura

```
NuClaud.slnx
src/NuClaud.Core/      Domínio, EF Core (DbContext, migrações), serviços com regras e permissões, PDFs
src/NuClaud.Web/       Aplicação Blazor (telas, autenticação, endpoints de PDF, estilos e fontes locais)
tests/NuClaud.Tests/   Testes xUnit (regras, permissões, relatório, códigos, pontuação, PDFs, SQL Server)
deploy/sql/            Script SQL idempotente das migrações (para o DBA)
scripts/               Utilitários (gerar .env de testes, iniciar no Codespaces)
.devcontainer/         Ambiente para testar no navegador via GitHub Codespaces (opcional)
Dockerfile, docker-compose.yml   Execução opcional em contêiner (não é exigida)
```

## Executar localmente (desenvolvimento)

Pré-requisitos: [.NET SDK 10](https://dotnet.microsoft.com/) e um SQL Server acessível (Developer/Express/Standard 2019+).

```bash
dotnet tool restore
```

Defina a conexão e o administrador inicial **fora do repositório** (user-secrets):

```bash
cd src/NuClaud.Web
dotnet user-secrets set "ConnectionStrings:NuClaud" "Server=localhost;Database=NuClaud_Dev;User Id=...;Password=...;TrustServerCertificate=true"
dotnet user-secrets set "Inicializacao:AdminNomeRede" "admin"
dotnet user-secrets set "Inicializacao:AdminSenhaInicial" "UmaSenhaForte2026"
```

```bash
dotnet run --project src/NuClaud.Web --launch-profile Desenvolvimento
```

Acesse `http://localhost:5251`. No ambiente `Development` as migrações são aplicadas na inicialização.
No primeiro acesso o administrador deve trocar a senha.

### Ambiente de testes com dados fictícios

O perfil `Testes` (`ASPNETCORE_ENVIRONMENT=Testes`) carrega dados **fictícios** de demonstração (áreas, processos,
auditores, auditorias, um relatório concluído) somente se o banco estiver vazio, e exibe a faixa
“Ambiente de demonstração e testes” em todas as telas. Defina `Demonstracao__SenhaUsuarios` para criar os usuários
de exemplo `ana.ribeiro`, `carla.mendes` e `bruno.carvalho` (perfil Usuário, vinculados a auditores).
**Nunca habilite a demonstração em produção.**

### Com Docker (opcional)

```bash
bash scripts/gerar-env.sh
```

```bash
docker compose up -d --build
```

Acesse `http://localhost:5251` com o usuário `admin` e a senha exibida pelo script.

### Testar no navegador sem instalar nada (GitHub Codespaces)

Com o repositório no GitHub: **Code › Codespaces › Create codespace**. O Codespace liga o SQL Server em contêiner (Docker), gera senhas
aleatórias em `.devcontainer/.env` (não versionado) e inicia o NUCLAUD na porta 5251, que o Codespaces abre em uma URL
privada do navegador. Usuário `admin`; senha inicial em `.devcontainer/.env` (`NUCLAUD_ADMIN_SENHA_INICIAL`).

## Testes automatizados

```bash
dotnet test
```

Para incluir os testes de integração com SQL Server real (migrações + criação simultânea de códigos):

```bash
NUCLAUD_TESTE_SQLSERVER="Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=true" dotnet test
```

## Publicação no servidor corporativo

Resumo (detalhes, opções Windows/IIS, Linux/systemd, contêiner, backup e atualização em [docs/IMPLANTACAO.md](docs/IMPLANTACAO.md)):

```bash
dotnet publish src/NuClaud.Web -c Release -o ./publish
```

1. Confirmar com a TI os itens do checklist (sistema operacional, .NET, SQL Server, autenticação, HTTPS, rede, backups).
2. Aplicar `deploy/sql/nuclaud-migracoes-idempotente.sql` no banco de produção (ou habilitar `BancoDeDados:AplicarMigracoesAoIniciar`).
3. Configurar variáveis de ambiente (`ConnectionStrings__NuClaud`, `Inicializacao__*`, `DataProtection__CaminhoChaves`, `ASPNETCORE_ENVIRONMENT=Production`).
4. Hospedar atrás de HTTPS (IIS, Nginx, Apache ou balanceador corporativo).
