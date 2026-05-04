# Cortex — Pilot setup runbook

## 1. Purpose

This document is the **pilot setup runbook** for **Cortex** (local or small-scale pilot validation). It explains how another developer or pilot engineer can bring the stack up, configure dependencies, and sanity-check core flows **without relying on tribal knowledge**.

It is **not** a full production / multi-tenant SaaS operations guide. Production deployments will add hardening, secret management, observability, and change control beyond this scope.

**Secrets:** Do **not** commit real API keys, passwords, or Auth0 client secrets. Prefer **environment variables**, **Azure (or host) app settings**, or **`dotnet user-secrets`** for local development (the backend project declares a **`UserSecretsId`** in `CortexBackend/Cortex.API.csproj`). The committed `CortexBackend/appsettings.json` illustrates **shape only** — replace values per environment.

---

## 2. Prerequisites

Likely required for a full pilot:

| Prerequisite | Purpose |
|--------------|---------|
| **.NET 8 SDK** | Build and run `CortexBackend` |
| **Node.js + npm** | Build and dev-serve `CortexFrontend` |
| **Docker Desktop** (optional but recommended) | Run `docker-compose.yml` (SQL + API + static frontend) |
| **SQL Server** | Application database (local instance or container) |
| **Auth0** tenant | Authentication; create **SPA** and **API** applications and an **API Identifier (audience)** |
| **OpenAI API key** (if testing AI) | Intake assist, triage, screenshot insight, repeat-issue AI, etc. (`OpenAI` section in `CortexBackend/appsettings.json`) |
| **Optional: Azure SignalR** | Multi-instance / hosted realtime; omit for single-process local realtime (`CortexBackend/Program.cs`) |

Repository layout (high level):

- **`CortexBackend/`** — ASP.NET Core API (`Cortex.API.csproj`)
- **`CortexFrontend/`** — Vite + React app
- **`docker-compose.yml`** — local stack orchestration

Related narrative demo (not install steps): **`README.demojam.md`**. Product overview: **`README.md`**.

---

## 3. Local Docker setup

**File:** root **`docker-compose.yml`**

The compose file defines three services:

| Service | Image / build | Host ports | Notes |
|---------|----------------|------------|-------|
| **`cortex-sql`** | `mcr.microsoft.com/mssql/server:2022-latest` | **1433→1433** | SQL Server Developer. Data volume: **`cortex_sql_data`**. SA password from env (see below). |
| **`cortex-api`** | Build `CortexBackend/Dockerfile` | **5214→8080** | API listens on container **8080**; mapped to host **5214**. |
| **`cortex-frontend`** | Build `CortexFrontend/Dockerfile` | **4173→80** | Nginx serves the built SPA; mapped to host **4173**. |

**Depends_on:** Frontend depends on API; API depends on SQL.

**Connection string (API container):**

- Environment variable **`ConnectionStrings__CortexDb`** (double underscore nests to configuration) targets the **`cortex-sql`** host name inside the compose network, e.g. `Server=cortex-sql;Database=CortexDb;User Id=sa;Password=…;TrustServerCertificate=True;Encrypt=False;`

**Password substitution:**

- Compose uses **`${CORTEX_SQL_PASSWORD:-ChangeMe_OnlyForLocalDev_123!}`** for SQL — override **`CORTEX_SQL_PASSWORD`** in your shell or **`.env`** for anything beyond local throwaway dev.

**Frontend API URL at build time:**

- **`CortexFrontend/Dockerfile`** defines **`ARG VITE_API_URL=http://localhost:5214/api`** and **`ENV VITE_API_URL=$VITE_API_URL`** before **`npm run build`**. Compose passes **`args: VITE_API_URL: http://localhost:5214/api`** so the browser calls the mapped API on the host. If you expose the UI/API on different hosts or HTTPS, rebuild the frontend with the **`VITE_API_URL`** matching what the browser will use.

**Manual alternative:** Run SQL locally, **`dotnet run`** the API from `CortexBackend`, and **`npm run dev`** (`VITE_API_URL` in `.env` for Vite — see §5).

---

## 4. Database and migrations

**Automatic migrations on startup**

- **`CortexBackend/Program.cs`** runs **`await db.Database.MigrateAsync()`** during startup **inside a scoped service block**.  
- If migrations **fail** (bad connection string, unreachable SQL, schema issues), startup **logs and throws** — the API **will not** continue with a mismatched schema.

**Connection string required at build time**

- **`Program.cs`** resolves the SQL connection via **`DatabaseConnectionConfiguration.ResolveFirstNonEmpty(...)`**. If unset, startup throws (**message references** **`ConnectionStrings:CortexDb`** / Azure env naming).

**Default boards / baseline config**

