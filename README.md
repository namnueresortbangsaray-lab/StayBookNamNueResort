# Stay Book · น้ำเหนือรีสอร์ท บางเสร่

Staff web app for the resort's guest logbook: room board, bookings, check-in/out with room inspection, guest history and
lifetime stats, pricing, attachments, analytics, CSV export, roles and an audit trail. The UI is Thai.

Built with ASP.NET Core MVC (.NET 10), EF Core with SQLite, ASP.NET Core Identity,
Razor, Tailwind CSS and vanilla JavaScript, packaged for Docker.

คู่มือผู้ใช้ภาษาไทย: [Docs/USAGE_TH.md](Docs/USAGE_TH.md)

## Quick start

Requirements: Docker Desktop or Docker Engine with Compose v2.24+.

```bash
cp .env.example .env
docker compose up -d --build
```

Open <http://localhost:8080> and sign in with the seeded demo account:

- Email: `admin@staybook.local`
- Password: `ChangeMe123!`

> **Change the demo password.** Set `DEMO_ADMIN_EMAIL`/`DEMO_ADMIN_PASSWORD` in `.env` before the first start, or create
> a real admin with `make user-create` and deactivate the demo account. The seeder only creates the admin when it is missing.

## Configuration (`.env`)

| Variable | Default | Purpose |
|---|---|---|
| `APP_PORT` | `8080` | Host port |
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables Swagger UI at `/swagger` and detailed logs |
| `App__Timezone` | `Asia/Bangkok` | Display timezone ("today", default report month); data is stored in UTC |
| `Database__AutoMigrate` | `true` | Apply EF Core migrations at startup |
| `Database__Seed` | `true` | Create the demo admin and the five rooms when missing |
| `ConnectionStrings__Sqlite` | `Data Source=/app/storage/database/staybook.db` | SQLite file (in the `staybook_database` volume) |
| `DEMO_ADMIN_EMAIL`, `DEMO_ADMIN_PASSWORD` | demo values | Seeded administrator |
| `Storage__Provider` | `Local` | File storage (`storage/uploads`, never public) |
| `UPLOAD_MAX_BYTES` | `5242880` | Attachment limit (JPEG, PNG, WebP, PDF) |
| `IdPhotos__MaxBytes` | `2097152` | ID card photo limit (JPEG, PNG, WebP; the browser shrinks photos to about 30–300 KB first) |
| `IdPhotos__RetentionDays` | `90` | ID card photos are deleted this many days after check-out (checked at start-up and daily) |
| `SESSION_LIFETIME` | `120` | Sign-in lifetime in minutes (sliding) |
| `Auth__PasswordMinLength` | `10` | Minimum password length (no symbol or case rules) |
| `Auth__SessionCheckSeconds` | `60` | How soon a password, role or status change signs out the user's other sessions |
| `BACKUP_KEEP` | `7` | SQLite backups kept by `db:backup` |

## Make commands

| Command | What it does |
|---|---|
| `make up` / `make down` / `make logs` | Start (build), stop, follow logs |
| `make prod-up` | Production stack with Nginx in front |
| `make dev` | `dotnet watch` + Tailwind watcher on <http://localhost:8082> |
| `make migrate` / `make seed` | Apply migrations / seed |
| `make user-create NAME_ARG="ชื่อ" EMAIL=a@b.c PASSWORD=... ROLE=front_desk` | Create a staff account (`admin`, `front_desk`, `housekeeping`) |
| `make backup` | SQLite online backup into the `staybook_backups` volume |
| `make import-php` | Import an existing v1 installation (see below) |
| `make test` / `make format-check` / `make smoke` | Tests, formatting, end-to-end smoke test |
| `make migrations-add NAME=AddX` | Create a migration in `Data/Migrations` |

Inside the container the CLI is `dotnet StayBook.dll <command>`:
`migrate`, `seed`, `user:create`, `db:backup`,
`import:php --sqlite=<file> [--uploads=<dir>] [--truncate] [--dry-run]`.

## Development without Docker

Requirements: .NET 10 SDK, Node 22.

```bash
npm ci
npm run build      # or run dev for watchers
dotnet run --project StayBook.csproj   # http://localhost:8080, Development, seeds automatically
```

The SQLite file and uploads go to `storage/` under the working directory (git-ignored).

The code is one project at the repository root; see the layout in [Docs/ARCHITECTURE.md](Docs/ARCHITECTURE.md). Pass
`StayBook.sln` to `dotnet build`, `test` and `format` (the root holds both the solution and the project).

### EF Core migrations

Migrations live in `Data/Migrations` (`dotnet tool restore` installs `dotnet-ef`):

```bash
dotnet ef migrations add <Name> --project StayBook.csproj --output-dir Data/Migrations
```

The app applies pending migrations on startup (`Database__AutoMigrate=true`) or with `make migrate`. A test fails if the
model has changes without a migration.

## Tests

```bash
dotnet test StayBook.sln
```

Runs unit tests, data tests on SQLite, `WebApplicationFactory` integration tests, and a contract test that replays the
recorded HTTP behaviour of v1 (`tests/baseline/`, needs Node).

`tests/smoke/smoke-test.sh` (curl + jq) checks a running instance end to end.

## Backup and restore

