# 1. Custom heuristic scheduler over constraint solver

Date: 2026-05-24

## Status

Accepted

## Context

The core feature of Smash Dates is auto-scheduling badminton league fixtures subject to a mix of hard physical constraints (a team plays once per date, venue capacity, blocked dates, double round-robin completeness, derby-first, gender/week-type matching) and soft constraints (avoid back-to-back nights, maximise gap between home and away legs of the same pairing). The schedule must be regenerable incrementally when matches are rejected.

Three approaches were considered:

- **CP-SAT / constraint solver** (Google OR-Tools via P/Invoke or sidecar). Declarative, near-optimal, but pulls in native binaries that complicate the container image and add operational surface area.
- **MILP** (also OR-Tools). Same dependency cost; round-robin pairing constraints are awkward to express linearly.
- **Custom heuristic** — round-robin base via Berger tables, greedy date assignment, local-search (2-opt swap) to reduce soft-penalty cost.

Expected league sizes are small: 4–12 teams per division, 12–132 matches per division per season. A heuristic terminates in milliseconds at that scale and the soft objective is smooth enough for local search to converge on a good schedule.

## Decision

Implement a custom heuristic scheduler in pure C# behind an `IScheduler` abstraction. The implementation will:

1. Generate the canonical double round-robin pairing list per Division using Berger tables.
2. Greedy-place each Match onto a `(Week, Venue, Date)` slot honouring all hard constraints. Place derbies first to satisfy the derby-first rule.
3. Run local-search swaps (exchange two Matches' date slots) for a fixed iteration budget, accepting swaps that reduce total soft-penalty cost.

The scheduler runs as an asynchronous background job triggered by the LeagueAdmin. Incremental re-runs lock `Confirmed` Matches and only reshuffle `Proposed` + `Rejected` ones.

## Consequences

**Positive**
- Zero external dependencies; the runtime container stays slim.
- Fully debuggable in C#: deterministic with a seeded RNG, step-traceable.
- Easy to unit-test individual phases (pairing list, hard-constraint validator, swap cost delta).
- Per-League configurable penalty weights are trivial to inject.

**Negative**
- No optimality guarantee. Pathological inputs may produce schedules a solver would improve by single-digit percent on soft cost.
- Local-search quality is sensitive to neighbourhood design; future tuning expected.

**Mitigation**
- `IScheduler` interface keeps the door open to plug in OR-Tools later without disturbing callers. The data model (Matches with a status lifecycle) is engine-agnostic.

## Implementation note (phased rollout)

The scheduler is delivered in stages behind the same `IScheduler` boundary:

1. **Done:** Berger double round-robin → derby-first → greedy placement satisfying all **hard** constraints, run **synchronously** (millisecond runtimes at expected scale). Generation persists `Proposed` matches and moves the season to `Proposed`; an unschedulable input returns 422 and changes nothing.
2. **Done:** incremental re-run — `SchedulerInput.Locked` carries the `Confirmed` fixtures (occupancy seeded, pairings not re-emitted) so the engine re-places only the rest. Triggered manually by the LeagueAdmin (`POST …/rerun`); all-or-nothing.
3. **Done:** soft-penalty **2-opt local search** — after greedy placement, deterministically swap two matches' dates whenever it stays hard-feasible and lowers `SchedulerCost` (team-spread + home/away leg-gap penalties, default weights). `SchedulerHardConstraints.IsFeasible` guards every move.
4. **Later:** async background-job execution (with the `Scheduling` season state) and per-League penalty configuration — layered in without changing callers.