- After migrations, **`ITicketBoardService.EnsureDefaultsAsync()`** runs in a **separate** try/catch (**`Program.cs`**). Failures are **logged**; unlike migrations, this path does **not** currently fail the entire process — **verify logs** if boards look missing.

**Optional demo eligibility bootstrap**

- **`DemoEligibilityBootstrapService`** runs when **`Development`** environment **OR** **`Demo:EnableEligibilityBootstrap`** is true (**`Program.cs`**). Intended for demo owner-eligibility scenarios; **disabled** unless those conditions apply.

**Manual migrations (CLI)**

- Normally unnecessary because the app migrates on boot. To run **`dotnet ef database update`** manually, use the **`CortexBackend`** project from a machine that has the **`ConnectionStrings:CortexDb`** (or **`__`**) pointing at the target database. Exact EF CLI prerequisites (startup project, tooling) follow standard EF Core conventions; verify your local **`dotnet ef`** toolchain if commands fail (**TODO**: add a pinned example if your repo adopts a **`dotnet-tools.json`**).

**Health checks**

- **`GET /health`** (live) and **`GET /health/ready`** (includes DB readiness) are mapped in **`Program.cs`** — use them after startup to confirm connectivity.

---

## 5. Required configuration

Sources: **`CortexBackend/appsettings.json`**, **`CortexBackend/Program.cs`**.

Environment variables commonly use **`__`** (double underscore) to represent nested JSON keys. A root **`.env.example`** lists placeholder-only names for Compose SQL password, common backend overrides, and **`VITE_API_URL`** — copy to **`.env`** locally (never commit secrets).

| Configuration key | Meaning | Notes |
|--------------------|---------|--------|
| **`ConnectionStrings:CortexDb`** / **`ConnectionStrings__CortexDb`** | SQL Server connection | **Required.** Empty → startup failure (**`Program.cs`**). |
| **`Auth0:Domain`** / **`Auth0__Domain`** | Auth0 tenant domain | Passed to JWT bearer **Authority**. |
| **`Auth0:Audience`** / **`Auth0__Audience`** | API Identifier (audience) | Must match tokens requested by the SPA (see frontend). Example placeholder shape in **`appsettings.json`**: **`https://cortex-api`** — **your** tenant will use **your** API identifier. |
| **`Auth0:ClientId`** | Used by Swagger OAuth (dev) | **Program.cs** wires Swagger **`OAuthClientId`**. Not a substitute for SPA client configuration in Auth0. |
| **`Auth0:ManagementClientId`**, **`Auth0:ManagementClientSecret`**, **`Auth0:ManagementApiAudience`** | Auth0 Management API | **Non-development:** **`ManagementClientSecret` required** or startup throws (**`Program.cs`**). **Development:** warning if missing — admin/sync features degrade. |
| **`Auth0:EnableUserAccessSync`** | Directory sync toggle | Defaults **`false`** in **`appsettings.json`**. |
| **`AllowedOrigins`** / **`AllowedOrigins__0`** … | CORS | **Non-development:** if **unset**, startup **throws** (**`Program.cs`**). **Development:** defaults allow **`http://localhost:5173`** and **`http://localhost:4173`**. Configure production origins explicitly. |
| **`OpenAI:ApiKey`** / **`OpenAI__ApiKey`** | OpenAI API | Empty → AI-backed features unavailable or degraded per feature handlers (intake assist, triage, etc.). |
| **`OpenAI:Model`**, **`OpenAI:EmbeddingModel`** | Model names | **`appsettings.json`** lists defaults (**e.g. `gpt-4o-mini`, `text-embedding-3-small`**). |
| **`Azure:SignalR:ConnectionString`** or **`ConnectionStrings:AzureSignalR`** | Azure SignalR Service | Optional. If absent, **`Program.cs`** uses **in-process SignalR** (single-instance style). |
| **`Demo:EnableEligibilityBootstrap`** | Demo bootstrap | Mirrors **`Program.cs`** gated block (**see §4**). |

**Frontend:**

- **`VITE_API_URL`** — base URL for REST + hub path composition (**see `CortexFrontend/src/services/api.ts`**, **`import.meta.env.VITE_API_URL`**). Must match where the browser can reach the API (include **`/api`** suffix as used in codebase, e.g. **`http://localhost:5214/api`**).

**Audience alignment:**

- Frontend uses a constant **`https://cortex-api`** as Auth0 **`audience`** in several components (e.g. **`CortexFrontend/src/App.tsx`**). Your Auth0 API Identifier **must match** what you configure in **`Auth0:Audience`** and what the SPA requests — **otherwise tokens fail validation.**

---

## 6. Auth0 and access approval

**Authentication (Auth0)**

