[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $PackagePath,

    [string[]] $ForbiddenText = @()
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression.FileSystem

$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
$findings = [System.Collections.Generic.List[string]]::new()

try {
    $entryNames = @($archive.Entries | ForEach-Object FullName)
    foreach ($name in $entryNames) {
        if ($name -match '(^|[/\\])\.\.([/\\]|$)' -or
            $name -match '^[/\\]' -or
            $name -match '^[A-Za-z]:') {
            $findings.Add("Chemin ZIP dangereux : $name")
        }

        if ($name -match '(?i)(\.pdb$|rcon\.secret|operator-settings\.json$|server-sandbox|current_session\.json$|\.log$|\.tmp$|\.bak$)') {
            $findings.Add("Fichier interdit : $name")
        }
    }

    $firstPartyNames = @(
        'PinteMod.ControlCenter.exe',
        'PinteMod.ControlCenter.dll',
        'PinteMod.ControlCenter.Core.dll',
        'PinteMod.ControlCenter.Infrastructure.dll'
    )
    foreach ($entry in $archive.Entries | Where-Object { $firstPartyNames -contains $_.FullName }) {
        $memory = [System.IO.MemoryStream]::new()
        try {
            $stream = $entry.Open()
            try {
                $stream.CopyTo($memory)
            }
            finally {
                $stream.Dispose()
            }

            $bytes = $memory.ToArray()
            $searchableText = [System.Text.Encoding]::GetEncoding(28591).GetString($bytes) + "`n" +
                [System.Text.Encoding]::Unicode.GetString($bytes)

            foreach ($forbidden in $ForbiddenText | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) {
                if ($searchableText.IndexOf($forbidden, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $findings.Add("Texte interdit présent dans $($entry.FullName).")
                }
            }

            if ([regex]::IsMatch($searchableText, '(?i)(?<![A-Za-z0-9])[A-Z]:\\(?:Users|Dev|agent)\\')) {
                $findings.Add("Chemin absolu de compilation présent dans $($entry.FullName).")
            }
        }
        finally {
            $memory.Dispose()
        }
    }
}
finally {
    $archive.Dispose()
}

if ($findings.Count -gt 0) {
    $findings | ForEach-Object { Write-Error $_ }
    throw "Audit du paquet en échec : $($findings.Count) problème(s)."
}

$hash = (Get-FileHash -LiteralPath $resolvedPackage -Algorithm SHA256).Hash
Write-Output "PACKAGE_AUDIT_PASS entries=$($entryNames.Count) sha256=$hash"
