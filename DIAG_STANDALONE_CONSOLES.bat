@echo off
setlocal EnableExtensions
chcp 65001 >nul 2>nul
pushd "%~dp0" >nul 2>&1

set "REPORT=%~dp0PinteMod_Standalone_Console_Diagnostic.txt"

echo ============================================================
echo  PinteMod - DIAGNOSTIC CONSOLES STANDALONE (LECTURE SEULE)
echo ============================================================
echo.
echo Ce diagnostic ne modifie et n'arrete aucun processus.
echo Le rapport sera cree a cote de ce BAT.
echo.

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command ^
  "$out=New-Object System.Collections.Generic.List[string];" ^
  "$out.Add('PINTE MOD - STANDALONE CONSOLE DIAGNOSTIC');" ^
  "$out.Add(('Date: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')));" ^
  "$out.Add(('Machine: ' + $env:COMPUTERNAME));" ^
  "$out.Add('');" ^
  "$all=@(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue);" ^
  "$rx='PinteMod_(?:LiveConsole|Remote_RCON|Remote_Tools_Launcher|Server_Launcher|Launch_SingleInstance|MultiServer_Control|MultiServer_Worker)\.ps1';" ^
  "$procs=@($all | Where-Object { $_.Name -in @('powershell.exe','pwsh.exe','cmd.exe') -and ([string]$_.CommandLine) -match $rx });" ^
  "$out.Add('PROCESSUS PINTE MOD / CHAINE PARENTS');" ^
  "$out.Add('---------------------------------');" ^
  "if($procs.Count -eq 0){$out.Add('(aucun)')}else{foreach($p in ($procs|Sort-Object ProcessId)){ $cmd=[string]$p.CommandLine; $cmd=$cmd -replace '(?i)(rcon_password|password|secret|token)(\s+|=)[^\s\"'']+','$1$2[REDACTED]'; $out.Add(('PID={0} PPID={1} NAME={2}' -f $p.ProcessId,$p.ParentProcessId,$p.Name)); $out.Add(('  CMD: '+$cmd)); $parent=$all|Where-Object ProcessId -eq $p.ParentProcessId|Select-Object -First 1; if($parent){$pcmd=[string]$parent.CommandLine; $pcmd=$pcmd -replace '(?i)(rcon_password|password|secret|token)(\s+|=)[^\s\"'']+','$1$2[REDACTED]'; $out.Add(('  PARENT PID={0} NAME={1}' -f $parent.ProcessId,$parent.Name)); $out.Add(('  PARENT CMD: '+$pcmd))}; $out.Add('') }};" ^
  "$out.Add('TACHES PLANIFIEES PINTE MOD');" ^
  "$out.Add('--------------------------');" ^
  "$tasks=@(); try{$tasks=Get-ScheduledTask -ErrorAction Stop | Where-Object { (($_.Actions | ForEach-Object { [string]$_.Execute + ' ' + [string]$_.Arguments }) -join ' ') -match 'PinteMod_' }}catch{};" ^
  "if(@($tasks).Count -eq 0){$out.Add('(aucune)')}else{$tasks | ForEach-Object {$acts=(($_.Actions | ForEach-Object { [string]$_.Execute + ' ' + [string]$_.Arguments }) -join ' ; '); $out.Add(('{0}{1} -> {2}' -f $_.TaskPath,$_.TaskName,$acts))}};" ^
  "$out.Add('');" ^
  "$out.Add('DEMARRAGE UTILISATEUR (HKCU RUN)');" ^
  "$out.Add('-------------------------------');" ^
  "$run=@(); try{$p=Get-ItemProperty 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction Stop; $run=$p.PSObject.Properties | Where-Object { $_.Name -notmatch '^PS' -and ([string]$_.Value) -match 'PinteMod' }}catch{};" ^
  "if(@($run).Count -eq 0){$out.Add('(aucun)')}else{$run | ForEach-Object {$out.Add(($_.Name + ' -> ' + [string]$_.Value))}};" ^
  "$out.Add('');" ^
  "$out.Add('DOSSIERS STARTUP UTILISATEUR / COMMUN');" ^
  "$out.Add('------------------------------------');" ^
  "$startup=@([Environment]::GetFolderPath('Startup'),[Environment]::GetFolderPath('CommonStartup')); foreach($dir in $startup){$out.Add(('['+$dir+']')); if(Test-Path -LiteralPath $dir){$items=@(Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -match 'PinteMod' }); if($items.Count -eq 0){$out.Add('  (aucun)')}else{$items|ForEach-Object{$out.Add(('  '+$_.FullName))}}}else{$out.Add('  (absent)')}};" ^
  "[IO.File]::WriteAllLines($env:REPORT,$out,[Text.UTF8Encoding]::new($false)); $out | ForEach-Object {Write-Host $_}"

echo.
echo Rapport : %REPORT%
echo.
pause
popd >nul 2>&1
exit /b 0
