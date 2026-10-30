<#
.SYNOPSIS
    Calcule la valeur "AdminPinHash" à coller dans client.json (format identique à VANAS.Shared.Security.PinHasher).

.EXAMPLE
    .\New-AdminPin.ps1 -Pin 482913
    .\New-AdminPin.ps1 -Pin 482913 -ClientConfig C:\VANAS\Client\client.json   # écrit directement dans le fichier
#>
param(
    [Parameter(Mandatory)][ValidateLength(4, 16)][string]$Pin,
    [string]$ClientConfig
)

$iterations = 120000
$salt = New-Object byte[] 16
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($salt)
$pbkdf2 = New-Object System.Security.Cryptography.Rfc2898DeriveBytes($Pin, $salt, $iterations, [System.Security.Cryptography.HashAlgorithmName]::SHA256)
$hash = $pbkdf2.GetBytes(32)
$value = 'pbkdf2-sha256${0}${1}${2}' -f $iterations, [Convert]::ToBase64String($salt), [Convert]::ToBase64String($hash)

if ($ClientConfig) {
    $config = Get-Content $ClientConfig -Raw | ConvertFrom-Json
    $config | Add-Member -NotePropertyName AdminPinHash -NotePropertyValue $value -Force
    $config | ConvertTo-Json | Set-Content -Path $ClientConfig -Encoding UTF8
    Write-Host "AdminPinHash écrit dans $ClientConfig"
} else {
    Write-Host 'Collez cette valeur dans client.json, clé "AdminPinHash" :'
    Write-Output $value
}
