<#
.SYNOPSIS
    Configure un PC du café en mode kiosque VANAS : compte Windows dédié, VANAS comme shell,
    restrictions (gestionnaire de tâches, cmd, regedit, Win+touches...) et connexion automatique optionnelle.

.DESCRIPTION
    Tout est appliqué au compte dédié uniquement. Les comptes administrateurs ne sont pas touchés.
    À exécuter dans une console PowerShell lancée en administrateur.
    Utilisez -WhatIf pour voir ce qui serait fait sans rien modifier.
    Pour annuler : Uninstall-KioskUser.ps1.

    NON TESTÉ sur une vraie machine : à valider d'abord dans une machine virtuelle (voir kiosk/README.md).

.PARAMETER ClientExe
    Chemin complet de VANAS.Client.exe sur ce PC (ex. C:\VANAS\Client\VANAS.Client.exe).

.PARAMETER UserName
    Nom du compte Windows dédié (doit correspondre à "KioskAccount" dans client.json).

.PARAMETER Password
    Mot de passe du compte dédié. Généré aléatoirement et affiché une fois si omis.

.PARAMETER EnableAutoLogon
    Ouvre automatiquement la session du compte dédié au démarrage du PC.
#>
#Requires -RunAsAdministrator
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$ClientExe,
    [string]$UserName = 'vanas-client',
    [string]$Password,
    [switch]$EnableAutoLogon
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ClientExe)) { throw "VANAS.Client.exe introuvable : $ClientExe" }
$ClientExe = (Resolve-Path -LiteralPath $ClientExe).Path

if ($UserName -ieq $env:USERNAME) { throw "Refus : '$UserName' est le compte actuellement utilisé. Choisissez un compte dédié distinct." }

if (-not $Password) {
    $bytes = New-Object byte[] 18
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $Password = 'Vn!' + [Convert]::ToBase64String($bytes).Replace('/', 'x').Replace('+', 'y')
    $generated = $true
}
$secure = ConvertTo-SecureString $Password -AsPlainText -Force

# 1. Compte local dédié (utilisateur standard, sans droits admin)
if (-not (Get-LocalUser -Name $UserName -ErrorAction SilentlyContinue)) {
    if ($PSCmdlet.ShouldProcess($UserName, 'Créer le compte local standard')) {
        New-LocalUser -Name $UserName -Password $secure -FullName 'Poste client VANAS' `
            -Description 'Compte kiosque VANAS' -PasswordNeverExpires -UserMayNotChangePassword | Out-Null
        Add-LocalGroupMember -Group (Get-LocalGroup -SID 'S-1-5-32-545') -Member $UserName
    }
} else {
    Write-Warning "Le compte '$UserName' existe déjà : mot de passe inchangé, réglages réappliqués."
    $generated = $false
}

# 2. Création du profil (nécessaire pour éditer sa ruche de registre)
$cred = New-Object System.Management.Automation.PSCredential($UserName, $secure)
if ($PSCmdlet.ShouldProcess($UserName, 'Initialiser le profil utilisateur')) {
    try {
        Start-Process -FilePath "$env:SystemRoot\System32\cmd.exe" -ArgumentList '/c exit' -Credential $cred `
            -LoadUserProfile -WorkingDirectory "$env:SystemRoot\System32" -Wait -WindowStyle Hidden
    } catch {
        Write-Warning "Profil non initialisé automatiquement ($($_.Exception.Message)). Ouvrez une session '$UserName' une fois, puis relancez ce script."
    }
}

