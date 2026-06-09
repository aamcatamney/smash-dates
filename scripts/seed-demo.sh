#!/usr/bin/env bash
# Seed a running smash-dates instance with a coherent demo league, for screenshots and
# manual exploration. Drives the real HTTP API (so every domain rule is honoured) as the
# bootstrap SystemAdmin — who is also made admin of every demo league and club, so one
# session can set everything up.
#
# Usage:
#   1. Start a FRESH database + the app, e.g.
#        docker run -d --name sd-demo -e POSTGRES_DB=smash_dates -e POSTGRES_USER=postgres \
#          -e POSTGRES_PASSWORD=postgres -p 5432:5432 postgres:17-alpine
#        ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=smash_dates;Username=postgres;Password=postgres" \
#          ASPNETCORE_ENVIRONMENT=Development dotnet run
#   2. Run this script:        scripts/seed-demo.sh
#      Against another origin:  BASE_URL=http://localhost:8080 scripts/seed-demo.sh
#
# Requires: bash, curl, python3. Expects an EMPTY database (it registers the first user,
# which becomes the SystemAdmin). Re-running against a populated DB will fail fast.
set -euo pipefail

BASE_URL="${BASE_URL:-http://localhost:5080}"
ADMIN_EMAIL="${ADMIN_EMAIL:-admin@smash-dates.test}"
ADMIN_PASSWORD="${ADMIN_PASSWORD:-correct-horse-battery}"
ADMIN_NAME="${ADMIN_NAME:-Demo Admin}"
RUBBERS=5 # rubbers per match; recorded results must sum to this

CJ="$(mktemp)"
trap 'rm -f "$CJ"' EXIT

