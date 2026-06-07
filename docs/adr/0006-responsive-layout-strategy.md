# 6. Responsive layout strategy (phone-first floor at 360px)

Date: 2026-06-07

## Status

Accepted

## Context

The app is admin tooling rendered in a brutalist `font-mono` Tailwind theme. Most screens were built desktop-first: multi-column grids, wide data tables, and a shared centred-card modal. The one screen that is genuinely used on a phone in the field is the live [Pegboard Session](../../CONTEXT.md#pegboard-session) board — a [SessionHost](../../CONTEXT.md#roles-and-access) runs a club night standing at courtside, one-handed, with no laptop. The board must therefore work well on a phone, not merely survive shrinking.

Two forces pull against each other:

- A monospace theme is **wider per character** than a proportional font, so text-heavy rows and fixed multi-column grids run out of horizontal room sooner than they would elsewhere.
- The board's core loop — *free court → who's waiting → fill it* — wants the courts and the waiting queue both reachable without losing your place.

We needed a consistent rule for how any screen collapses to a narrow viewport, rather than ad-hoc breakpoints per page. We picked the rules by prototyping the three obvious board layouts (stacked / tabs / bottom sheet) at phone width and choosing on feel.

Alternatives considered for the board's court↔queue arrangement:

- **Stacked single column** — all courts, then the queue below. Zero new code, but the queue is never co-visible with a free court and the page is a long scroll.
- **Bottom sheet** — courts scroll full-width, the queue peeks from a pinned sheet that expands. Most board-like, but introduces sheet/gesture machinery used nowhere else.
- **Segmented tabs** — `Courts | Waiting`, one pane at a time below the desktop breakpoint, the existing two-pane retained above it.

## Decision

Adopt one app-wide responsive strategy:

- **360px is the supported floor.** Layouts must not break between 360px and desktop; 320px should remain usable but is not optimised for.
- **Narrow = one thing at a time; wide = multi-pane.** Below Tailwind's `lg` (1024px) a screen that is multi-pane on desktop collapses to a **single active pane**, switched by a segmented control where two panes are equally primary (the Pegboard board uses `Courts | Waiting` tabs). At `lg`+ the existing multi-pane layout is retained unchanged.
- **Side-by-side content cells flex and wrap, never fix.** Versus-style layouts (e.g. a court's Side A `v` Side B) stay horizontal but use `flex` + `min-w-0` so names wrap inside each cell instead of forcing overflow.
- **Modals are height-capped and body-scroll.** The shared modal is `max-h`-constrained with a fixed header and a scrolling body, so a tall dialog's primary action is always reachable on a short screen. Inner scroll regions inside a modal are dropped on phone to avoid nested scrollbars.
- **Touch targets stay ≥ 44px** (`min-h-11`), which already held across the app.

## Consequences

**Positive**

- The Pegboard host — the one real phone user — gets a board that works at courtside, and the rule generalises: future multi-pane screens have a known way to collapse.
- The modal fix is global and corrects a real bug (a dialog's submit button could sit below the fold on a short phone with no way to scroll to it).
- No new layout primitives or gesture code: tabs are a visibility toggle, and everything else is Tailwind responsive utilities.

**Negative / limits**

- Tabs mean the two panes are never co-visible on a phone; checking the queue is a tab away from the courts. Accepted as the cheapest option that keeps both panes full-size (the bottom sheet would keep them co-visible at the cost of bespoke machinery).
- The breakpoint is width-only, so a phone in landscape (wide but short) gets tabs — which is the right call there anyway, as a side-by-side queue would be cramped vertically.
- Other desktop-first screens (admin tables, league/club detail) are **not** retrofitted by this ADR; they are audited and migrated to this strategy separately.
