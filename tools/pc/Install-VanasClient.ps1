<#
.SYNOPSIS
    Installs the VANAS client on a cafe PC: files, settings, logs, automatic updates and (optionally) kiosk mode.

.DESCRIPTION
    Run it in PowerShell AS ADMINISTRATOR on the PC. The package is downloaded from the cafe server
    (published with tools\Publish-Client.ps1) or taken from -SourceZip.

    What it does:
      1. downloads (or reads) the client package and checks its checksum;
      2. extracts it in InstallDir (C:\VANAS\Client by default);
      3. writes client.json (server address, station name) unless one already exists (use -Force to rewrite it);
      4. creates the log folder (C:\ProgramData\VANAS\logs);
      5. registers the scheduled task "VANAS Client Update" (runs at startup and every day at 04:00);
      6. with -EnableKiosk: sets the admin PIN and creates the locked-down Windows account (kiosk\ scripts).

    Test the kiosk part in a virtual machine first (see kiosk\README.md).

.EXAMPLE
    .\Install-VanasClient.ps1 -ServerUrl http://192.168.1.10:5049 -StationName PC-01
    .\Install-VanasClient.ps1 -ServerUrl http://192.168.1.10:5049 -StationName PC-01 -EnableKiosk -Pin 482913 -EnableAutoLogon
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ServerUrl,
    [Parameter(Mandatory)][string]$StationName,
    [string]$InstallDir = 'C:\VANAS\Client',
    [string]$SourceZip,
    [string]$Pin,
    [switch]$EnableKiosk,
    [switch]$EnableAutoLogon,
    [string]$KioskScriptsDir,
    [switch]$SkipScheduledTask,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $KioskScriptsDir) { $KioskScriptsDir = Join-Path $here '..\..\kiosk' }
$ServerUrl = $ServerUrl.TrimEnd('/')

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and (-not $SkipScheduledTask -or $EnableKiosk)) {
    throw 'Lancez ce script dans une fenetre PowerShell "en tant qu''administrateur".'
}
if ($StationName -notmatch '^[A-Za-z0-9_.\- ]{1,64}$') { throw 'Nom de poste invalide (lettres, chiffres, tiret, 64 caracteres max).' }
if ($EnableKiosk -and -not $Pin) { throw 'Avec -EnableKiosk, indiquez un code PIN administrateur (-Pin) : sans lui, aucune sortie du kiosque ne serait possible.' }

# 1. package
$zip = $SourceZip
$expectedHash = $null
if (-not $zip) {
    Write-Host "Telechargement du client depuis $ServerUrl ..."
    $remote = Invoke-RestMethod "$ServerUrl/api/client/version" -TimeoutSec 20
    $zip = Join-Path $env:TEMP ('vanas-client-' + [guid]::NewGuid().ToString('N') + '.zip')
    Invoke-WebRequest "$ServerUrl/api/client/package" -OutFile $zip -TimeoutSec 600 -UseBasicParsing
    $expectedHash = $remote.sha256
}
if (-not (Test-Path $zip)) { throw "Paquet introuvable : $zip" }
if ($expectedHash) {
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($hash -ne $expectedHash) { throw "Somme de controle incorrecte : le paquet est corrompu ($hash)." }
}

# 2. files (an existing client.json is kept)
$configPath = Join-Path $InstallDir 'client.json'
$existingConfig = if (Test-Path $configPath) { Get-Content $configPath -Raw } else { $null }
New-Item -ItemType Directory -Force $InstallDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    foreach ($entry in $archive.Entries) {
        $target = [IO.Path]::GetFullPath((Join-Path $InstallDir $entry.FullName))
        if (-not $target.StartsWith([IO.Path]::GetFullPath($InstallDir), [StringComparison]::OrdinalIgnoreCase)) { throw "Entree de paquet suspecte : $($entry.FullName)" }
        if ($entry.FullName.EndsWith('/')) { New-Item -ItemType Directory -Force $target | Out-Null; continue }
        New-Item -ItemType Directory -Force (Split-Path $target) | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $target, $true)
    }
} finally { $archive.Dispose() }
if (-not $SourceZip) { Remove-Item $zip -Force -ErrorAction SilentlyContinue }

# 3. settings
if ($existingConfig -and -not $Force) {
    Set-Content $configPath $existingConfig -Encoding UTF8
    Write-Host 'client.json existant conserve (utilisez -Force pour le reecrire).'
} else {
    $config = [ordered]@{
        ServerUrl             = $ServerUrl
        StationName           = $StationName
        KioskMode             = $false
        KioskAccount          = 'vanas-client'
        CloseProgramsOnLogout = $true
        WipeLauncherLogins    = $true
        MinFreeDiskGb         = 20
        AdminPinHash          = ''
    }
    $config | ConvertTo-Json | Set-Content $configPath -Encoding UTF8
    Write-Host "client.json ecrit : serveur $ServerUrl, poste $StationName."
}

# 4. logs (the kiosk account must be able to write them)
$dataDir = Join-Path $env:ProgramData 'VANAS'
New-Item -ItemType Directory -Force (Join-Path $dataDir 'logs') | Out-Null
if ($isAdmin) { icacls $dataDir /grant 'Users:(OI)(CI)M' /T /Q | Out-Null }

# 5. automatic updates
# Next to the client folder, not inside it: an update replaces the whole client folder.
$toolsDir = Join-Path (Split-Path $InstallDir -Parent) 'tools'
New-Item -ItemType Directory -Force $toolsDir | Out-Null
Copy-Item (Join-Path $here 'Update-VanasClient.ps1') $toolsDir -Force
Copy-Item (Join-Path $here 'Uninstall-VanasClient.ps1') $toolsDir -Force
if (-not $SkipScheduledTask) {
    $script = Join-Path $toolsDir 'Update-VanasClient.ps1'
    $action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$script`" -InstallDir `"$InstallDir`""
    # 2 minutes after startup: on a kiosk PC the client starts by itself at logon, and the updater must find it up and idle.
    $startup = New-ScheduledTaskTrigger -AtStartup
    $startup.Delay = 'PT2M'
    $triggers = @($startup, (New-ScheduledTaskTrigger -Daily -At 4am))
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName 'VANAS Client Update' -Action $action -Trigger $triggers -Principal $principal -Force | Out-Null
    Write-Host 'Tache planifiee "VANAS Client Update" creee (demarrage + 04h00).'
} else {
    Write-Host 'Tache planifiee ignoree (-SkipScheduledTask).'
}

# 6. kiosk
if ($EnableKiosk) {
    $kiosk = (Resolve-Path $KioskScriptsDir -ErrorAction Stop).Path
    $exe = Join-Path $InstallDir 'VANAS.Client.exe'
    & (Join-Path $kiosk 'New-AdminPin.ps1') -Pin $Pin -ClientConfig $configPath
    $kioskArgs = @{ ClientExe = $exe }
    if ($EnableAutoLogon) { $kioskArgs.EnableAutoLogon = $true }
    & (Join-Path $kiosk 'Install-KioskUser.ps1') @kioskArgs
    Write-Host 'Mode kiosque installe : redemarrez le PC.'
}

$version = if (Test-Path (Join-Path $InstallDir 'version.txt')) { (Get-Content (Join-Path $InstallDir 'version.txt') -Raw).Trim() } else { '?' }
Write-Host "Termine : client $version installe dans $InstallDir."
