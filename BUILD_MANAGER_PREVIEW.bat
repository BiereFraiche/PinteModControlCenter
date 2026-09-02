@echo off
setlocal EnableExtensions
chcp 65001 >nul 2>nul
set "NO_UI=0"
if /I "%~1"=="--no-ui" set "NO_UI=1"

set "ROOT=%~dp0"
set "APP=%ROOT%app"
set "SLN=%APP%\PinteMod.ControlCenter.sln"
set "PROJECT=%APP%\src\PinteMod.ControlCenter\PinteMod.ControlCenter.csproj"
set "OUTROOT=%APP%\artifacts\release-v2.4.5-win-x64"
set "OUT_SINGLE=%OUTROOT%\single-exe"
set "OUT_FOLDER=%OUTROOT%\folder"
set "OUT_SINGLE_ZIP=%OUTROOT%\PinteMod.ControlCenter-single-exe-win-x64.zip"
set "OUT_FOLDER_ZIP=%OUTROOT%\PinteMod.ControlCenter-folder-win-x64.zip"
set "OUT_HASHES=%OUTROOT%\SHA256SUMS.txt"
set "OUT_SELFTEST=%OUTROOT%\SELF-TEST.txt"
set "OUT_SELFTEST_FOLDER_TEMP=%OUTROOT%\SELF-TEST-FOLDER.tmp.txt"
set "PACKAGER=%APP%\packaging\Build-PreviewPackages.ps1"

echo ============================================================
echo  PinteMod Control Center - VERSION STABLE v2.4.5
echo ============================================================
echo.
where dotnet >nul 2>nul
if errorlevel 1 goto :nodotnet

pushd "%APP%"
if errorlevel 1 goto :badpath

echo [1/8] Restore...
dotnet restore "%SLN%"
if errorlevel 1 goto :fail

echo.
echo [2/8] Build Debug...
dotnet build "%SLN%" -c Debug --no-restore
if errorlevel 1 goto :fail

echo.
echo [3/8] Tests Debug...
dotnet test "%SLN%" -c Debug --no-build --no-restore
if errorlevel 1 goto :fail

echo.
echo [4/8] Build Release...
dotnet build "%SLN%" -c Release --no-restore
if errorlevel 1 goto :fail

echo.
echo [5/8] Tests Release...
dotnet test "%SLN%" -c Release --no-build --no-restore
if errorlevel 1 goto :fail

echo.
echo [6/8] Publish SINGLE EXE win-x64...
if exist "%OUTROOT%" rmdir /s /q "%OUTROOT%"
echo Assemblage single EXE self-contained SANS compression...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true --no-restore --no-build -o "%OUT_SINGLE%" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=false -p:PublishReadyToRun=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -v:minimal
if errorlevel 1 goto :fail

if not exist "%OUT_SINGLE%\PinteMod.ControlCenter.exe" goto :singlefail
for /f %%N in ('dir /b /a-d "%OUT_SINGLE%" ^| find /c /v ""') do set "FILECOUNT=%%N"
if not "%FILECOUNT%"=="1" goto :singlefail

echo.
echo [7/8] Publish DOSSIER PORTABLE win-x64...
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true --no-restore --no-build -o "%OUT_FOLDER%" -p:PublishSingleFile=false -p:IncludeNativeLibrariesForSelfExtract=false -p:PublishReadyToRun=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -v:minimal
if errorlevel 1 goto :fail
if not exist "%OUT_FOLDER%\PinteMod.ControlCenter.exe" goto :folderfail
copy /y "%OUT_SINGLE%\PinteMod.ControlCenter.exe" "%OUT_FOLDER%\PinteMod.ControlCenter.Agent.exe" >nul
if not exist "%OUT_FOLDER%\PinteMod.ControlCenter.Agent.exe" goto :folderfail
for /f %%N in ('dir /b /a-d "%OUT_FOLDER%" ^| find /c /v ""') do set "FOLDERFILECOUNT=%%N"
if "%FOLDERFILECOUNT%"=="0" goto :folderfail
if "%FOLDERFILECOUNT%"=="1" goto :folderfail

