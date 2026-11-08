# Arquitetura e decisões técnicas

## Versões

| Componente | Versão | Licença |
|---|---|---|
| .NET / ASP.NET Core | 10.0 (LTS, suporte até nov/2028) — runtime testado 10.0.14 | MIT |
| Entity Framework Core + provedor SQL Server | 10.0.14 | MIT |
| SQL Server (testado) | 2022 Developer (contêiner `mcr.microsoft.com/mssql/server:2022-latest`) | — |
| PDFsharp / MigraDoc | **6.2.4** (fixado) | MIT |
| SkiaSharp (validação/normalização do logotipo) | 4.154.0 | MIT |
| ClosedXML (leitura de planilhas .xlsx na importação de processos) | 0.105.1 | MIT (dependências: DocumentFormat.OpenXml MIT, SixLabors.Fonts 1.0 Apache-2.0) |
| Microsoft.Extensions.Identity.Core (somente `PasswordHasher`) | 10.0.14 | MIT |
| Fonte da interface: Inter | servida em `wwwroot/fonts` | SIL OFL 1.1 |
| Fonte dos PDFs: Carlito (métrica compatível com Calibri) | embutida no assembly | SIL OFL 1.1 |
| Testes: xUnit + SQLite em memória (EF Core Sqlite 10.0.14) | — | Apache 2.0 / MIT |

### Por que .NET 10 e não .NET 8
O .NET 8 encerra o suporte em 10/11/2026. O .NET 10 é a LTS vigente. Se a TI só permitir .NET 8,
a migração é possível (troca de `TargetFramework` e pacotes 8.x), mas não é recomendada por causa do fim de suporte.

### Licença do PDFsharp — atenção
A versão **6.5.0** (out/2026) passou para **AGPL‑3.0** e exige licença em tempo de execução; isso não é adequado
para um sistema corporativo de código fechado. O projeto fixa a **6.2.4**, última versão estável sob **MIT**.
Não atualize o pacote sem rever a licença. Alternativas avaliadas: QuestPDF (licença comunitária só para
receita anual < US$ 1 milhão — a NUCLEP exigiria licença paga) e iText (AGPL/comercial).

## Modelo de hospedagem do Blazor: Interactive Server

Escolhido o **Blazor Web App com renderização interativa no servidor**:

- **Simplicidade de implantação:** um único processo ASP.NET Core (sem API separada nem pacote WebAssembly).
- **Segurança:** regras de negócio, consultas e permissões executam somente no servidor; o navegador recebe apenas a interface.
- **Manutenção:** um projeto de interface, um de regras; nenhum JavaScript próprio é necessário.
- **Custo:** cada usuário conectado mantém um circuito SignalR (WebSocket). Para o volume esperado (dezenas de usuários) é irrelevante.
  Exige que o proxy/IIS permita **WebSockets** (há *fallback* para long polling).

As telas **Entrar** e **Alterar senha** usam renderização estática (formulário HTML com antifalsificação),
porque precisam emitir o cookie de autenticação.

## Camadas

```
NuClaud.Web (Blazor)  ──►  NuClaud.Core.Servicos  ──►  NuClaud.Core.Dados (EF Core)  ──►  SQL Server
   telas, cookies,          regras de negócio,           entidades, restrições,
   endpoints de PDF         permissões, histórico        migrações versionadas
```

- **Toda verificação de permissão está nos serviços** (`Permissoes`, `Ator`), independente da visibilidade dos botões.
  As telas e os endpoints de PDF apenas repassam o usuário autenticado, sempre recarregado do banco.
- Serviços recebem DTOs e **nunca alteram o objeto da tela** (cópia defensiva), evitando perda de dados digitados.
- `IDbContextFactory` cria um contexto por operação (requisito do Blazor Server).
- Operações críticas usam **transação** (programação, plano, relatório/conclusão, reabertura, exclusão, usuários).
- **Concorrência otimista:** campo `Versao` (GUID) em cada entidade editável; quem salvar sobre dados alterados por
  outra pessoa recebe a mensagem para recarregar (nunca sobrescreve silenciosamente).

## Dados e histórico

- Chaves primárias internas (`int identity`) permanentes; o **código visível** (AUDI-I-001-2027) é único, mas **não é chave**.
- A auditoria guarda **retratos** (snapshots) dos nomes de normas, áreas, processos, auditores e requisitos
  (`AuditoriaArea.NomeArea`, `AuditoriaRequisito.Codigo/Titulo/Origem`, `AuditoriaParticipante.NomeAuditor` …).
  Renomear ou inativar um cadastro não altera documentos já emitidos. Os retratos são atualizados apenas enquanto o
  relatório não foi iniciado (e somente quando a programação é salva novamente).
- Cadastros referenciados por auditorias não podem ser excluídos (restrição `Restrict` no banco e mensagem explicando que
  devem ser **inativados**).
- `HistoricoAlteracao` registra autor, instante (UTC), ação, resumo e um JSON com antes/depois (processos, programação,
  plano, conclusão, reabertura com retrato da versão concluída, exclusões).
- **Datas e horários:** datas/horas de auditoria são `date`/`time` (horário de negócio, sem conversão de fuso).
  Instantes de histórico são gravados em **UTC** (`datetime2`) e exibidos em **America/Sao_Paulo**.
- O logotipo fica no banco (`ConfiguracoesVisuais`, original + PNG normalizado), portanto entra no backup do banco.

## Segurança

- Senhas com `PasswordHasher` (PBKDF2‑HMAC‑SHA512, 100 mil iterações); política mínima de 10 caracteres com letras e números;
  bloqueio de 15 min após 5 tentativas; troca obrigatória da senha temporária.
- Cookie `HttpOnly`, `SameSite=Lax`, `Secure` em produção, expiração deslizante de 8 h; carimbo de segurança invalida sessões
  ao inativar usuário, mudar perfil, redefinir senha ou mudar vínculo com auditor (cookies e circuitos Blazor revalidados).
- Antifalsificação (antiforgery) em formulários; CSP restritiva (`script-src 'self'`), `X-Frame-Options: DENY`, `nosniff`.
- Upload do logotipo: até 2 MB, assinatura de bytes (PNG/JPEG/WebP), decodificação real e regravação como PNG (descarta metadados).
- Consultas apenas via EF Core (parametrizadas). Erros exibem mensagens úteis sem detalhes internos; detalhes vão ao log.
- Nenhuma credencial no repositório: conexão e senha inicial via variáveis de ambiente/user-secrets/arquivo fora do repositório.

## Preparação para Active Directory / Microsoft Entra ID

A identidade do usuário no NUCLAUD é o **nome da rede** (`Usuario.NomeRede`). Para integrar:

- **Entra ID (OpenID Connect):** adicionar `Microsoft.Identity.Web`, registrar o app no Entra, e no evento `OnTokenValidated`
  localizar o `Usuario` pelo `preferred_username`/`onPremisesSamAccountName` e emitir as mesmas claims de
  `ClaimsNuclaud.Criar`. Perfis continuam administrados no NUCLAUD.
- **AD on‑premises (Windows/Kerberos):** `AddAuthentication(NegotiateDefaults...)` no IIS/Kestrel, mapeando `DOMINIO\usuario` para `NomeRede`.
- O restante (permissões, `Ator`, revalidação) não muda. A autenticação local pode ser mantida como contingência ou desativada.
