<#
.SYNOPSIS
    Builds the PC client and publishes it on the server, so the PCs of the cafe can install it and update themselves.

.DESCRIPTION
    Produces three files in the server's update folder (default VANAS.Server\ClientUpdates, setting Client:UpdateFolder):
      client.zip     the self-contained client (without client.json: each PC keeps its own settings)
      client.sha256  checksum checked by the PCs after download
      version.txt    version number, written last so a PC never sees a half-published build

.PARAMETER Version
    Version number to publish (default: the one in VANAS.Client.csproj). A PC only updates to a higher number.

.EXAMPLE
    .\tools\Publish-Client.ps1
    .\tools\Publish-Client.ps1 -Version 1.2.0
#>
[CmdletBinding()]
param(
    [string]$ServerFolder,
    [string]$Version,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = (Resolve-Path (Join-Path $here '..')).Path
if (-not $ServerFolder) { $ServerFolder = Join-Path $repo 'VANAS.Server\ClientUpdates' }
$csproj = Join-Path $repo 'VANAS.Client\VANAS.Client.csproj'

if (-not $Version) {
    $Version = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') { throw "Version invalide : '$Version' (attendu : 1.2.3)" }

$work = Join-Path $env:TEMP ('vanas-publish-' + [guid]::NewGuid().ToString('N'))
Write-Host "Compilation du client $Version ..."
dotnet publish $csproj -c $Configuration -r win-x64 --self-contained true -p:Version=$Version -o $work | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'La compilation a echoue.' }

# Each PC has its own client.json; the package must never overwrite it.
Remove-Item (Join-Path $work 'client.json') -ErrorAction SilentlyContinue
[IO.File]::WriteAllText((Join-Path $work 'version.txt'), $Version)

Write-Host 'Creation du paquet ...'
New-Item -ItemType Directory -Force $ServerFolder | Out-Null
$ServerFolder = (Resolve-Path $ServerFolder).Path
$zipTemp = Join-Path $ServerFolder 'client.zip.part'
Remove-Item $zipTemp -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($work, $zipTemp, [IO.Compression.CompressionLevel]::Optimal, $false)

$hash = (Get-FileHash $zipTemp -Algorithm SHA256).Hash.ToLowerInvariant()
Move-Item $zipTemp (Join-Path $ServerFolder 'client.zip') -Force
[IO.File]::WriteAllText((Join-Path $ServerFolder 'client.sha256'), $hash)
[IO.File]::WriteAllText((Join-Path $ServerFolder 'version.txt'), $Version)   # last: this is what makes the build visible

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
$size = [math]::Round((Get-Item (Join-Path $ServerFolder 'client.zip')).Length / 1MB, 1)
Write-Host "Publie : version $Version, $size Mo, sha256 $hash"
Write-Host "Dossier : $ServerFolder"
