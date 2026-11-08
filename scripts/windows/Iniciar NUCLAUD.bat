@echo off
setlocal EnableExtensions EnableDelayedExpansion
title NUCLAUD - gestao de auditorias (uso local)
cd /d "%~dp0"

echo.
echo  ============================================================
echo   NUCLAUD - instalacao local para demonstracao e testes
echo  ============================================================
echo.

rem --- 1. Verifica o banco de dados local (SQL Server Express LocalDB)
where sqllocaldb >nul 2>nul
if errorlevel 1 (
  echo  [ATENCAO] O SQL Server Express LocalDB nao foi encontrado neste computador.
  echo      Siga o PASSO 1 do arquivo LEIA-ME.txt e depois abra este arquivo de novo.
  echo.
  pause
  exit /b 1
)
sqllocaldb create MSSQLLocalDB >nul 2>nul
sqllocaldb start MSSQLLocalDB >nul 2>nul

rem --- 2. Senhas iniciais (geradas uma unica vez e guardadas so neste computador)
set "DADOS=%LOCALAPPDATA%\NUCLAUD"
if not exist "%DADOS%" mkdir "%DADOS%"
if not exist "%DADOS%\senhas-iniciais.txt" (
  set "S1=Nuclaud!RANDOM!!RANDOM!x"
  set "S2=Demonstra!RANDOM!!RANDOM!x"
  > "%DADOS%\senhas-iniciais.txt" echo !S1!
  >> "%DADOS%\senhas-iniciais.txt" echo !S2!
)
set /p ADMSENHA=<"%DADOS%\senhas-iniciais.txt"
for /f "usebackq skip=1 delims=" %%L in ("%DADOS%\senhas-iniciais.txt") do set "DEMOSENHA=%%L"

rem --- 3. Configuracao (somente para este computador)
set "ASPNETCORE_ENVIRONMENT=Testes"
set "ASPNETCORE_URLS=http://localhost:5251"
set "ConnectionStrings__NuClaud=Server=(localdb)\MSSQLLocalDB;Database=NuClaud_Local;Trusted_Connection=True;TrustServerCertificate=True"
set "Inicializacao__AdminNomeRede=admin"
set "Inicializacao__AdminNomeCompleto=Administrador do NUCLAUD"
set "Inicializacao__AdminSenhaInicial=%ADMSENHA%"
set "Demonstracao__SenhaUsuarios=%DEMOSENHA%"
set "DataProtection__CaminhoChaves=%DADOS%\chaves"

echo  Endereco:  http://localhost:5251   (o navegador abre sozinho em alguns segundos)
echo.
echo  Primeiro acesso:
echo     Nome da rede: admin
echo     Senha:        %ADMSENHA%
echo     (o sistema pede uma senha nova; depois use a senha que voce criou)
echo.
echo  Usuarios de exemplo: ana.ribeiro, carla.mendes, bruno.carvalho
echo     Senha inicial:  %DEMOSENHA%
echo.
echo  Para DESLIGAR o sistema, feche esta janela.
echo  ------------------------------------------------------------
echo.

start "" /min cmd /c "timeout /t 15 /nobreak >nul & start http://localhost:5251"
cd /d "%~dp0app"
NuClaud.Web.exe
echo.
echo  O NUCLAUD foi encerrado. Se apareceu algum erro acima, veja "Problemas comuns" no LEIA-ME.txt.
pause