- JWT bearer middleware uses **`Auth0:Domain`** and **`Auth0:Audience`** (**`Program.cs`**).

**Local authorization (Cortex)**

- Policies such as **`ElevatedAccess`**, **`BusinessAccess`**, **`AdminOnly`** map Auth0 **`role`** claims — **`CortexBackend/Authorization/CortexAuthorizationExtensions.cs`**.
- Cortex stores **`Users`** rows ( **`CortexBackend/Models/Users.cs`** ) aligned with **`Auth0` `sub`**, email, **`Role`**, **`IsActive`**, **`Department`**, etc.

**`IAccessApprovalService` — who may use the app**

- Implemented by **`AccessApprovalService`** (**`CortexBackend/Services/AccessApprovalService.cs`**): after identity is known, Cortex evaluates **`AccessApprovalDecision`**. Factors include **unknown user**, **inactive/expired**, and a **documented verified demo carve-out** (**`demo@cortex.com`** with **`email_verified`**).
- **Unknown / disallowed identities** yield **`AccessNotApprovedException`** from **`UserContextService`** (**`UserContextService.cs`**) paths — the **React app** treats this as **`isAccessNotApprovedError`** and shows **“Access not approved”** (**`CortexFrontend/src/App.tsx`** pattern).

**First elevated user / bootstrap**

- **`UserContextService`** promotes the **first** authenticated user scenario when **no active elevated-role users exist in the DB**: role may be forced to **`Admin`**, **`IsActive` true**, and default **`Department`** for developer bootstrap (**logged as `[BOOTSTRAP]` / `[AUTH0-LINK]`**). Subsequent users may arrive as **`User`** with **`IsActive`** depending on bootstrap path (**read logs / code paths**).

**Separate from ticket approval**

- **App access**: Auth0 login + Cortex **user gate** (**§ above**).
- **Ticket approval workflow**: **`PendingApproval`** / reviewer actions on **tickets** (approval queue UX) — different domain (“**approve this request**,” not “**approve Cortex account**”).

Pilot guidance: **Pre-provision** approved users (**active**, correct **role**) OR enable/document **`Auth0:EnableUserAccessSync`** and Management API secrets so directory automation matches your governance.

---

## 7. First-run checklist

Use as a sequencing guide (adapt to Docker vs bare-metal):

- [ ] **Start SQL** (Docker **`cortex-sql`** or install)
- [ ] Set **`ConnectionStrings__CortexDb`** (compose env or `.env`/user secrets)
- [ ] **Configure Auth0**: SPA application, API (identifier = audience), roles, callbacks
- [ ] Align **`Auth0:Audience`** with SPA token **`audience`** (**see §5**)
- [ ] Configure **`AllowedOrigins`** when not `"Development"` (**`Program.cs`**)
- [ ] Optionally set **`OpenAI__ApiKey`** for AI features
- [ ] Optionally set **Azure SignalR** connection string for hosted realtime
- [ ] **`dotnet run`** / compose **up** the **API** — confirm **`/health/ready`**
- [ ] **Run frontend** with correct **`VITE_API_URL`**
- [ ] Log in — confirm **bootstrap admin** vs **pending** user outcomes match expectations
- [ ] In **Users/admin UI**: activate users, departments, roles (**as your process requires**)
- [ ] Confirm **boards/statuses**: defaults from **`EnsureDefaultsAsync`** (or Configure if absent)
- [ ] Create **routing rules** (Configuration) and eligible owners (**Syniti/Business eligibility** flags per user policy)
- [ ] **Create ticket** → **approval queue** → **approve**
- [ ] Open **Reports** (SLA/metrics/intake learning) — expect **empty-but-honest** states on fresh DB
- [ ] **Rule health**, **Intake Learning**, **Rebalance** — availability depends on **`ElevatedAccess` / role** and seeded activity

After completing setup, run **[docs/fresh-install-validation-checklist.md](fresh-install-validation-checklist.md)** to validate the environment, approval workflow, Cortex rail, reports, and empty states.

---

## 8. Feature smoke test

A concise scripted pass (adapt data to tenant):

1. **Create** a deliberately thin ticket  
2. **Intake Assist** (“Improve Request”) — requires **OpenAI** if testing full flow  
3. Open **Approval Queue** — reviewer triage readiness  
4. In **Ticket modal**, skim **Decision / Risk / Intake / Evidence / History** (Cortex rail)  
5. **Approve** (or return/reject paths as demo script requires) — confirm routing owners  
6. Attach a **PNG/JPEG/WebP** screenshot — run **screenshot insight** if AI enabled  
7. **Configuration → Ticket routing → Rule Health** (**GET** rules surface exposes health per repo tier)  
8. **Reports → Intake Learning** (`/api/reports/intake-learning` backend) — may be empty cohort  
9. **Rebalance Overview** (`/api/rebalance/...`) — needs workload/decision prerequisites  

