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
- Update the screenshots under `docs/screenshots/` wherever a change alters the UI they show. Capture them from a seeded run of the app and regenerate the affected images (light/dark) rather than leaving them outdated.
- Keep the domain glossary `CONTEXT.md` and any relevant ADRs in `docs/adr/` in step with behavioural and architectural changes.
