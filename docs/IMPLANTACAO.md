# Implantação, operação e manutenção

> A configuração usada no ambiente de desenvolvimento/testes (Linux + SQL Server 2022 em contêiner) **não é uma
> implantação de produção aprovada**. A forma final depende das confirmações da TI listadas abaixo.

## 1. Itens a confirmar com a equipe de TI

| Item | Por que importa | Opções suportadas |
|---|---|---|
| Sistema operacional do servidor | Forma de hospedar e serviço de inicialização | Windows Server 2019+ (IIS) · Linux (systemd + Nginx/Apache) · contêiner |
| Versões do .NET permitidas | O NUCLAUD usa **.NET 10 LTS** (ASP.NET Core Runtime 10) | Instalar *ASP.NET Core Runtime 10* / *Hosting Bundle* no Windows |
| Banco de dados e versão | Provedor e migrações são SQL Server | SQL Server 2019 ou superior (Express, Standard, Enterprise, Azure SQL) |
| Autenticação corporativa | Integração futura | AD (Windows/Kerberos) ou Microsoft Entra ID (OIDC) — ver ARQUITETURA.md |
| Forma de hospedagem | Proxy, porta, WebSockets | IIS (habilitar WebSocket Protocol) · Nginx/Apache como proxy reverso |
| Certificado HTTPS | Cookie seguro e HSTS | Certificado corporativo no IIS/proxy |
| Permissões de rede | App ↔ SQL Server (1433/TCP) e usuários ↔ app (443) | Firewall interno |
| Armazenamento de chaves e backups | Chaves de proteção de dados e cópias do banco | Pasta persistente com ACL restrita; política de backup do SQL Server |
| Processo de atualização | Janela, aprovação, homologação | Ver seção 7 |
| Conta de serviço | Execução com privilégio mínimo | Conta dedicada sem privilégio de administrador |
| Logs | Retenção e destino | Console/Event Log/journald ou provedor corporativo |

## 2. Requisitos do servidor
- ASP.NET Core Runtime 10.0 (x64). No Windows/IIS: **.NET 10 Hosting Bundle**.
- 2 vCPU, 4 GB RAM, 1 GB de disco para a aplicação (estimativa para dezenas de usuários simultâneos).
- SQL Server 2019+ com um banco dedicado (ex.: `NuClaud`).
- Nenhum serviço externo/Internet é necessário: fontes, estilos, scripts e imagens são servidos pela própria aplicação.

## 3. Banco de dados
1. O DBA cria o banco e um login dedicado. Para operação: `db_datareader` + `db_datawriter`.
   Para aplicar migrações pela aplicação também `db_ddladmin` (alternativa: o DBA aplica o script).
   A geração de códigos usa `sp_getapplock` (disponível a qualquer usuário do banco).
2. Aplicar as migrações (escolha uma opção):
   - **Script (recomendado em produção):** executar `deploy/sql/nuclaud-migracoes-idempotente.sql` (pode ser executado várias vezes). No SSMS, basta abrir e executar; com `sqlcmd`, use a opção `-I` (QUOTED_IDENTIFIER ligado), por exemplo `sqlcmd -S servidor -d NuClaud -I -i nuclaud-migracoes-idempotente.sql`.
     Para regenerar após novas migrações:
     ```bash
     dotnet ef migrations script --idempotent -p src/NuClaud.Core -s src/NuClaud.Core -o deploy/sql/nuclaud-migracoes-idempotente.sql
     ```
   - **Pela aplicação:** `BancoDeDados__AplicarMigracoesAoIniciar=true` (usa bloqueio para evitar aplicação simultânea).
   - **Ferramenta:** `NUCLAUD_CONEXAO="..." dotnet ef database update -p src/NuClaud.Core -s src/NuClaud.Core`.
3. Na primeira inicialização o NUCLAUD carrega as normas iniciais (idempotente) e cria o administrador inicial
   se não houver usuários.

## 4. Configuração (sem segredos no repositório)

As configurações podem vir de variáveis de ambiente (separador `__`), de `appsettings.Production.json` **fora do controle de versão**
ou de um cofre corporativo. Principais chaves:

