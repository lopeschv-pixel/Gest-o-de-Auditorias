#!/usr/bin/env bash
# Inicia o SQL Server (contêiner) e o NUCLAUD em modo de testes no Codespaces/devcontainer.
# Pode ser executado várias vezes: reaproveita o que já estiver rodando.
set -eu
cd "$(dirname "$0")/.."
ENV_ARQ=.devcontainer/.env
[ -f "$ENV_ARQ" ] || bash scripts/gerar-env.sh "$ENV_ARQ"
set -a; . "$ENV_ARQ"; set +a
PORTA="${PORTA:-5251}"
SQL_NOME="${SQL_NOME:-nuclaud-sql-dev}"
SQL_PORTA="${SQL_PORTA:-1433}"
LOG="${LOG:-/tmp/nuclaud.log}"

if pgrep -f "NuClaud.Web" >/dev/null && [ "$PORTA" = "5251" ]; then echo "NUCLAUD já está em execução."; exit 0; fi

if (echo > "/dev/tcp/127.0.0.1/$SQL_PORTA") 2>/dev/null; then
  echo "SQL Server já disponível na porta $SQL_PORTA."
else
  echo "Aguardando o Docker..."
  for i in $(seq 1 60); do docker info >/dev/null 2>&1 && break; sleep 2; done
  docker info >/dev/null 2>&1 || { echo "ERRO: Docker indisponível no Codespace."; exit 1; }

  if ! docker ps -a --format '{{.Names}}' | grep -qx "$SQL_NOME"; then
    echo "Criando o SQL Server (primeira vez: alguns minutos para baixar)..."
    docker run -d --name "$SQL_NOME" -e ACCEPT_EULA=Y -e MSSQL_PID=Developer \
      -e "MSSQL_SA_PASSWORD=$NUCLAUD_SA_SENHA" -p "$SQL_PORTA:1433" \
      -v "${SQL_NOME}-dados:/var/opt/mssql" mcr.microsoft.com/mssql/server:2022-latest >/dev/null
  else
    docker start "$SQL_NOME" >/dev/null
  fi
  echo "Aguardando o SQL Server ficar pronto..."
  sql_pronto() { docker logs "$SQL_NOME" 2>&1 | grep -c "ready for client connections" | grep -qv '^0$'; }
  for i in $(seq 1 90); do sql_pronto && break; sleep 2; done
  sql_pronto || { echo "ERRO: SQL Server não iniciou. Veja: docker logs $SQL_NOME"; exit 1; }
fi

export ASPNETCORE_ENVIRONMENT=Testes
export ASPNETCORE_URLS="http://0.0.0.0:$PORTA"
export ConnectionStrings__NuClaud="Server=localhost,$SQL_PORTA;Database=NuClaud_Testes;User Id=sa;Password=${NUCLAUD_SA_SENHA};TrustServerCertificate=true"
export Inicializacao__AdminNomeRede=admin
export Inicializacao__AdminSenhaInicial="${NUCLAUD_ADMIN_SENHA_INICIAL}"
export Demonstracao__SenhaUsuarios="${NUCLAUD_DEMO_SENHA:-}"
export DataProtection__CaminhoChaves="${HOME}/.nuclaud-chaves"
setsid nohup dotnet run --project src/NuClaud.Web --no-launch-profile > "$LOG" 2>&1 < /dev/null &
echo "NUCLAUD iniciando na porta $PORTA (log em $LOG). Usuário: admin. Senha inicial em $ENV_ARQ"
