<#
.SYNOPSIS
    Annule Install-KioskUser.ps1 : retire la connexion automatique et supprime le compte kiosque.
    À exécuter en administrateur, depuis un AUTRE compte que le compte kiosque. Supporte -WhatIf.

.PARAMETER RemoveProfile
    Supprime aussi le dossier de profil (C:\Users\<compte>) et ses réglages.
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$UserName = 'vanas-client',
    [switch]$RemoveProfile
)

$ErrorActionPreference = 'Stop'

if ($UserName -ieq $env:USERNAME) { throw "Connectez-vous avec un autre compte administrateur pour supprimer '$UserName'." }

$winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
if ((Get-ItemProperty $winlogon -ErrorAction SilentlyContinue).DefaultUserName -ieq $UserName) {
    if ($PSCmdlet.ShouldProcess('Winlogon', 'Désactiver la connexion automatique')) {
        Set-ItemProperty $winlogon -Name AutoAdminLogon -Value '0'
        Remove-ItemProperty $winlogon -Name DefaultPassword -ErrorAction SilentlyContinue
        Remove-ItemProperty $winlogon -Name DefaultUserName -ErrorAction SilentlyContinue
    }
}

$user = Get-LocalUser -Name $UserName -ErrorAction SilentlyContinue
if ($user) {
    $sid = $user.SID.Value
    if ($PSCmdlet.ShouldProcess($UserName, 'Supprimer le compte local')) {
        Remove-LocalUser -Name $UserName
        if ($RemoveProfile) {
            Get-CimInstance Win32_UserProfile -Filter "SID='$sid'" -ErrorAction SilentlyContinue | Remove-CimInstance
        }
    }
} else {
    Write-Warning "Le compte '$UserName' n'existe pas."
}

Write-Host "Terminé. Le dossier du client et client.json ne sont pas supprimés." -ForegroundColor Green
