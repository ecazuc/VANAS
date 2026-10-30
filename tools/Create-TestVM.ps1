<#
.SYNOPSIS
    Crée une machine virtuelle VirtualBox Windows 11 pour tester le mode kiosque VANAS sans risque.
    Installation Windows entièrement automatique (compte administrateur local de test).

.PARAMETER IsoPath       ISO officiel de Windows 11.
.PARAMETER ImageIndex    Édition Windows dans l'ISO (6 = Professionnel sur les ISO multi-éditions fr-FR).
.PARAMETER ShareDir      Dossier de l'hôte monté dans la VM (lecture seule) : client publié + scripts kiosk.
#>
param(
    [Parameter(Mandatory)][string]$IsoPath,
    [int]$ImageIndex = 6,
    [string]$Name = 'VANAS-Kiosk-Test',
    [string]$MachinesDir = 'Q:\VM\machines',
    [string]$ShareDir = 'Q:\VM\share',
    [int]$MemoryMB = 8192,
    [int]$Cpus = 4,
    [int]$DiskGB = 80,
    [string]$AdminUser = 'vmadmin',
    [string]$AdminPassword = 'Vanas-Test-1'
)

$ErrorActionPreference = 'Stop'
$vb = 'C:\Program Files\Oracle\VirtualBox\VBoxManage.exe'
function Vb {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"   # VirtualBox writes progress to stderr, which PowerShell 5.1 would treat as an error
    $output = & $vb @args 2>&1
    $ErrorActionPreference = $previous
    if ($LASTEXITCODE -ne 0) { throw "VBoxManage $($args -join ' ') a échoué ($LASTEXITCODE) : $output" }
}

if (-not (Test-Path $IsoPath)) { throw "ISO introuvable : $IsoPath" }
if ((& $vb list vms) -match "`"$Name`"") { throw "La VM '$Name' existe déjà. Supprimez-la avec : VBoxManage unregistervm '$Name' --delete" }

Vb createvm --name $Name --ostype Windows11_64 --basefolder $MachinesDir --register
Vb modifyvm $Name --memory $MemoryMB --cpus $Cpus --vram 128 --graphicscontroller vboxsvga `
    --firmware efi --tpm-type 2.0 --nic1 nat --clipboard-mode bidirectional --draganddrop bidirectional --audio-enabled off

$disk = Join-Path $MachinesDir "$Name\$Name.vdi"
Vb createmedium disk --filename $disk --size ($DiskGB * 1024) --variant Standard
Vb storagectl $Name --name SATA --add sata --controller IntelAhci --portcount 2
Vb storageattach $Name --storagectl SATA --port 0 --device 0 --type hdd --medium $disk

if (Test-Path $ShareDir) {
    Vb sharedfolder add $Name --name vanas --hostpath $ShareDir --readonly --automount --auto-mount-point 'V:'
}

Vb unattended install $Name --iso $IsoPath --user $AdminUser --password $AdminPassword --full-user-name 'Admin VM' `
    --install-additions --locale fr_FR --country FR --time-zone 'Europe/Paris' --hostname 'vanas-vm.local' `
    --image-index $ImageIndex --start-vm gui

Write-Host "VM '$Name' créée et démarrée. L'installation de Windows prend 15 à 30 minutes (plusieurs redémarrages automatiques)." -ForegroundColor Green
Write-Host "Compte de la VM : $AdminUser / $AdminPassword (VM de test uniquement)."
