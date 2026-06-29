# Deployment

Live architecture:

```
Vercel (Angular frontend)  ──HTTPS──>  Render (.NET API)  ──>  Neon (Postgres)
```

Vercel can't host the .NET backend, so the API runs on Render (Docker) and the
database on Neon. Deploy in this order (backend first — the frontend needs its URL).

## 1. Database — Neon

1. Create a project at [neon.tech](https://neon.tech) and copy the connection string.
2. Neon gives a URI like `postgresql://user:pass@ep-xxx.region.aws.neon.tech/dbname?sslmode=require`.
   Convert it to the Npgsql keyword form the backend expects:

   ```
   Host=ep-xxx.region.aws.neon.tech;Database=dbname;Username=user;Password=pass;SSL Mode=Require;Trust Server Certificate=true
   ```

Keep this string for the next step.

## 2. Backend — Render

1. [render.com](https://render.com) → **New → Web Service** → connect this GitHub repo.
2. **Root Directory:** repository root (`.`).
3. **Dockerfile Path:** `Dockerfile.render` (this image includes `/app/tools` + Python deps for telemetry ingestion).
4. **Environment variables:**
   | Key | Value |
   | --- | ----- |
   | `ConnectionStrings__F1Database` | the Npgsql string from step 1 |
   | `AllowImport` | `true` (temporary — lets you seed the DB once) |
   | `Cors__AllowedOrigins` | your Vercel URL (set after step 3; can edit later) |
5. Deploy. On boot the app runs `EnsureCreated()` and builds the schema automatically.
6. **Seed the data** (one time) — pull real F1 data into the fresh DB:
   ```
   curl -X POST https://<your-render-url>/api/import
   ```
   (defaults to seasons 2023–2026; or `?seasons=2024,2025`). Takes ~30–60s.
7. (Optional) Seed lap telemetry — see [Seeding lap telemetry](#seeding-lap-telemetry) below.
8. Set `AllowImport` back to `false` and redeploy (optional hardening).

Verify: `https://<your-render-url>/api/drivers` returns JSON.

## Seeding lap telemetry

The **Lap Data** tab needs FastF1 telemetry in the `telemetry_*` tables. Those tables
are created automatically on startup; they just start empty (`/api/lap-data/seasons`
returns `[]`).

> ⚠️ **Run the ingest externally, not on the hosted API.** FastF1 loads a whole
> race's car + position data into memory (hundreds of MB per race), which OOM-kills
> small instances (e.g. Render's 512 MB free tier) and can take the API down. The
> hosted `POST /api/import/telemetry` endpoint is best-effort for local/large hosts;
> on small tiers seed Neon directly from any machine with enough RAM:

```bash
cd tools
python3 -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt

# Point the ingest at Neon. Parse these from ConnectionStrings__F1Database:
#   Host=... -> PGHOST,  Database=... -> PGDATABASE,
#   Username=... -> PGUSER,  Password=... -> PGPASSWORD
export PGHOST=ep-xxx.region.aws.neon.tech
export PGDATABASE=neondb
export PGUSER=neondb_owner
export PGPASSWORD=********
export PGSSLMODE=require

# One race (fast, low memory) — proves the pipeline end to end:
python ingest_telemetry.py --season 2026 --round 1 --all-drivers

# Or a whole season / range (sequential, one race at a time):
python ingest_all_telemetry.py --seasons 2026
```

Verify the seed over HTTP (no DB access needed), with `AllowImport=true`:

```bash
curl https://<your-render-url>/api/import/telemetry/status
#   -> {"ingestRunning":false,"seasons":[2026],"races":1,"drivers":20,"laps":...}
curl https://<your-render-url>/api/lap-data/seasons   # -> [2026]
```

## 3. Frontend — Vercel

1. In `src/F1Dashboard.Web/src/environments/environment.ts`, set `apiBaseUrl` to
   `https://<your-render-url>/api` and commit/push.
2. [vercel.com](https://vercel.com) → **Add New → Project** → import this repo.
3. **Root Directory:** `src/F1Dashboard.Web`  (the `vercel.json` handles the Angular
   build, the `dist/f1-dashboard.web/browser` output dir, and SPA routing).
4. Deploy, then copy the resulting Vercel URL.

## 4. Close the CORS loop

Back in Render, set `Cors__AllowedOrigins` to your Vercel URL (e.g.
`https://f1-dashboard.vercel.app`) and redeploy the backend. The browser will now be
allowed to call the API from the deployed frontend.

## Notes

- Free tiers sleep when idle — the first request after a while has a cold-start delay.
- To refresh the data later, flip `AllowImport=true`, re-run the `curl` POST, flip it back.
- Local dev is unchanged: `environment.development.ts` points at `http://localhost:5197/api`.