---

## 9. Common failure modes

| Symptom | Likely cause | Direction |
|---------|---------------|-----------|
| API **won’t start** after SQL up | **`ConnectionStrings:CortexDb`** wrong / SQL not reachable | Verify connection string, firewall, **`TrustServerCertificate`**, **`Encrypt`** flags for pilot SQL |
| **Migration error** | Schema drift manually applied | Prefer letting app **`Migrate`**; repair DB or restore snapshot |
| **Frontend CORS/network errors** | **`VITE_API_URL`** mismatches actual API URL | Align protocol/host/port **`/api`**, rebuild Docker image if bake-time |
| **401 / invalid_token** audience | SPA audience ≠ **`Auth0:Audience`** | Fix Auth0 API identifier & SPA config |
| **“Access not approved”** (`App.tsx`) | User **inactive**/unknown per **`AccessApprovalService`** | Approve **`Users`** in DB/app; pre-provision test accounts |
| **Auth0 Management features fail** | Missing **`ManagementClientSecret`** (**non-dev fails hard**) | Set **`Auth0__ManagementClientSecret`** |
| AI features **empty/unavailable** | **`OpenAI:ApiKey`** empty | Set key (**user-secrets**/env); accept bounded behavior otherwise |
| **Realtime flaky** multi-instance | In-process SignalR insufficient | **`Azure__SignalR__ConnectionString`** per **`Program.cs`** |
| Routing **no suggestion / cold-start** panels | No **routing rules** or **no eligible owners** | Add rules per **`TicketRouting*`** UX; flag owner eligibility |

---

## 10. Pilot data expectations

A compelling demo depends on **data volume and variety**:

- Approved / returned / rejected **tickets**
- Persisted **routing decisions** / **overrides**
- Resolved **similar tickets** / embeddings pipeline for Cortex insight richness
- **Attachments** (**screenshots**) for insight
- **`TicketOutcome`** rows for analytics (rule health / intake learning / learning overlays)
- **Users** with **departments**, **roles**, and **owner eligibility** where routing relies on those attributes

**Fresh database:** Lists, aggregates, insight panels, Reports, Rule Health counts, Intake Learning, Rebalance narratives may legitimately **empty**. **That is normal** — not necessarily “broken.” For pilot messaging, **`docs/tier-11-intake-learning-aggregate-spec.md`** spells correlation limits on intake KPIs.

---

## 11. Pilot readiness checklist

- [ ] **Backend starts** cleanly (no migration exception); **`/health/ready`** succeeds  
- [ ] **Frontend** loads (**Vite dev** or **nginx** compose) against correct **`VITE_API_URL`**  
- [ ] **Auth0 login** returns tokens validated by **`Auth0:Audience`**  
- [ ] Admin / reviewer **roles** can reach **elevated surfaces** (**routing**, **rebalance** policy per deployment)  
- [ ] Pilot **test users** created + **active** (**not trapped** behind **access approval**)  
- [ ] **Routing rules** + **eligible owners** present for scripted scenarios  
- [ ] **`OpenAI`** configured **if demoing AI** assist/triage/evidence/repeat-issue AI  
- [ ] **Approval workflow** exercised end-to-end  
- [ ] Cortex **rail** exercised on at least one rich ticket  
- [ ] **Rule Health** renders (may be sparse)  
- [ ] **Intake Learning** report loads (possibly empty cohort)  
- [ ] **Rebalance** exercised if workload seed exists  
- [ ] No **secrets committed** (`git status` clean of keys; use env / Key Vault patterns in prod)  

---

## Evidence gaps / TODOs (repo)

| Item | Note |
|------|------|
| **`appsettings.Development.json`** | Not present in repository snapshot inspected for this runbook — local dev often uses **`dotnet user-secrets`** (**`UserSecretsId`** on project) instead. |
| **Manual EF commands** | Add exact **`dotnet ef database update`** one-liner if your CI pins `dotnet-tools.json`. |

---

## References (paths)

- **`docker-compose.yml`**, **`CortexBackend/Dockerfile`**, **`CortexFrontend/Dockerfile`**
- **`CortexBackend/appsettings.json`**, **`CortexBackend/Program.cs`**
- **`CortexBackend/Authorization/CortexAuthorizationExtensions.cs`**
- **`CortexBackend/Services/UserContextService.cs`**, **`CortexBackend/Services/AccessApprovalService.cs`**
- **`README.md`**, **`README.demojam.md`**, **`docs/tier-11-intake-learning-aggregate-spec.md`**

---

*Pilot setup runbook — documentation only (Tier 12 Task 1).*
