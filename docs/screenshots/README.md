# Screenshots

Screenshots embedded in the root `README.md`, captured from a seeded demo league:

| File | Shows |
|------|-------|
| `leagues.png` | Leagues list (`/admin/leagues`) |
| `league-detail.png` | League detail — divisions, seasons, member clubs |
| `season-setup.png` | Season weeks editor + team entries |
| `fixtures.png` | A generated season's fixtures |
| `match-actions.png` | Confirm / reject / record-result controls on fixtures |
| `standings.png` | Division standings table |
| `club-detail.png` | Club detail — teams, venues, blocked dates, matches |
| `csv-import.png` | Bulk CSV import dialog with a per-row result |
| `dark-mode.png` | League detail in the dark theme |
| `players.png` | Player registrations + transfers awaiting league approval |
| `pegboard-sessions.png` | Club page → Sessions tab: past/current club nights + "Open session" |
| `pegboard-board.png` | Full-screen live pegboard board — courts grid + waiting queue |

Suggested width ~1400px, PNG.

> **Pending capture:** `pegboard-sessions.png` and `pegboard-board.png` are not yet captured (no browser-automation tooling in the repo — screenshots are taken by hand from a seeded run). To capture: start Postgres + the app (see root README "Run locally"), register the first user, create a club and a few graded players, open the **Sessions** tab, open a session, add 2 courts + ~6 attendees and start a couple of games, then screenshot the Sessions tab and the board route `/admin/clubs/{id}/pegboard/{sessionId}` in both light and dark themes. Once captured, add the **Club night (pegboard)** subsection with the two `![...]` image refs to the root `README.md` Screenshots section.