echo.
echo [8/8] Auto-diagnostic, compression et audit...
"%OUT_FOLDER%\PinteMod.ControlCenter.exe" --self-test --self-test-report="%OUT_SELFTEST_FOLDER_TEMP%"
if errorlevel 1 goto :selftestfail
findstr /x /c:"RESULTAT=PASS" "%OUT_SELFTEST_FOLDER_TEMP%" >nul
if errorlevel 1 goto :selftestfail
"%OUT_SINGLE%\PinteMod.ControlCenter.exe" --self-test --self-test-report="%OUT_SELFTEST%"
if errorlevel 1 goto :selftestfail
findstr /x /c:"RESULTAT=PASS" "%OUT_SELFTEST%" >nul
if errorlevel 1 goto :selftestfail
del /q "%OUT_SELFTEST_FOLDER_TEMP%"
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "%PACKAGER%" "%OUT_SINGLE%" "%OUT_FOLDER%" "%OUTROOT%" "%ROOT%" "%OUT_SELFTEST%"
if errorlevel 1 goto :fail
if not exist "%OUT_SINGLE_ZIP%" goto :folderfail
if not exist "%OUT_FOLDER_ZIP%" goto :folderfail
if not exist "%OUT_HASHES%" goto :folderfail
if not exist "%OUT_SELFTEST%" goto :selftestfail

echo.
echo ============================================================
echo  BUILD OK - TESTS OK - DEUX FORMATS OK
echo ============================================================
echo.
echo EXE unique :
echo   %OUT_SINGLE%\PinteMod.ControlCenter.exe
echo.
echo Dossier portable :
echo   %OUT_FOLDER%
echo.
echo ZIP du dossier :
echo   %OUT_FOLDER_ZIP%
echo.
echo ZIP mono-EXE et empreintes :
echo   %OUT_SINGLE_ZIP%
echo   %OUT_HASHES%
echo   %OUT_SELFTEST%
echo.
echo Version stable v2.4.5 : parcours serveur vierge validé, Agent distant explicite et réparation RCON sûre.
echo Le meme EXE peut aussi fonctionner en Agent distant SMB sur le PC serveur.
echo.
popd
if "%NO_UI%"=="1" exit /b 0
explorer "%OUTROOT%"
pause
exit /b 0

:singlefail
echo.
echo [ERREUR] Le publish n'a pas produit exactement un seul EXE.
echo Contenu de %OUT_SINGLE% :
dir /b "%OUT_SINGLE%"
goto :fail_after_pop

:folderfail
echo.
echo [ERREUR] Le publish dossier n'a pas produit un dossier portable complet.
echo Contenu de %OUT_FOLDER% :
dir /b "%OUT_FOLDER%"
goto :fail_after_pop

:selftestfail
echo.
echo [ERREUR] L'auto-diagnostic local d'un des deux formats n'a pas produit RESULTAT=PASS.
if exist "%OUT_SELFTEST%" type "%OUT_SELFTEST%"
if exist "%OUT_SELFTEST_FOLDER_TEMP%" type "%OUT_SELFTEST_FOLDER_TEMP%"
goto :fail_after_pop

:nodotnet
echo [ERREUR] SDK .NET introuvable. Installez le SDK .NET 8 x64.
if "%NO_UI%"=="1" exit /b 1
pause
exit /b 1

:badpath
echo [ERREUR] Impossible d'ouvrir le dossier app.
if "%NO_UI%"=="1" exit /b 1
pause
exit /b 1

:fail
echo.
echo ECHEC DE BUILD - copiez les dernieres lignes dans ChatGPT.
popd
if "%NO_UI%"=="1" exit /b 1
pause
exit /b 1

:fail_after_pop
popd
echo.
echo ECHEC DE BUILD - copiez les dernieres lignes dans ChatGPT.
if "%NO_UI%"=="1" exit /b 1
pause
exit /b 1
