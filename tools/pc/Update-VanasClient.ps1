<#
.SYNOPSIS
    Updates the VANAS client on this PC when the server has a newer build. Meant to run at startup and at 04:00 (scheduled task, SYSTEM).

.DESCRIPTION
    Safe by design:
      - if the server is unreachable or has no build, nothing happens;
      - the package is downloaded, checked against its checksum and unpacked while the client keeps running;
      - the client is only stopped when nobody is playing: it writes <ProgramData>\VANAS\state.json (inSession,
        refreshed every 30 s); a missing, old or "inSession" state means "wait and retry at the next run";
      - on a kiosk PC the client is the Windows shell and always runs, so after the swap the PC is restarted
        (use -NoRestart to prevent it);
      - the new version is prepared next to the old one and swapped in; if the swap fails the old one is put back;
      - client.json (this PC's settings) is always kept.
    Everything is written to <ProgramData>\VANAS\logs\update.log.
#>
[CmdletBinding()]
param(
    [string]$InstallDir = 'C:\VANAS\Client',
    [string]$ServerUrl,
    [string]$LogDir = (Join-Path $env:ProgramData 'VANAS\logs'),
    [string]$StateFile = (Join-Path $env:ProgramData 'VANAS\state.json'),
    [switch]$Force,
    [switch]$NoRestart
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $LogDir | Out-Null
$logFile = Join-Path $LogDir 'update.log'

function Log([string]$message) {
    "{0:yyyy-MM-dd HH:mm:ss} {1}" -f (Get-Date), $message | Add-Content -Path $logFile -Encoding UTF8
    Write-Host $message
}

function Get-RunningClient {
    Get-Process -Name 'VANAS.Client' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($InstallDir, [StringComparison]::OrdinalIgnoreCase) }
}

# True only when the client says, recently, that nobody is playing.
function Test-ClientIdle {
    if (-not (Test-Path $StateFile)) { return $false }
    try {
        $state = Get-Content $StateFile -Raw | ConvertFrom-Json
        $age = (Get-Date).ToUniversalTime() - ([datetime]$state.at).ToUniversalTime()
        return (-not $state.inSession) -and ($age.TotalSeconds -lt 120)
    } catch { return $false }
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

    # Nobody playing? Decide before the long download so a busy PC does not download for nothing.
    if ((Get-RunningClient) -and -not (Test-ClientIdle)) {
        Log "Mise a jour $($remote.version) disponible mais un joueur est en session (ou l'etat du client est inconnu) : nouvel essai au prochain passage."
        return
    }

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

    # The download took time: check again that nobody sat down in the meantime, then stop the client.
    $client = Get-RunningClient
    $needsRestart = $false
    if ($client) {
        if (-not (Test-ClientIdle)) {
            Remove-Item $staging -Recurse -Force
            Log "Un joueur s'est connecte pendant le telechargement : mise a jour reportee."
            return
        }
        Log 'Arret du client (personne ne joue) ...'
        $client | Stop-Process -Force
        $client | Wait-Process -Timeout 20 -ErrorAction SilentlyContinue
        $needsRestart = $true
    }

    Move-Item $InstallDir $backup
    try {
        Move-Item $staging $InstallDir
    } catch {
        Move-Item $backup $InstallDir   # put the old version back
        throw
    }

    Remove-Item $backup -Recurse -Force -ErrorAction SilentlyContinue
    Log "Client mis a jour en version $($remote.version)."

    # On a kiosk PC the client is the Windows shell: a restart is the clean way to get it back.
    if ($needsRestart -and -not $NoRestart) {
        Log 'Redemarrage du PC pour lancer la nouvelle version.'
        Restart-Computer -Force
    }
} catch {
    Log "ERREUR : $($_.Exception.Message)"
    exit 1
}
