#!/usr/bin/env bash
# Gera o pacote local do NUCLAUD para Windows 10/11 (x64), com o .NET embutido.
# Uso: bash scripts/gerar-pacote-windows.sh [pasta-de-saida]
set -euo pipefail
cd "$(dirname "$0")/.."
SAIDA="${1:-publish/pacote-windows}"
rm -rf "$SAIDA" && mkdir -p "$SAIDA/NUCLAUD"
dotnet publish src/NuClaud.Web -c Release -r win-x64 --self-contained true \
  -p:DebugType=none -p:DebugSymbols=false -p:SatelliteResourceLanguages=pt-BR -o "$SAIDA/NUCLAUD/app"
find "$SAIDA/NUCLAUD/app" -name "*.pdb" -delete
cp "scripts/windows/Iniciar NUCLAUD.bat" scripts/windows/LEIA-ME.txt "$SAIDA/NUCLAUD/"
(cd "$SAIDA" && rm -f NUCLAUD-Windows.zip && zip -qr NUCLAUD-Windows.zip NUCLAUD)
echo "Pacote gerado: $SAIDA/NUCLAUD-Windows.zip"
