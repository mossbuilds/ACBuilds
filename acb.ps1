# ACBuilds admin tool (Windows): backup, restore, update, status.
#   .\acb.ps1 backup [-All]         dump ace_auth+ace_shard (accounts, characters) to backups\  (-All adds ace_world)
#   .\acb.ps1 restore <file.sql>    load a backup into the running database
#   .\acb.ps1 update [server|db|all] backup FIRST, pull new images, redeploy (db: fresh seeded DB, then your auth+shard restored)
#   .\acb.ps1 status
param([Parameter(Position=0)][string]$Cmd, [Parameter(Position=1)][string]$Arg, [switch]$All)
$ErrorActionPreference = 'Continue'  # docker prints harmless stderr warnings; failures are checked via exit codes/throw; Set-Location $PSScriptRoot; New-Item -ItemType Directory -Force backups | Out-Null
function Backup {
  $dbs = if ($All) { 'ace_auth ace_shard ace_world' } else { 'ace_auth ace_shard' }
  $f = "backups\ace-$(Get-Date -Format yyyyMMdd-HHmmss).sql"
  # dump inside the container (no PowerShell pipe encoding issues), then copy out
  docker exec ace-db sh -c "mariadb-dump -h127.0.0.1 -uace -pace-local --single-transaction --databases $dbs > /tmp/b.sql"
  if ($LASTEXITCODE -ne 0) { throw 'Backup FAILED' }
  docker cp ace-db:/tmp/b.sql $f; docker exec ace-db rm /tmp/b.sql
  if (-not (Test-Path $f) -or (Get-Item $f).Length -lt 1000) { throw 'Backup FAILED (empty file)' }
  Write-Host "Backup OK: $f ($([int]((Get-Item $f).Length/1KB)) KB)"; return $f
}
function Restore($f) {
  if (-not (Test-Path $f)) { throw "No such file: $f" }
  docker compose stop ace-server
  docker cp $f ace-db:/tmp/r.sql; docker exec ace-db sh -c 'mariadb -h127.0.0.1 -uace -pace-local < /tmp/r.sql; rm /tmp/r.sql'
  if ($LASTEXITCODE -ne 0) { throw 'Restore FAILED' }
  docker compose start ace-server; Write-Host 'Restore done.'
}
switch ($Cmd) {
  'backup'  { Backup | Out-Null }
  'restore' { Restore $Arg }
  'update'  {
    $what = if ($Arg) { $Arg } else { 'all' }; $b = Backup   # aborts here if the backup fails
    if ($what -eq 'server') { docker compose pull ace-server; docker compose up -d ace-server }
    elseif ($what -in 'db','all') {
      docker compose pull; docker compose stop ace-server
      Write-Host 'Replacing database volume with the new pre-seeded image, then restoring your accounts and characters...'
      docker compose rm -sf ace-db; docker volume rm acbuilds_ace-db | Out-Null
      docker compose up -d ace-db
      while ((docker inspect -f '{{.State.Health.Status}}' ace-db) -ne 'healthy') { Start-Sleep 3 }
      docker cp $b ace-db:/tmp/r.sql; docker exec ace-db sh -c 'mariadb -h127.0.0.1 -uace -pace-local < /tmp/r.sql; rm /tmp/r.sql'
      docker compose up -d
    } else { throw 'update [server|db|all]' }
    Write-Host "Update complete. Backup kept at $b"
  }
  'status'  { docker compose ps; Get-ChildItem backups | Sort-Object LastWriteTime -Desc | Select-Object -First 1 | ForEach-Object { "Newest backup: $($_.Name)" } }
  default   { Get-Content $PSCommandPath -TotalCount 6 | Select-Object -Skip 1; exit 1 }
}
