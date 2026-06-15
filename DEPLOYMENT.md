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
2. **Root Directory:** `src/F1Dashboard.Api`  (Render auto-detects the `Dockerfile`).
3. **Environment variables:**
   | Key | Value |
   | --- | ----- |
   | `ConnectionStrings__F1Database` | the Npgsql string from step 1 |
   | `AllowImport` | `true` (temporary — lets you seed the DB once) |
   | `Cors__AllowedOrigins` | your Vercel URL (set after step 3; can edit later) |
4. Deploy. On boot the app runs `EnsureCreated()` and builds the schema automatically.
5. **Seed the data** (one time) — pull real F1 data into the fresh DB:
   ```
   curl -X POST https://<your-render-url>/api/import
   ```
   (defaults to seasons 2023–2026; or `?seasons=2024,2025`). Takes ~30–60s.
6. Set `AllowImport` back to `false` and redeploy (optional hardening).

Verify: `https://<your-render-url>/api/drivers` returns JSON.

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
