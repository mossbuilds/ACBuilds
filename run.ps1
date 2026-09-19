# ACBuilds one-shot launcher for Windows: installs Docker Desktop if missing, pulls + runs the image, prints where to connect.
# Usage: .\run.ps1 [-Dats C:\path\to\dats]    (folder with client_cell_1.dat, client_portal.dat, client_highres.dat, client_local_English.dat)
param([string]$Dats = (Join-Path $PWD 'dats'), [string]$Image = 'ghcr.io/mossbuilds/acbuilds:latest')
$ErrorActionPreference = 'Stop'

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  Write-Host 'Docker not found - installing Docker Desktop (needs admin + may need a reboot)...'
  winget install -e --id Docker.DockerDesktop --accept-source-agreements --accept-package-agreements
  Write-Host 'Docker Desktop installed. Reboot if asked, start Docker Desktop once, then re-run this script.'
  exit 0
}
& docker info *> $null
if ($LASTEXITCODE -ne 0) {
  $exe = "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe"
  if (Test-Path $exe) { Start-Process $exe }
  Write-Host 'Waiting for Docker to start...'
  for ($i = 0; $i -lt 60; $i++) { & docker info *> $null; if ($LASTEXITCODE -eq 0) { break }; Start-Sleep 5 }
  & docker info *> $null; if ($LASTEXITCODE -ne 0) { throw 'Docker is not running.' }
}

New-Item -ItemType Directory -Force $Dats | Out-Null
if (-not (Get-ChildItem $Dats -Filter 'client_*.dat' -ErrorAction SilentlyContinue)) { throw "Put your AC client DAT files in: $Dats  then re-run." }

docker pull $Image
docker rm -f ace *> $null
docker run -d --name ace --restart unless-stopped -p 9000:9000/udp -p 9001:9001/udp -v "${Dats}:/ace/Dats" -v ace-db:/var/lib/mysql $Image | Out-Null

$ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254.*' -and $_.PrefixOrigin -ne 'WellKnown' } | Select-Object -First 1).IPAddress
if (-not $ip) { $ip = '127.0.0.1' }
Write-Host ''
Write-Host '=============================================='
Write-Host '  ACE server is starting (give it ~1 minute)'
Write-Host "  Connect your client to:  $ip : 9000"
Write-Host '  (same machine: 127.0.0.1 : 9000)'
Write-Host '  Logs: docker logs -f ace'
Write-Host '=============================================='
