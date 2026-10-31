<#
.SYNOPSIS
    Updates the VANAS client on this PC when the server has a newer build. Meant to run at startup (scheduled task, SYSTEM).

.DESCRIPTION
    Safe by design:
      - if the server is unreachable or has no build, nothing happens;
      - if a customer is using the client, nothing happens (it retries at the next run);
      - the package is checked against its checksum before use;
      - the new version is prepared next to the old one and swapped in; if the swap fails the old one is put back;
      - client.json (this PC's settings) is always kept.
    Everything is written to <ProgramData>\VANAS\logs\update.log.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\VANAS\Client',
    [string]$ServerUrl,
    [string]$LogDir = (Join-Path $env:ProgramData 'VANAS\logs'),
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $LogDir | Out-Null
$logFile = Join-Path $LogDir 'update.log'

function Log([string]$message) {
    "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message | Add-Content -Path $logFile -Encoding UTF8
    Write-Host $message
}

try {
    $configPath = Join-Path $InstallDir 'client.json'
    if (-not $ServerUrl) {
        if (-not (Test-Path $configPath)) { Log "Pas de client.json dans $InstallDir : rien a faire."; return }
        $ServerUrl = (Get-Content $configPath -Raw | ConvertFrom-Json).ServerUrl
    }
    $ServerUrl = $ServerUrl.TrimEnd('/')

    $versionFile = Join-Path $InstallDir 'version.txt'
    $installed = if (Test-Path $versionFile) { (Get-Content $versionFile -Raw).Trim() } else { '0.0.0' }

    try {
        $remote = Invoke-RestMethod "$ServerUrl/api/client/version" -TimeoutSec 15
    } catch {
        Log "Serveur injoignable ou sans mise a jour ($($_.Exception.Message)) : on garde la version $installed."
        return
    }

    if (-not $Force -and ([version]$remote.version) -le ([version]$installed)) {
        Log "A jour (version $installed)."
        return
    }

    $running = Get-Process -Name 'VANAS.Client' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($InstallDir, [StringComparison]::OrdinalIgnoreCase) }
    if ($running) { Log "Mise a jour $($remote.version) disponible mais le client est utilise : nouvel essai au prochain passage."; return }

    Log "Mise a jour de $installed vers $($remote.version) ..."
    $zip = Join-Path $env:TEMP ('vanas-client-' + [guid]::NewGuid().ToString('N') + '.zip')
    Invoke-WebRequest "$ServerUrl/api/client/package" -OutFile $zip -TimeoutSec 600 -UseBasicParsing
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $remote.sha256) { Remove-Item $zip -Force; throw "Somme de controle incorrecte (attendu $($remote.sha256), obtenu $hash)." }

    $staging = "$InstallDir.new"
    $backup = "$InstallDir.old"
    if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
    if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $staging)
    Remove-Item $zip -Force

    if (Test-Path $configPath) { Copy-Item $configPath (Join-Path $staging 'client.json') -Force }

    Move-Item $InstallDir $backup
    try {
        Move-Item $staging $InstallDir
    } catch {
        Move-Item $backup $InstallDir   # put the old version back
        throw
    }

    Remove-Item $backup -Recurse -Force -ErrorAction SilentlyContinue
    Log "Client mis a jour en version $($remote.version)."
} catch {
    Log "ERREUR : $($_.Exception.Message)"
    exit 1
}
