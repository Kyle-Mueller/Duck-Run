# Control Dashboard — Docker Compose

A one-command stack: the dashboard plus a Postgres database.

## Run

1. `cp .env.example .env` and edit the values (at minimum `DASHBOARD_SECRET`, `POSTGRES_PASSWORD`, `ADMIN_EMAIL`, `ADMIN_PASSWORD`).
2. `docker compose up -d`
3. Open http://localhost:8080 and log in with the `ADMIN_EMAIL` / `ADMIN_PASSWORD` you set. Change the password after first login.

## Notes

- The dashboard image is pinned to this release. To track the newest build instead, change the `image:` tag to `:latest` and run `docker compose pull`.
- Data persists in the `duckrun-pgdata` volume. `docker compose down` keeps it; `docker compose down -v` deletes it.
- **Bring your own database** by removing the `postgres` service and pointing `DuckRun__Db__ConnectionString` at your server. Providers: `Postgres`, `SqlServer` (CockroachDB uses `Postgres`).
- gRPC ingest (HTTP/2) works out of the box here — no extra configuration.
- Behind a reverse proxy / TLS terminator, keep WebSockets enabled so the live console (SignalR) works.
