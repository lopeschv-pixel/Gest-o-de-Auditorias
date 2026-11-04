#!/usr/bin/env bash
# Gera um arquivo .env local com senhas aleatórias para o ambiente de testes (não versionado).
set -euo pipefail
destino="${1:-.env}"
if [ -f "$destino" ]; then echo "$destino já existe; nada foi alterado."; exit 0; fi
r() { head -c 64 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 16; }
cat > "$destino" <<FIM
NUCLAUD_SA_SENHA=Sa$(r)9!
NUCLAUD_ADMIN_SENHA_INICIAL=Adm$(r)7
NUCLAUD_DEMO_SENHA=Demo$(r)5
FIM
chmod 600 "$destino"
echo "Arquivo $destino criado. Senha inicial do administrador (usuário 'admin'):"
grep NUCLAUD_ADMIN_SENHA_INICIAL "$destino" | cut -d= -f2
