# Architecture

- Backend to use Dotnet 10 as a single project
- Do not use swagger
- Do not use Entity Framework Core
- Use Dapper in a repository pattern with Npgsql
- Use postgresql as the database
- Use a migration scripts folder with a service that applies them on startup
- Deployment will be done via a container
- Use Minimal API endpoints, one endpoint per file under `Endpoints/`

# Documentation

After making changes, update the docs they affect — treat stale docs as a bug:

- Keep `README.md` current: the feature list, tech stack, quick start, and the Screenshots section.
- Update the screenshots under `docs/screenshots/` wherever a change alters the UI they show, rather than leaving them outdated. Don't hand-roll demo data or drive the browser by hand each time: seed a fresh instance with [`scripts/seed-demo.sh`](scripts/seed-demo.sh), then run the capture helper [`scripts/capture-screenshots.sh`](scripts/capture-screenshots.sh) (optionally with image names to limit it, e.g. `scripts/capture-screenshots.sh players`) — it logs in as the demo admin, drives each view at the gallery's fixed viewport and writes the light/dark PNGs into `docs/screenshots/`. See [`docs/screenshots/README.md`](docs/screenshots/README.md) for setup and the image→view map.
- Keep the **GitHub Pages landing site** (`site/index.html`) in step with the README: its feature highlights, screenshot gallery and the "Run it" quick start mirror the README, so when a feature, screenshot or the quick start changes, update the site too. (Screenshots are bundled into the site at deploy time by `.github/workflows/pages.yml` — no copies are committed under `site/`.)
- Keep the domain glossary `CONTEXT.md` and any relevant ADRs in `docs/adr/` in step with behavioural and architectural changes. `CONTEXT.md` is a glossary only — no process/maintenance notes.
