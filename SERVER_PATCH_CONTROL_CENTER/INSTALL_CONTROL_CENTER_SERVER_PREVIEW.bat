@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ============================================================
rem  UNC-safe installer. ASCII only, CRLF written at packaging.
rem  GSC payload remains Control Center Contracts Preview v0.3.1.
rem ============================================================

set "SELF_PUSHED=0"
set "TARGET_PUSHED=0"

rem pushd maps an UNC launch folder to a temporary drive letter.
pushd "%~dp0" >nul 2>&1
if errorlevel 1 goto :selfpathfail
set "SELF_PUSHED=1"
set "HERE=%CD%"
set "SOURCE=%HERE%\boiii\custom_scripts\ezz_admin_control_center_contracts.gsc"

echo ============================================================
echo  PinteMod - CONTROL CENTER SERVER PREVIEW
echo  Installer UNC FIX 1 - GSC v0.3.1
echo ============================================================
echo.
echo IMPORTANT : arrete le serveur BOIII cible avant de continuer.
echo Ce BAT n'a besoin d'aucun secret RCON.
echo Les chemins locaux et UNC ^(\\serveur\partage\...^) sont supportes.
echo.
set /p "SERVERROOT_INPUT=Chemin racine du serveur cible (dossier contenant boiii) : "

if not defined SERVERROOT_INPUT goto :badroot
set "SERVERROOT_INPUT=%SERVERROOT_INPUT:"=%"
if "%SERVERROOT_INPUT:~-1%"=="\" set "SERVERROOT_INPUT=%SERVERROOT_INPUT:~0,-1%"

rem Map the selected server root too. This avoids CMD UNC current-directory issues.
pushd "%SERVERROOT_INPUT%" >nul 2>&1
if errorlevel 1 goto :badroot
set "TARGET_PUSHED=1"
set "SERVERROOT=%CD%"

if not exist "%SERVERROOT%\boiii\custom_scripts" goto :badroot
if not exist "%SERVERROOT%\boiii\custom_scripts\ezz_admin_storage.gsc" (
    echo.
    echo [ERREUR] ezz_admin_storage.gsc introuvable.
    echo Ce patch exige une base PinteMod v2.1.1 coherente.
    goto :fail
)
if not exist "%SOURCE%" (
    echo [ERREUR] GSC source introuvable dans ce paquet.
    goto :fail
)

echo.
echo Cartes autorisees pour CHANGE MAP
echo ----------------------------------
echo Une carte doit etre a la fois installee sur CE serveur et volontairement autorisee.
echo "supported" ne signifie jamais "installed".
echo.
choice /C ON /N /M "Les 14 cartes officielles sont-elles toutes installees ET autorisees ? [O/N] "
if errorlevel 2 goto :individual
if errorlevel 1 goto :allmaps

:allmaps
set "COUNT=14"
set "MAP_1=zm_zod"
set "MAP_2=zm_castle"
set "MAP_3=zm_island"
set "MAP_4=zm_stalingrad"
set "MAP_5=zm_genesis"
set "MAP_6=zm_cosmodrome"
set "MAP_7=zm_theater"
set "MAP_8=zm_moon"
set "MAP_9=zm_prototype"
set "MAP_10=zm_tomb"
set "MAP_11=zm_temple"
set "MAP_12=zm_sumpf"
set "MAP_13=zm_factory"
set "MAP_14=zm_asylum"
goto :write

:individual
set "COUNT=0"
call :askmap "Shadows of Evil" "zm_zod"
call :askmap "Der Eisendrache" "zm_castle"
call :askmap "Zetsubou No Shima" "zm_island"
call :askmap "Gorod Krovi" "zm_stalingrad"
call :askmap "Revelations" "zm_genesis"
call :askmap "Ascension" "zm_cosmodrome"
call :askmap "Kino der Toten" "zm_theater"
call :askmap "Moon" "zm_moon"
call :askmap "Nacht der Untoten" "zm_prototype"
call :askmap "Origins" "zm_tomb"
call :askmap "Shangri-La" "zm_temple"
call :askmap "Shi No Numa" "zm_sumpf"
call :askmap "The Giant" "zm_factory"
call :askmap "Verruckt" "zm_asylum"
goto :write

