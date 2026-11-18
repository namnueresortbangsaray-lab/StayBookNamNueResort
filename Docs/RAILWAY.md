# Running Stay Book on Railway

Railway builds `Dockerfile` from GitHub (settings in `railway.json`), runs one container and keeps SQLite,
uploads, ID card photos, logs and data-protection keys on one volume mounted at `/app/storage` (the app's default
paths all live there). Rehearsed locally on 2026-11-18 with a root-owned volume, a proxy header and a redeploy.

Use it for a trial with sample data. Do not put real guests on a public trial (names, phones and ID card photos are
personal data).

## 1. Create the service

1. Sign in at railway.com with GitHub and allow the Railway app to see `Stay-Book`.
2. New Project → Deploy from GitHub repo → `Stay-Book`. Railway reads `railway.json`: Dockerfile build, health check
   `/health`, restart on failure. The first build takes several minutes (Node, .NET SDK).

## 2. Add the volume

Service → right click → Attach volume (or Ctrl/⌘K → "volume"), mount path **`/app/storage`**. Without it every
deploy starts with an empty database.

## 3. Variables

Service → Variables → Raw editor:

```dotenv
RAILWAY_RUN_UID=0
PORT=8080
App__TrustForwardedHeaders=true
Database__Seed=true
DEMO_ADMIN_EMAIL=admin@staybook.local
DEMO_ADMIN_PASSWORD=<a long password, not ChangeMe123!>
```

- `RAILWAY_RUN_UID=0`: Railway volumes belong to root and the image runs as uid 1654; without it the app stops with
  `Access to the path '/app/storage/keys' is denied`.
- `PORT=8080`: the port Kestrel listens on, so Railway's proxy sends traffic there.
- `App__TrustForwardedHeaders=true`: Railway terminates HTTPS; the app then sees `https` and marks cookies `Secure`.
- `Database__Seed=true` creates the admin and the five sample rooms on an empty database only; it never overwrites.

Optional: `UPLOAD_MAX_BYTES`, `IdPhotos__RetentionDays`, `SESSION_LIFETIME` as in `.env.example`.

## 4. Open it

Settings → Networking → Generate Domain (target port 8080) → `https://<name>.up.railway.app`. Sign in with the admin
above, change its password, and add trial accounts (front desk / housekeeping) on the ผู้ใช้งาน page.

Pushes to `main` redeploy automatically; the volume keeps the data.

## Notes

- One instance only: SQLite and a Railway volume do not support replicas.
- The `backup` service of `docker-compose.yml` does not run here; use Railway's volume backups or download the database
  (`/app/storage/database/staybook.db`) from time to time.
- To start the trial over: delete and re-create the volume (or the service).
