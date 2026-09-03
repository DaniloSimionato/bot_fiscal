param(
    [string]$InstallRoot = 'C:\Program Files\Fripora Fiscal Bot'
)

$ErrorActionPreference = 'Stop'
$serviceName = 'FriporaFiscalBot'
$exe = Join-Path $InstallRoot 'FriporaFiscalBot.Service.exe'

if (-not (Test-Path $exe)) { throw "Executável não encontrado: $exe" }
New-Item -ItemType Directory -Force -Path (Join-Path $InstallRoot 'logs') | Out-Null

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service $serviceName -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name $serviceName -BinaryPathName "`"$exe`"" -DisplayName 'Fripora Fiscal Bot Service' -StartupType Automatic
sc.exe failure $serviceName actions= restart/60000/restart/60000/restart/60000 reset= 86400 | Out-Null
Start-Service $serviceName
Write-Host "Serviço instalado e iniciado: $serviceName"