:askmap
choice /C ON /N /M "%~1 (%~2) installee ET autorisee ? [O/N] "
if errorlevel 2 exit /b 0
if errorlevel 1 (
    set /a COUNT+=1
    set "MAP_!COUNT!=%~2"
)
exit /b 0

:write
set "TARGETGSC=%SERVERROOT%\boiii\custom_scripts\ezz_admin_control_center_contracts.gsc"
set "CONFIGDIR=%SERVERROOT%\boiii\scriptdata\pintemod\config"
set "ALLOWLIST=%CONFIGDIR%\control_center_map_allowlist.json"

if not exist "%CONFIGDIR%" md "%CONFIGDIR%" >nul 2>&1
if errorlevel 1 (
    echo [ERREUR] Impossible de creer : %CONFIGDIR%
    goto :fail
)

if exist "%TARGETGSC%" (
    set "BACKUP=%TARGETGSC%.before_cc_v030_%RANDOM%.bak"
    copy /Y "%TARGETGSC%" "!BACKUP!" >nul
    if errorlevel 1 (
        echo [ERREUR] Impossible de sauvegarder l'ancien GSC.
        goto :fail
    )
    echo [INFO] Ancien GSC sauvegarde : !BACKUP!
)

copy /Y "%SOURCE%" "%TARGETGSC%" >nul
if errorlevel 1 (
    echo [ERREUR] Echec copie du GSC vers le serveur.
    goto :fail
)

>"%ALLOWLIST%" echo {
>>"%ALLOWLIST%" echo   "schema_version": 1,
>>"%ALLOWLIST%" echo   "authority": "operator_declared",
if !COUNT! GTR 0 (
    >>"%ALLOWLIST%" echo   "count": !COUNT!,
    for /L %%I in (1,1,!COUNT!) do (
        if %%I LSS !COUNT! (
            >>"%ALLOWLIST%" echo   "map_%%I": "!MAP_%%I!",
        ) else (
            >>"%ALLOWLIST%" echo   "map_%%I": "!MAP_%%I!"
        )
    )
) else (
    >>"%ALLOWLIST%" echo   "count": 0
)
>>"%ALLOWLIST%" echo }

if not exist "%ALLOWLIST%" (
    echo [ERREUR] Allowlist non creee.
    goto :fail
)

echo.
echo ============================================================
echo  INSTALLATION TERMINEE
echo ============================================================
echo GSC installe :
echo   boiii\custom_scripts\ezz_admin_control_center_contracts.gsc
echo Allowlist locale :
echo   boiii\scriptdata\pintemod\config\control_center_map_allowlist.json
echo Cartes autorisees : !COUNT!
echo.
echo Redemarre maintenant le serveur cible.
echo Dans la console BOIII, cherche exactement :
echo   [PinteMod] Control Center Contracts Preview v0.3.1 loaded
echo.
echo Le module creera ensuite automatiquement :
echo   pintemod\diagnostics\control_center_capabilities.json
echo   pintemod\runtime\server_identity.json
echo   pintemod\remote\action_feedback.latest.json apres une mutation confirmee
echo.
goto :success

:selfpathfail
echo.
echo [ERREUR] Impossible d'acceder au dossier de l'installateur.
echo Copie le paquet localement OU verifie l'acces au partage UNC.
goto :fail_nocleanup

:badroot
echo.
echo [ERREUR] Racine serveur invalide ou inaccessible.
echo Le dossier choisi doit contenir boiii\custom_scripts.
goto :fail

:fail
echo.
echo ECHEC - aucun secret n'a ete demande ni modifie.
echo Copie simplement le message affiche dans ChatGPT.
echo.
call :cleanup
pause
exit /b 1

:fail_nocleanup
echo.
pause
exit /b 1

:success
call :cleanup
pause
exit /b 0

:cleanup
if "%TARGET_PUSHED%"=="1" popd >nul 2>&1
if "%SELF_PUSHED%"=="1" popd >nul 2>&1
exit /b 0