# 3. Réglages du registre du compte dédié
$sid = (Get-LocalUser -Name $UserName).SID.Value
$profile = Get-CimInstance Win32_UserProfile -Filter "SID='$sid'" -ErrorAction SilentlyContinue
if (-not $profile) {
    if ($WhatIfPreference) { Write-Host "[WhatIf] Réglages du registre de $UserName (profil non encore créé)." }
    else { throw "Profil de '$UserName' introuvable : ouvrez une session avec ce compte une fois, puis relancez le script." }
} else {
    $hiveKey = "HKU\VANAS_KIOSK"
    $hivePath = Join-Path $profile.LocalPath 'NTUSER.DAT'
    $alreadyLoaded = Test-Path "Registry::HKEY_USERS\$sid"
    $root = if ($alreadyLoaded) { "Registry::HKEY_USERS\$sid" } else { 'Registry::HKEY_USERS\VANAS_KIOSK' }

    function Set-Value($path, $name, $value, $type = 'DWord') {
        if (-not (Test-Path "$root\$path")) { New-Item -Path "$root\$path" -Force | Out-Null }
        New-ItemProperty -Path "$root\$path" -Name $name -Value $value -PropertyType $type -Force | Out-Null
    }

    if ($PSCmdlet.ShouldProcess($UserName, 'Appliquer shell VANAS + restrictions')) {
        if (-not $alreadyLoaded) {
            & reg.exe load $hiveKey $hivePath | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Impossible de charger $hivePath" }
        }
        try {
            # VANAS remplace explorer.exe pour ce compte uniquement
            Set-Value 'Software\Microsoft\Windows NT\CurrentVersion\Winlogon' 'Shell' "`"$ClientExe`"" 'String'

            $policies = 'Software\Microsoft\Windows\CurrentVersion\Policies'
            Set-Value "$policies\System" 'DisableTaskMgr' 1
            Set-Value "$policies\System" 'DisableLockWorkstation' 1
            Set-Value "$policies\System" 'DisableChangePassword' 1
            Set-Value "$policies\System" 'DisableRegistryTools' 1
            Set-Value "$policies\Explorer" 'NoWinKeys' 1
            Set-Value "$policies\Explorer" 'NoRun' 1
            Set-Value "$policies\Explorer" 'NoLogoff' 1
            Set-Value "$policies\Explorer" 'NoClose' 1
            Set-Value 'Software\Policies\Microsoft\Windows\System' 'DisableCMD' 1
        } finally {
            if (-not $alreadyLoaded) {
                [gc]::Collect(); [gc]::WaitForPendingFinalizers()
                & reg.exe unload $hiveKey | Out-Null
            }
        }
    }
}

# 4. client.json : active le mode kiosque pour ce compte
$configPath = Join-Path (Split-Path $ClientExe) 'client.json'
if ($PSCmdlet.ShouldProcess($configPath, 'Activer KioskMode')) {
    $config = if (Test-Path $configPath) { Get-Content $configPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
    $config | Add-Member -NotePropertyName KioskMode -NotePropertyValue $true -Force
    $config | Add-Member -NotePropertyName KioskAccount -NotePropertyValue $UserName -Force
    $config | ConvertTo-Json | Set-Content -Path $configPath -Encoding UTF8
}

# 5. Connexion automatique (optionnelle)
if ($EnableAutoLogon -and $PSCmdlet.ShouldProcess($UserName, 'Activer la connexion automatique')) {
    $winlogon = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon'
    Set-ItemProperty $winlogon -Name AutoAdminLogon -Value '1'
    Set-ItemProperty $winlogon -Name DefaultUserName -Value $UserName
    Set-ItemProperty $winlogon -Name DefaultDomainName -Value $env:COMPUTERNAME
    Set-ItemProperty $winlogon -Name DefaultPassword -Value $Password
    Write-Warning 'Le mot de passe est stocké en clair dans le registre (HKLM). Pour mieux faire, utilisez Sysinternals Autologon qui le chiffre.'
}

Write-Host ''
Write-Host "Kiosque configuré pour le compte '$UserName'." -ForegroundColor Green
if ($generated) { Write-Host "Mot de passe généré (notez-le, il ne sera plus affiché) : $Password" -ForegroundColor Yellow }
Write-Host "Définissez aussi AdminPinHash dans client.json (kiosk\New-AdminPin.ps1), sinon aucune sortie du kiosque ne sera possible."
Write-Host "Ouvrez une session '$UserName' pour tester. Votre propre compte n'a pas été modifié."