| Chave | Exemplo / observação |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` (ou `Testes` para homologação) |
| `ConnectionStrings__NuClaud` | `Server=sqlprod;Database=NuClaud;User Id=nuclaud_app;Password=***;Encrypt=true` (ou `Integrated Security=true` com conta de serviço) |
| `Inicializacao__AdminNomeRede` / `__AdminSenhaInicial` / `__AdminNomeCompleto` / `__AdminEmail` | Usados só quando o banco não tem usuários. A senha é temporária; remova a variável após o primeiro acesso |
| `DataProtection__CaminhoChaves` | Pasta persistente (ex.: `D:\NuClaud\chaves` ou `/var/lib/nuclaud/chaves`). Sem ela, reinícios podem encerrar sessões |
| `Seguranca__CookieSomenteHttps` | `true` em produção |
| `Seguranca__RedirecionarHttps` | `true` se o próprio app receber HTTP e HTTPS |
| `ProxyReverso__Habilitado` / `ProxyReverso__ProxiesConhecidos__0` | Quando atrás de Nginx/Apache/balanceador |
| `BancoDeDados__AplicarMigracoesAoIniciar` | `false` em produção se o DBA aplicar o script |
| `Demonstracao__Habilitada` | **sempre `false` em produção** |

Ambientes: `appsettings.json` (base), `appsettings.Development.json`, `appsettings.Testes.json`, `appsettings.Production.json`.

## 5. Publicação

```bash
dotnet publish src/NuClaud.Web -c Release -o ./publish
```

### Windows Server + IIS
1. Instalar o *.NET 10 Hosting Bundle* e habilitar o recurso **WebSocket Protocol** do IIS.
2. Copiar `publish` para `D:\NuClaud\app`; criar site/aplicação com pool “Sem código gerenciado”, identidade de conta de serviço.
3. Definir as variáveis de ambiente no `web.config` (`<environmentVariables>`) ou no pool — sem versionar segredos.
4. Vincular o certificado HTTPS no site.

### Linux + systemd + Nginx
`/etc/systemd/system/nuclaud.service`:
```ini
[Unit]
Description=NUCLAUD
After=network.target
[Service]
WorkingDirectory=/opt/nuclaud
ExecStart=/usr/bin/dotnet /opt/nuclaud/NuClaud.Web.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://127.0.0.1:5000
EnvironmentFile=/etc/nuclaud/nuclaud.env
User=nuclaud
Restart=always
[Install]
WantedBy=multi-user.target
```
Nginx: `proxy_pass http://127.0.0.1:5000;` com `proxy_http_version 1.1; proxy_set_header Upgrade $http_upgrade; proxy_set_header Connection "upgrade"; proxy_set_header X-Forwarded-Proto $scheme;` e `ProxyReverso__Habilitado=true`.

### Contêiner (opcional)
`Dockerfile` na raiz; montar `/dados` (chaves) e informar as variáveis acima. `docker-compose.yml` é apenas para testes.

Verificação: `GET /saude` retorna `ok` quando a aplicação alcança o banco.

## 6. Backup e restauração
**O que precisa de backup:** o banco SQL Server (todos os dados, histórico e o logotipo) e a pasta de chaves
(`DataProtection__CaminhoChaves`; se perdida, os usuários apenas precisam entrar novamente).

Backup (exemplo, ajustar à política corporativa — completo diário + log a cada hora com modelo FULL):
```sql
BACKUP DATABASE [NuClaud] TO DISK = N'E:\Backups\NuClaud_full.bak' WITH COMPRESSION, CHECKSUM, INIT;
BACKUP LOG [NuClaud] TO DISK = N'E:\Backups\NuClaud_log.trn' WITH COMPRESSION, CHECKSUM;
```
Restauração:
1. Parar a aplicação (pool do IIS / `systemctl stop nuclaud`).
2. ```sql
   RESTORE DATABASE [NuClaud] FROM DISK = N'E:\Backups\NuClaud_full.bak' WITH REPLACE, NORECOVERY;
   RESTORE LOG [NuClaud] FROM DISK = N'E:\Backups\NuClaud_log.trn' WITH RECOVERY;
   ```
3. Se o backup for de versão anterior do NUCLAUD, aplicar o script de migrações da versão atual.
4. Iniciar a aplicação e verificar `/saude`.
Teste a restauração periodicamente em ambiente de homologação.

## 7. Atualização de versão
1. Fazer backup do banco.
2. Publicar a nova versão em pasta nova (ex.: `app-2027.05`), sem sobrescrever a anterior (permite voltar).
3. Aplicar o script idempotente de migrações da nova versão.
4. Trocar o caminho do site/serviço para a nova pasta e reiniciar.
5. Validar `/saude`, entrar e gerar um PDF. Em caso de problema: voltar a pasta anterior e, se a migração tiver alterado
   o esquema de forma incompatível, restaurar o backup.
O logotipo e todos os dados ficam no banco, portanto são preservados nas atualizações.

## 8. Logs
Os logs vão para o console/provedor padrão do ASP.NET Core (IIS: stdout/Event Log; Linux: journald). Não registram senhas
nem cadeias de conexão; consultas SQL só aparecem em nível `Information` se configurado explicitamente.
