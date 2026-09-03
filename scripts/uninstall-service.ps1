$ErrorActionPreference = 'Stop'
$serviceName = 'FriporaFiscalBot'
Stop-Service $serviceName -ErrorAction SilentlyContinue
sc.exe delete $serviceName | Out-Null
Write-Host "Serviço removido: $serviceName"