- The `backup` service makes one at start and then every 24 hours (`BACKUP_INTERVAL_SECONDS`) while the stack runs;
  `make backup` makes one now. Both write `stay-book-YYYYmmdd-HHMMSS.sqlite` into the `staybook_backups` volume, which keeps
  the newest `BACKUP_KEEP` files (default 7).
- Copy backups off this machine regularly, for example:
  `docker run --rm -v stay-book_staybook_backups:/b:ro -v "$PWD/offsite:/out" alpine cp -r /b/. /out/`
- Back up the `staybook_uploads` volume together with the database; files and rows belong together.
- Restore: stop the stack, copy a backup over `/app/storage/database/staybook.db` in the `staybook_database` volume
  (delete `staybook.db-wal`/`-shm`), start again.
- `staybook_keys` holds the Data Protection keys; losing it only signs everyone out.

## Upgrading an existing v1 installation

The Compose project (`stay-book`), port and volume names are unchanged, so the old database (`stay_book.sqlite`) and
uploads are already mounted. The new app uses its own file (`staybook.db`) and never writes to the old one.

1. Back up the old database, e.g.
   `docker run --rm -v stay-book_staybook_database:/db:ro -v stay-book_staybook_backups:/b busybox cp /db/stay_book.sqlite /b/`
2. Stop the old containers, regenerate `.env` from `.env.example` (carry over your admin and limit values),
   then `docker compose up -d --build`.
3. `make import-php` — copies every row (ids, bcrypt password hashes, timestamps) and checks the uploaded files; it is
   idempotent and `--dry-run` shows the counts first. `--truncate` replaces the freshly seeded demo data.
4. Sign in: existing passwords keep working and are re-hashed to the Identity format on first sign-in.
   Old sessions cannot carry over, so everyone signs in once. The unused `staybook_sessions` volume can be removed.

## User interface

A flat blue-and-white interface with Light, Dark and System themes, chosen from the sun/moon menu in the top bar, the
phone menu or the sign-in page. It works on phones (menu button, bottom tabs, card lists, full-screen forms), iPads
(icon rail, key table columns, two-column layouts, portrait, landscape and split view) and desktops (full sidebar and
tables). The ☰ button at the left of the top bar collapses the sidebar to icons and back. A comfortable/compact
density toggle sits next to the theme menu. Ctrl/⌘K opens search from anywhere.

- Tokens, breakpoints, components and dark mode: [DESIGN.md](DESIGN.md). Screenshots in both
  themes at 390, 768, 1024 and 1440px, with loading, error and print states: `Docs/design/after-skill/` (earlier:
  `Docs/design/before-skill/`, `Docs/design/before/`). UI review against the ui-ux-pro-max skill:
  [Docs/design/SKILL_AUDIT.md](Docs/design/SKILL_AUDIT.md).
- Fonts (Prompt) and Lucide icons are self-hosted; `npm run build` copies the fonts,
  builds the icon sprite and fails if any colour pair misses WCAG AA in either theme.
- UI checks against a running instance (`npm --prefix tests/ui ci && npx --prefix tests/ui playwright install chromium`):

```bash
BASE_URL=http://localhost:8080 node tests/ui/a11y.mjs        # axe-core in both themes + responsive sweep
BASE_URL=http://localhost:8080 node tests/ui/screenshots.mjs Docs/design/after-skill light,dark 390,768,1024,1440
```

`tests/ui/seed-demo.mjs` fills an empty development instance with fictional guests for screenshots.

## Production

See [Docs/DEPLOYMENT.md](Docs/DEPLOYMENT.md): `make prod-up` runs Nginx (gzip, security headers, upload limit, asset
caching, forwarded headers) in front of the non-root Kestrel container. For a hosted trial on Railway see [Docs/RAILWAY.md](Docs/RAILWAY.md).

## Troubleshooting

- **Container not healthy:** `docker compose logs web`. `storage-init` fixes volume ownership before each start.
- **Port in use:** set `APP_PORT` in `.env`.
- **Cannot sign in:** five failed attempts from one address block sign-in for 15 minutes; the same limit locks an account.
  An admin can lift the lock at once with *ผู้ใช้งาน → ตั้งรหัสผ่านใหม่*.
- **Staff accounts:** admins add, edit and deactivate accounts on the *ผู้ใช้งาน* page; everyone changes their own password
  from the sidebar (or the phone menu). `user:create` is only needed for the first admin.
- **Upload rejected (422):** only JPEG, PNG, WebP and PDF up to `UPLOAD_MAX_BYTES`, detected from the file content.
- **ID card photos:** attached in the booking form (camera on phones), visible to every signed-in staff member, and
  deleted automatically `IdPhotos__RetentionDays` days after check-out; each purge is in `audit_logs` (action `purge`).
- **Styles missing when running locally:** run `npm run build`.

## Documentation

[API](Docs/API.md) · [Architecture](Docs/ARCHITECTURE.md) · [Deployment](Docs/DEPLOYMENT.md) · [Railway](Docs/RAILWAY.md) ·
[Project knowledge](Docs/PROJECT_KNOWLEDGE.md) · [Migration plan and deviations](Docs/DOTNET_MIGRATION_PLAN.md) ·
[Design system](DESIGN.md) · [UI audit and assumptions](Docs/design/UI_AUDIT.md)
