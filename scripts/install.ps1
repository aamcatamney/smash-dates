# Smash Dates - production bootstrap (Windows / PowerShell).
#
# Downloads the production compose + Caddyfile, generates a Postgres password, and starts the
# stack (published image + PostgreSQL + Caddy HTTPS). No source checkout or build needed.
#
#   irm https://raw.githubusercontent.com/aamcatamney/smash-dates/main/scripts/install.ps1 | iex
#
# For a public HTTPS certificate, set a real domain first (DNS -> this host, ports 80/443 open):
#   $env:DOMAIN='league.example.com'; irm .../install.ps1 | iex
$ErrorActionPreference = 'Stop'

$raw = 'https://raw.githubusercontent.com/aamcatamney/smash-dates/main'
$dir = if ($env:SMASH_DIR) { $env:SMASH_DIR } else { 'smash-dates' }
$domain = if ($env:DOMAIN) { $env:DOMAIN } else { 'localhost' }

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
  throw 'Docker is required - see https://docs.docker.com/get-docker/'
}

New-Item -ItemType Directory -Force -Path $dir | Out-Null
Set-Location $dir

Write-Host "-> Fetching deployment files into $(Get-Location)"
Invoke-WebRequest "$raw/deploy/docker-compose.prod.yml" -OutFile docker-compose.yml
Invoke-WebRequest "$raw/deploy/Caddyfile" -OutFile Caddyfile

if (-not (Test-Path .env)) {
  $pw = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 32 | ForEach-Object { [char]$_ })
  "DOMAIN=$domain`nPOSTGRES_PASSWORD=$pw`nTAG=latest`n" | Set-Content -Path .env -NoNewline
  Write-Host "-> Wrote .env (generated POSTGRES_PASSWORD; DOMAIN=$domain)"
}
else {
  Write-Host "-> Keeping existing .env"
}

Write-Host "-> Starting (docker compose up -d)..."
docker compose up -d

Write-Host ""
Write-Host "OK  Smash Dates is starting at https://$domain"
Write-Host "  - Register the first account - it becomes the SystemAdmin."
Write-Host "  - DOMAIN=localhost uses a self-signed cert (browser warning is expected)."
Write-Host "    For a trusted cert, set DOMAIN to a real hostname in .env and re-run 'docker compose up -d'."
