#!/usr/bin/env bash
# Inicia o NUCLAUD em modo de testes dentro do Codespaces/devcontainer (porta 5251).
set -euo pipefail
export ASPNETCORE_ENVIRONMENT=Testes
export ASPNETCORE_URLS=http://0.0.0.0:5251
export ConnectionStrings__NuClaud="Server=localhost,1433;Database=NuClaud_Testes;User Id=sa;Password=${NUCLAUD_SA_SENHA};TrustServerCertificate=true"
export Inicializacao__AdminNomeRede=admin
export Inicializacao__AdminSenhaInicial="${NUCLAUD_ADMIN_SENHA_INICIAL}"
export Demonstracao__SenhaUsuarios="${NUCLAUD_DEMO_SENHA:-}"
export DataProtection__CaminhoChaves="${HOME}/.nuclaud-chaves"
for i in $(seq 1 30); do (echo > /dev/tcp/127.0.0.1/1433) 2>/dev/null && break; sleep 2; done
nohup dotnet run --project src/NuClaud.Web --no-launch-profile > /tmp/nuclaud.log 2>&1 &
echo "NUCLAUD iniciando na porta 5251 (log em /tmp/nuclaud.log). Usuário: admin. Senha inicial em .devcontainer/.env"
