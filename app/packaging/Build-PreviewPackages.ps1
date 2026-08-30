[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $SingleDirectory,

    [Parameter(Mandatory = $true)]
    [string] $FolderDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputRoot,

    [Parameter(Mandatory = $true)]
    [string] $RepositoryRoot,

    [Parameter(Mandatory = $true)]
    [string] $SelfTestReport
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256Hex {
    param([Parameter(Mandatory = $true)][string] $Path)

    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha256 = [Security.Cryptography.SHA256]::Create()
        try {
            return [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '')
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$output = [IO.Path]::GetFullPath($OutputRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$single = [IO.Path]::GetFullPath($SingleDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$folder = [IO.Path]::GetFullPath($FolderDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
$outputPrefix = $output + [IO.Path]::DirectorySeparatorChar

foreach ($candidate in @($single, $folder)) {
    if (-not $candidate.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Dossier de publication hors de la racine de sortie : $candidate"
    }
}

$singleFiles = @(Get-ChildItem -LiteralPath $single -File)
if ($singleFiles.Count -ne 1 -or $singleFiles[0].Name -ne 'PinteMod.ControlCenter.exe') {
    throw 'Le format mono-EXE doit contenir exactement PinteMod.ControlCenter.exe.'
}

$folderFiles = @(Get-ChildItem -LiteralPath $folder -Recurse -File)
if ($folderFiles.Count -le 1 -or -not (Test-Path -LiteralPath (Join-Path $folder 'PinteMod.ControlCenter.exe'))) {
    throw 'Le format dossier autonome est incomplet.'
}

$folderAgent = Join-Path $folder 'PinteMod.ControlCenter.Agent.exe'
$singleAgent = Join-Path $single 'PinteMod.ControlCenter.exe'
if (-not (Test-Path -LiteralPath $folderAgent) -or
    -not [string]::Equals((Get-Sha256Hex -Path $folderAgent), (Get-Sha256Hex -Path $singleAgent), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Le dossier portable doit inclure le package Agent autonome identique au mono-EXE.'
}

$selfTest = [IO.Path]::GetFullPath($SelfTestReport)
if (-not $selfTest.StartsWith($outputPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not (Test-Path -LiteralPath $selfTest -PathType Leaf)) {
    throw 'Le rapport self-test doit être un fichier situé dans la racine de sortie.'
}

$selfTestText = [IO.File]::ReadAllText($selfTest)
if ($selfTestText -notmatch '(?m)^RESULTAT=PASS\r?$' -or
    $selfTestText -match '(?i)[A-Z]:\\(?:Users|Dev|agent)\\') {
    throw 'Le rapport self-test est en échec ou contient un chemin privé.'
}

$singleZip = Join-Path $output 'PinteMod.ControlCenter-single-exe-win-x64.zip'
$folderZip = Join-Path $output 'PinteMod.ControlCenter-folder-win-x64.zip'
foreach ($archive in @($singleZip, $folderZip)) {
    if (Test-Path -LiteralPath $archive) {
        Remove-Item -LiteralPath $archive -Force
    }
}

Compress-Archive -Path (Join-Path $single '*') -DestinationPath $singleZip -CompressionLevel Optimal
Compress-Archive -Path (Join-Path $folder '*') -DestinationPath $folderZip -CompressionLevel Optimal

$audit = Join-Path $PSScriptRoot 'Test-PublishedPackage.ps1'
& $audit -PackagePath $singleZip -ForbiddenText $RepositoryRoot
& $audit -PackagePath $folderZip -ForbiddenText $RepositoryRoot

$exe = Join-Path $single 'PinteMod.ControlCenter.exe'
$hashLines = @(
    "$(Get-Sha256Hex -Path $exe) *single-exe/PinteMod.ControlCenter.exe",
    "$(Get-Sha256Hex -Path $singleZip) *PinteMod.ControlCenter-single-exe-win-x64.zip",
    "$(Get-Sha256Hex -Path $folderZip) *PinteMod.ControlCenter-folder-win-x64.zip"
)
$hashLines += "$(Get-Sha256Hex -Path $selfTest) *SELF-TEST.txt"
[IO.File]::WriteAllText(
    (Join-Path $output 'SHA256SUMS.txt'),
    ($hashLines -join "`r`n") + "`r`n",
    [Text.UTF8Encoding]::new($false))

Write-Output "PACKAGES_OK single=$singleZip folder=$folderZip"
