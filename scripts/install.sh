#!/usr/bin/env bash
# Smash Dates — production bootstrap.
#
# Downloads the production compose + Caddyfile, generates a Postgres password, and starts the
# stack (published image + PostgreSQL + Caddy HTTPS). No source checkout or build needed.
#
#   curl -fsSL https://raw.githubusercontent.com/aamcatamney/smash-dates/main/scripts/install.sh | bash
#
# For a public HTTPS certificate, set a real domain (DNS must point at this host, ports 80/443 open):
#   curl -fsSL .../install.sh | DOMAIN=league.example.com bash
set -euo pipefail

RAW="https://raw.githubusercontent.com/aamcatamney/smash-dates/main"
DIR="${SMASH_DIR:-smash-dates}"
DOMAIN="${DOMAIN:-localhost}"

if ! command -v docker >/dev/null 2>&1; then
  echo "Docker is required — see https://docs.docker.com/get-docker/" >&2
  exit 1
fi

mkdir -p "$DIR"
cd "$DIR"

echo "→ Fetching deployment files into $(pwd)"
curl -fsSL "$RAW/deploy/docker-compose.prod.yml" -o docker-compose.yml
curl -fsSL "$RAW/deploy/Caddyfile" -o Caddyfile

if [ ! -f .env ]; then
  pw="$(openssl rand -hex 24 2>/dev/null || head -c 24 /dev/urandom | base64 | tr -dc 'a-zA-Z0-9' | head -c 32)"
  printf 'DOMAIN=%s\nPOSTGRES_PASSWORD=%s\nTAG=latest\n' "$DOMAIN" "$pw" >.env
  echo "→ Wrote .env (generated POSTGRES_PASSWORD; DOMAIN=$DOMAIN)"
else
  echo "→ Keeping existing .env"
  DOMAIN="$(grep -E '^DOMAIN=' .env | cut -d= -f2- || echo "$DOMAIN")"
fi

echo "→ Starting (docker compose up -d)…"
docker compose up -d

cat <<EOF

✓ Smash Dates is starting at https://${DOMAIN}
  • Register the first account — it becomes the SystemAdmin.
  • DOMAIN=localhost uses a self-signed cert (browser warning is expected).
    For a trusted cert, set DOMAIN to a real hostname in .env and re-run 'docker compose up -d'.
  • Manage it from this folder: docker compose logs -f · docker compose down
EOF
