# 4. Server-Sent Events for live Pegboard updates

Date: 2026-05-31

## Status

Accepted

## Context

The new [Pegboard Session](../../CONTEXT.md#pegboard-session) replaces a Club's physical club-night board. It is inherently a **shared, live** surface: a host drives it from a tablet while players watch the queue and courts on a wall display or their own phones. Any authenticated User can view a running board read-only; only the host (or an admin) mutates it. When the host fills a court, finishes a game or moves someone in the queue, every viewer's board must update without a manual refresh — a stale board on a shared screen is the exact failure the physical pegboard never had.

The app had **no real-time infrastructure** before this. It is a single .NET 10 process serving the Angular client same-origin, behind a TLS-terminating reverse proxy, authenticated by cookies + antiforgery.

Alternatives considered:

- **Short polling** — the client re-fetches the board every few seconds. No streaming infra, trivial to build, and easy to swap out later behind the same read endpoint. But it is laggy on a shared screen and chatty (every viewer polls regardless of activity), and a club night is bursty — long quiet spells punctuated by a flurry of changes when games finish together.
- **SignalR (WebSocket)** — .NET-native, battle-tested, bidirectional. But the traffic here is overwhelmingly **one-way** (server → viewers); writes are ordinary host actions. SignalR adds a hub, a client dependency (`@microsoft/signalr`), and WebSocket support/affinity to configure through the reverse proxy — machinery we don't need for one-way push.
- **Server-Sent Events (SSE)** — a one-way server → client stream over plain HTTP (`text/event-stream`). Mutations stay as normal POST endpoints that, on success, publish a "board changed" event; viewers subscribe to the Session's stream and re-render. `EventSource` is built into the browser, rides the existing cookie auth and proxy untouched, and auto-reconnects.

## Decision

Use **SSE** for live Pegboard updates.

- A streaming endpoint, `GET /api/clubs/{clubId}/pegboard/sessions/{sessionId}/stream` (`text/event-stream`), authorized for any authenticated User (same as viewing the board).
- All board mutations remain ordinary REST endpoints (`SessionHost`/`ClubAdmin`/`SystemAdmin` only). On a successful mutation the server publishes a board-changed event for that Session through an **in-process publisher**.
- The client opens an `EventSource` for the active Session and, on each event, re-fetches (or patches) the board view. On `Closed`, the stream ends.

## Consequences

**Positive**

- No new dependency and no protocol upgrade: SSE is plain HTTP, so the existing cookie auth, antiforgery posture and `X-Forwarded-*` reverse-proxy setup carry over unchanged.
- Matches the actual traffic shape — one-way fan-out to read-only viewers, with writes as normal POSTs — so the read and write paths stay simple and independently testable.
- Graceful degradation: `EventSource` reconnects automatically, and because every event just triggers a board read, a missed event self-heals on the next one.

**Negative / limits**

- The publisher is **in-process**, so this assumes a **single app instance**. If the app is ever scaled horizontally, two viewers pinned to different instances would miss each other's events. Going multi-instance later requires a backplane (e.g. Postgres `LISTEN/NOTIFY` or Redis) behind the same publisher abstraction — deliberately deferred, not designed in now.
- SSE is one-way: it is unsuitable if the board ever needs low-latency client → server messaging (it doesn't today — writes are plain POSTs).
- Each viewer holds an open connection; many concurrent club nights mean many long-lived requests to budget for.

**Mitigation**

- Hide the transport behind a small publisher/subscriber abstraction so swapping the in-process bus for a Postgres/Redis backplane is a one-place change if multi-instance scaling ever lands.
- Events are content-free "board changed" signals that trigger a normal authorized read, so the stream never becomes a second, divergent source of truth — it only tells clients *when* to read.
