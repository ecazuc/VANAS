<#
.SYNOPSIS
    Removes the VANAS client from this PC (files and the automatic update task).
    The kiosk account is removed separately with kiosk\Uninstall-KioskUser.ps1.

.EXAMPLE
    .\Uninstall-VanasClient.ps1
    .\Uninstall-VanasClient.ps1 -KeepLogs
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$InstallDir = 'C:\VANAS\Client',
    [switch]$KeepLogs
)

$ErrorActionPreference = 'Stop'

$task = Get-ScheduledTask -TaskName 'VANAS Client Update' -ErrorAction SilentlyContinue
if ($task -and $PSCmdlet.ShouldProcess('VANAS Client Update', 'Supprimer la tache planifiee')) {
    Unregister-ScheduledTask -TaskName 'VANAS Client Update' -Confirm:$false
    Write-Host 'Tache planifiee supprimee.'
}

$running = Get-Process -Name 'VANAS.Client' -ErrorAction SilentlyContinue
if ($running) { throw 'Le client est en cours d''utilisation : fermez-le (Ctrl+Maj+F12 puis le code PIN) avant de le desinstaller.' }

$toolsDir = Join-Path (Split-Path $InstallDir -Parent) 'tools'
if ((Test-Path $toolsDir) -and $PSCmdlet.ShouldProcess($toolsDir, 'Supprimer les scripts')) { Remove-Item $toolsDir -Recurse -Force }

if ((Test-Path $InstallDir) -and $PSCmdlet.ShouldProcess($InstallDir, 'Supprimer le client')) {
    Remove-Item $InstallDir -Recurse -Force
    Write-Host "Dossier $InstallDir supprime."
}

$dataDir = Join-Path $env:ProgramData 'VANAS'
if (-not $KeepLogs -and (Test-Path $dataDir) -and $PSCmdlet.ShouldProcess($dataDir, 'Supprimer les journaux')) {
    Remove-Item $dataDir -Recurse -Force
    Write-Host 'Journaux supprimes.'
}