say() { printf '\033[36m▸ %s\033[0m\n' "$*"; }
die() { printf '\033[31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

# JSON field extractor: `... | jget a.b` or `jget 0.id` for arrays.
jget() { python3 -c '
import sys, json
d = json.load(sys.stdin)
for k in sys.argv[1].split("."):
    d = d[int(k)] if isinstance(d, list) else d[k]
print(d)
' "$1"; }

post() { curl -fsS -b "$CJ" -c "$CJ" -X POST "$BASE_URL$1" -H 'Content-Type: application/json' -d "${2:-}"; }
postn() { curl -fsS -b "$CJ" -c "$CJ" -X POST "$BASE_URL$1"; } # no body
get() { curl -fsS -b "$CJ" "$BASE_URL$1"; }

# --- 0. Preflight ---------------------------------------------------------------------
curl -fsS "$BASE_URL/api/version" >/dev/null 2>&1 || die "App not reachable at $BASE_URL"
say "Seeding demo data into $BASE_URL"

# --- 1. Bootstrap admin (first user -> SystemAdmin, auto-verified, signed in) ----------
reg_code=$(curl -fsS -o /dev/null -w '%{http_code}' -b "$CJ" -c "$CJ" \
  -X POST "$BASE_URL/api/auth/register" -H 'Content-Type: application/json' \
  -d "{\"email\":\"$ADMIN_EMAIL\",\"password\":\"$ADMIN_PASSWORD\",\"displayName\":\"$ADMIN_NAME\"}" || true)
[ "$reg_code" = "200" ] || die "Expected an empty DB: registering the bootstrap admin returned HTTP $reg_code"
ADMIN_ID=$(get /api/auth/me | jget id)
say "Bootstrap admin $ADMIN_EMAIL ($ADMIN_ID)"

# --- 2. League + divisions -------------------------------------------------------------
LEAGUE_ID=$(post /api/leagues \
  "{\"name\":\"North London Badminton League\",\"description\":\"Autumn 2026 season\",\"firstLeagueAdminUserId\":\"$ADMIN_ID\"}" | jget id)
say "League $LEAGUE_ID"

DIV_ID=$(post "/api/leagues/$LEAGUE_ID/divisions" \
  "{\"name\":\"Division 1\",\"gender\":\"Mens\",\"rank\":1,\"rubbersPerMatch\":$RUBBERS,\"winPoints\":2,\"drawPoints\":1,\"lossPoints\":0}" | jget id)
post "/api/leagues/$LEAGUE_ID/divisions" \
  "{\"name\":\"Division 2\",\"gender\":\"Ladies\",\"rank\":2,\"rubbersPerMatch\":$RUBBERS,\"winPoints\":2,\"drawPoints\":1,\"lossPoints\":0}" >/dev/null
say "Divisions created"

# --- 3. Clubs, each with a venue + a Mens team entered in Division 1 --------------------
CLUB_NAMES=("Riverside Smashers" "Parkside Badminton Club" "Hillcrest Racquets" "Lakeside Shuttlers")
CLUB_CODES=("RIV" "PARK" "HILL" "LAKE")
declare -a CLUB_IDS TEAM_IDS
for i in "${!CLUB_NAMES[@]}"; do
  name="${CLUB_NAMES[$i]}"; code="${CLUB_CODES[$i]}"
  cid=$(post /api/clubs \
    "{\"name\":\"$name\",\"shortCode\":\"$code\",\"contactEmail\":\"${code,,}@example.com\",\"firstClubAdminUserId\":\"$ADMIN_ID\"}" | jget id)
  CLUB_IDS[$i]="$cid"
  post "/api/clubs/$cid/venues" "{\"name\":\"$name Hall\",\"courts\":4,\"maxConcurrentMatches\":2,\"address\":\"$name Leisure Centre, 12 High St\"}" >/dev/null
  tid=$(post "/api/clubs/$cid/teams" "{\"name\":\"$name I\",\"gender\":\"Mens\"}" | jget id)
  TEAM_IDS[$i]="$tid"
  # invite to league (league admin) then accept (club admin) — same admin does both
  mid=$(post "/api/leagues/$LEAGUE_ID/memberships" "{\"clubId\":\"$cid\"}" | jget id)
  postn "/api/leagues/$LEAGUE_ID/memberships/$mid/accept" >/dev/null
  say "Club $name ($code) — venue, team, membership accepted"
done

# --- 4. Players + discipline registrations (some confirmed, some left pending) ----------
PLAYER_NAMES=("James Carter" "Daniel Brook" "Marcus Webb" "Oliver Hunt")
declare -a RIV_PLAYER_IDS
riv="${CLUB_IDS[0]}"
for i in "${!PLAYER_NAMES[@]}"; do
  pid=$(post "/api/clubs/$riv/players" \
    "{\"fullName\":\"${PLAYER_NAMES[$i]}\",\"gender\":\"Male\",\"type\":\"Member\"}" | jget id)
  RIV_PLAYER_IDS[$i]="$pid"
  rid=$(post "/api/clubs/$riv/players/$pid/registrations" \
    "{\"leagueId\":\"$LEAGUE_ID\",\"discipline\":\"Level\"}" | jget id)
  # confirm the first two; leave the rest Pending so the league-approvals view has content
  if [ "$i" -lt 2 ]; then
    postn "/api/leagues/$LEAGUE_ID/registrations/$rid/confirm" >/dev/null
  fi
done
say "Players + registrations at ${CLUB_NAMES[0]} (2 confirmed, 2 pending)"

# The two confirmed players join the club's team squad (eligibility: confirmed Level + Male).
for i in 0 1; do
  post "/api/clubs/$riv/teams/${TEAM_IDS[0]}/players" "{\"playerId\":\"${RIV_PLAYER_IDS[$i]}\"}" >/dev/null
done
say "Squad: 2 players assigned to ${CLUB_NAMES[0]}'s team"

# --- 5. Season with weekly Level weeks, team entries, generated schedule ----------------
WEEKS_JSON=$(python3 -c '
import datetime, json
start = datetime.date(2026, 9, 7)  # a Monday
weeks = []
for w in range(8):
    s = start + datetime.timedelta(days=7 * w)
    e = s + datetime.timedelta(days=6)
    weeks.append({"startDate": s.isoformat(), "endDate": e.isoformat(), "weekType": "Level"})
print(json.dumps(weeks))
')
SEASON_ID=$(post "/api/leagues/$LEAGUE_ID/seasons" \
  "{\"name\":\"Autumn 2026\",\"startDate\":\"2026-09-01\",\"endDate\":\"2026-12-31\",\"weeks\":$WEEKS_JSON}" | jget id)
say "Season $SEASON_ID with 8 Level weeks"

for i in "${!TEAM_IDS[@]}"; do
  post "/api/leagues/$LEAGUE_ID/seasons/$SEASON_ID/entries" \
    "{\"teamId\":\"${TEAM_IDS[$i]}\",\"divisionId\":\"$DIV_ID\"}" >/dev/null
done
say "4 teams entered in Division 1"

say "Generating the schedule (background job)…"
postn "/api/leagues/$LEAGUE_ID/seasons/$SEASON_ID/generate" >/dev/null
status=""
for _ in $(seq 1 60); do
  body=$(get "/api/leagues/$LEAGUE_ID/seasons/$SEASON_ID")
  status=$(printf '%s' "$body" | jget status)
  case "$status" in
    Proposed) break ;;
    Draft) err=$(printf '%s' "$body" | jget schedulingError 2>/dev/null || echo "unknown"); die "Schedule generation failed: $err" ;;
  esac
  sleep 1
done
[ "$status" = "Proposed" ] || die "Schedule did not reach Proposed (last status: $status)"
say "Schedule generated (season Proposed)"

# --- 6. Confirm fixtures + record a handful of results (populates standings) ------------
MATCH_IDS=$(get "/api/leagues/$LEAGUE_ID/seasons/$SEASON_ID/matches" \
  | python3 -c 'import sys,json; [print(m["id"]) for m in json.load(sys.stdin)]')
SCORES=("3,2" "4,1" "2,3" "5,0" "3,2")
n=0
for mid in $MATCH_IDS; do
  postn "/api/matches/$mid/force-confirm" >/dev/null
  if [ "$n" -lt "${#SCORES[@]}" ]; then
    IFS=',' read -r hs as <<<"${SCORES[$n]}"
    post "/api/matches/$mid/result" "{\"homeScore\":$hs,\"awayScore\":$as,\"playedOn\":\"2026-09-10\"}" >/dev/null
  fi
  n=$((n + 1))
done
say "Confirmed $n fixtures; recorded ${#SCORES[@]} results"

# --- 6b. A second season left in Draft (shows the weeks editor + entries setup UI) ------
DRAFT_WEEKS='[{"startDate":"2027-01-11","endDate":"2027-01-17","weekType":"Level"},{"startDate":"2027-01-18","endDate":"2027-01-24","weekType":"Level"},{"startDate":"2027-01-25","endDate":"2027-01-31","weekType":"Level"}]'
DRAFT_SEASON_ID=$(post "/api/leagues/$LEAGUE_ID/seasons" \
  "{\"name\":\"Spring 2027\",\"startDate\":\"2027-01-01\",\"endDate\":\"2027-04-30\",\"weeks\":$DRAFT_WEEKS}" | jget id)
for i in 0 1; do
  post "/api/leagues/$LEAGUE_ID/seasons/$DRAFT_SEASON_ID/entries" \
    "{\"teamId\":\"${TEAM_IDS[$i]}\",\"divisionId\":\"$DIV_ID\"}" >/dev/null
done
say "Draft season 'Spring 2027' with weeks + 2 entries (left in Draft)"

# --- 7. Pegboard: an open club night at Riverside with a finished game ------------------
SESSION_ID=$(post "/api/clubs/$riv/pegboard/sessions" "{\"name\":\"Tuesday Club Night\"}" | jget id)
COURT_ID=$(post "/api/clubs/$riv/pegboard/sessions/$SESSION_ID/courts" "{\"label\":\"Court 1\"}" | jget id)
declare -a ATT_IDS
for pid in "${RIV_PLAYER_IDS[@]}"; do
  aid=$(post "/api/clubs/$riv/pegboard/sessions/$SESSION_ID/attendances" "{\"playerId\":\"$pid\"}" | jget id)
  ATT_IDS+=("$aid")
done
GAME_ID=$(post "/api/clubs/$riv/pegboard/sessions/$SESSION_ID/games?courtId=$COURT_ID" \
  "{\"type\":\"Doubles\",\"sideA\":[\"${ATT_IDS[0]}\",\"${ATT_IDS[1]}\"],\"sideB\":[\"${ATT_IDS[2]}\",\"${ATT_IDS[3]}\"]}" | jget id)
post "/api/clubs/$riv/pegboard/sessions/$SESSION_ID/games/$GAME_ID/finish" "{\"winnerSide\":\"A\",\"score\":\"21-18\"}" >/dev/null
say "Pegboard session open at ${CLUB_NAMES[0]} with one finished game"

printf '\033[32m✓ Demo seeded. Sign in as %s / %s\033[0m\n' "$ADMIN_EMAIL" "$ADMIN_PASSWORD"
