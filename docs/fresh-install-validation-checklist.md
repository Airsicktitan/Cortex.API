# Cortex — Fresh install validation checklist

## 1. Purpose

Use this checklist to **validate** that Cortex can be brought up **from a clean clone and a fresh database** for **local engineering** or **pilot** readiness — not as a production deployment runbook.

- It confirms **behavior** matches expectations after **`docs/pilot-setup.md`**-style setup.
- It assumes **placeholder or local-only credentials** (see **`.env.example`**); never paste real secrets into tickets or commits.
- Run it **before demos**, **before pilot handoffs**, or **before tagging a release candidate** when you need confidence the stack works end-to-end.


**Relationship to other docs:** Deep setup narrative lives in **`docs/pilot-setup.md`**. Env placeholder names live in **`.env.example`**. Surface-level orientation is linked from **`README.md`**. Use this checklist when proving behavior on a **fresh** database and clean install, beyond verifying configuration keys alone.
---

## 2. Validation modes

Pick **one** primary path per run; both should reach the same functional outcome if configuration is aligned.

### Mode A — Docker SQL + local apps

- **Why:** Faster iteration on API/UI against a persistent SQL container; matches many developers’ workflows.
- **Typical steps (high level):**
  - Start **only** the **`cortex-sql`** service from the root **`docker-compose.yml`** (SQL on host **1433**, volume **`cortex_sql_data`** — see **`docker-compose.yml`**).
  - Configure **`ConnectionStrings__CortexDb`** for **`Server=localhost,1433;…`** using the same **`CORTEX_SQL_PASSWORD`** substitution pattern as Compose ( **`${CORTEX_SQL_PASSWORD:-…}`** in Compose; your **`.env`** / shell supplies **`CORTEX_SQL_PASSWORD`**).
  - Run the API locally: from repo root **`dotnet run --project CortexBackend/Cortex.API.csproj`** (or **`cd CortexBackend`** then **`dotnet run`** ).
  - Run the SPA with Vite: **`cd CortexFrontend`**, **`npm ci`** once, then **`npm run dev`** (default dev URL uses **5173**; **`Program.cs`** default CORS origins include **`http://localhost:5173`** and **`http://localhost:4173`**).
  - Set **`VITE_API_URL=http://localhost:5214/api`** (or wherever **`dotnet`** exposes the API — **`launchSettings.json`** **`http`** profile uses **`http://localhost:5214`**) via **`.env`** in **`CortexFrontend`** or consistent with **`import.meta.env`**.

### Mode B — Full **docker-compose** stack

- **Why:** One command brings up SQL + API + nginx-hosted SPA per **`docker-compose.yml`**.
- **Services:** **`cortex-sql`** (1433), **`cortex-api`** (**5214→8080**), **`cortex-frontend`** (**4173→80**). Build args **`VITE_API_URL: http://localhost:5214/api`** match **`CortexFrontend/Dockerfile`** **`ARG`**.
- **Observation:** Compose sets **`ASPNETCORE_ENVIRONMENT=Development`** for **`cortex-api`** in **`docker-compose.yml`**. Frontend is **baked** at image build time with **`VITE_API_URL`** — change URL ⇒ rebuild **`cortex-frontend`**.

### Which mode is “best supported”?

Both are **first-class in repo artifacts** (Compose wires all three images; **`Program.cs`** and **`launchSettings.json`** clearly support **`dotnet run`**). Choose **Mode A** when iterating on code frequently; choose **Mode B** for closest-to-integration smoke without local Node/dotnet for SPA/API.

---

## 3. Preflight checklist

- [ ] **Docker Desktop** running (if using Docker SQL or Compose).
- [ ] **.NET 8 SDK** installed ( **`CortexBackend/Cortex.API.csproj`** targets .NET **8** per repository layout).
- [ ] **Node.js + npm** installed ( **`CortexFrontend/package.json`**).
- [ ] Repo **cloned**.
- [ ] **`.env`** copied from **`.env.example`** at repo root and adjusted (**never commit secrets** — **`.gitignore`** ignores **`.env`** but allows **`.env.example`**).
- [ ] **Auth0:** test **tenant**, SPA app, API (identifier **=** **`Auth0__Audience`**), callbacks, **`audience`** alignment — frontend **`App.tsx`** uses constant **`API_AUDIENCE = "https://cortex-api"`**; **`Auth0:Audience`** must match (**`docs/pilot-setup.md`** §5).
- [ ] **OpenAI** API key **if** validating AI/evidence slices (**`OpenAI` section**, **`appsettings.json`** shape).
- **Ports free:** SQL **1433** (Docker map), API **5214** ( **`launchSettings.json`** / Compose), Vite **5173** (default unless overridden), Compose UI **4173** ( **`docker-compose.yml`**).
- [ ] **`git status`**: **no `.env`** or secrets staged; **`appsettings`** not overridden with real keys.

---

## 4. Fresh database setup

### Create / reset database data

**Full Compose stack**

- Prefer **`docker compose down -v`** from repo root to remove the Compose-managed **`cortex_sql_data`** volume (**`docker-compose.yml`** **`volumes:`**), giving a truly empty persistence layer on next **`docker compose up`**. **`TODO`:** Confirm exact Compose project naming if **`down -v`** does not remove expected volumes — **`docker volume ls`** and Compose docs.

**Docker SQL only (Mode A)**

- Same **`cortex_sql_data`** volume semantics; reset by removing the volume while containers are stopped, or **`docker compose down -v`** for the SQL-only project.

**Local SQL Server (outside Compose)**

- Create an empty **`CortexDb`** (or your chosen name) and point **`ConnectionStrings__CortexDb`** at it.

### Connection string

- **`Program.cs`** throws at startup if **`DatabaseConnectionConfiguration.ResolveFirstNonEmpty`** returns empty (**message references** **`ConnectionStrings:CortexDb`** / Azure-style **`ConnectionStrings__CortexDb`**).
- Compose injects **`ConnectionStrings__CortexDb`** with **`Password=${CORTEX_SQL_PASSWORD:-ChangeMe_OnlyForLocalDev_123!}`** for **`cortex-sql`** hostname.

### Migrations on startup

- **`await db.Database.MigrateAsync()`** runs inside a **`using`** scope (**`Program.cs`**). **Success:** log **"Database migrations applied successfully."** — **failure:** logged and **rethrow** (process **does not** start with mismatched/unreachable schema).

### Default boards / baseline

- **`ITicketBoardService.EnsureDefaultsAsync()`** runs in a **separate** **`try/catch`** — failures are **logged**; startup **continues** (unlike migrations). **Investigate logs** if boards look wrong (**`docs/pilot-setup.md`** §4).

### Logs

- **Console:** **`dotnet`** or **`docker compose logs cortex-api`** (service name **`cortex-api`**).
- Structured request logging middleware: **`Program.cs`** **`app.UseStructuredRequestLogging()`** (after startup).

---

## 5. Backend startup validation

- [ ] Process starts (**`dotnet`** exit code **0**, or Container **Running**).
- [ ] Logs show **successful migrations** (**`MigrateAsync`** block).
- [ ] Logs show **EnsureDefaults** attempted; if errors, investigate (**`EnsureDefaultsAsync`** swallow path).
- [ ] **`GET /health`** — **live** (**`Predicate`** tag **`live`** — **`Program.cs`** ).
- [ ] **`GET /health/ready`** — **ready** includes **DbContext** check (**tags** **`ready`**).
- [ ] No unhandled fatal exception past middleware (**`GlobalExceptionHandler`** registered).
- [ ] **CORS:** **`Development`** with unset **`AllowedOrigins`** uses **`localhost:5173`** and **`localhost:4173`** (**`Program.cs`**). Non-**Development**: **`AllowedOrigins__0`** must be configured or startup throws.
- [ ] **Auth0:** Swagger UI **Development/Staging** only; JWT **Authority/Audience** from **`Auth0:Domain`** / **`Auth0:Audience`**.
- [ ] **`Auth0__ManagementClientSecret`:** **`Development`** → **warning** if missing (**`Program.cs`**); **non-Development** → **startup fails** unless set.
- [ ] **OpenAI:** Missing key ⇒ AI-assisted features degraded/unavailable (**handlers** vary); verify expected bounded behavior (**`docs/pilot-setup.md`**).

---

## 6. Frontend startup validation

- [ ] **`npm run dev`** (Mode A) or open **4173** (Mode B) — app shell loads (**`package.json`** **`dev`** ⇒ **Vite**).
- [ ] **`import.meta.env.VITE_API_URL`** services use same base URL as reachable API (**`CortexFrontend/src/services/api.ts`** pattern).
- [ ] Browser **login** redirects to Auth0 and returns without blank screen (**`App.tsx`** + **`@auth0/auth0-react`**).
- [ ] API calls succeed (network tab → **`/api/...`** on **5214** or mapped host).
- [ ] No CORS errors (**origin** **`5173`**/**`4173`** vs **`Program.cs`** policy).
- [ ] Not stuck on **`isAccessNotApprovedError`** when user should have access (**`App.tsx`** / **`services/api`**).

---

## 7. First admin / user access validation

Refer to **`UserContextService`** first-elevated-user bootstrap and **`AccessApprovalService`** rules.

**App access approval (who may use Cortex)** vs **ticket approval workflow (routing queue)**

- **App access:** After Auth0 identity, Cortex evaluates **`IAccessApprovalService`** (**`AccessApprovalService`**): unknown user ⇒ denied unless **`demo@cortex.com`**-style verified demo path (**`AccessApprovalService`** constants); inactive/expired denied (**`Evaluate`**).
- **Ticket approval:** Separate domain — **`PendingApproval`** tickets, reviewers (**Approval Queue**), **§9** workflows.

Checklist:

- [ ] Login as pilot admin on **fresh DB** — **`UserContextService`**: **first** authenticated user path when **no active elevated role users**: may promote **`Admin`**, **`IsActive`**, default **department** (**`UserDepartmentPolicy.DefaultDeveloperDepartment`**) (**see logs** **`[BOOTSTRAP]`**).
- [ ] Approved path: SPA loads **`Users`**, dashboards as role allows.
- [ ] **`AccessApprovalService`**: **`DeniedUnknownUser`** for unknown identities; inactive users **`DeniedInactive`** (**`ExpiryDate`** in past denies).
- [ ] **`Users`/admin UX:** Roles, departments (**as product allows**) — prerequisite for routing when ownership uses **department**/eligibility (**`docs/pilot-setup.md`** checklist).
- [ ] Departments/eligibility populated for routing scenarios (see **`docs/pilot-setup.md`** §10 *Pilot data expectations*).

---

## 8. Configuration validation

Adapt to your seeded data (**empty is OK** for many aggregates — **`docs/tier-11-intake-learning-aggregate-spec.md`** cited from pilot doc for KPI limits).

- [ ] **Boards** exist (**`EnsureDefaultsAsync`** **or** manual configuration UX).
- [ ] **Statuses/priorities/SLA** — verify **Configuration / SLA / status** UX as installed.
- [ ] Users exist for **Syniti** vs **Business** owner scenarios (**eligibility/routing**) — pilot may seed manually.
- [ ] At least one **routing rule** can resolve **Syniti + Business** owners when script requires (**`TicketRouting*`** UX).
- [ ] **Configuration → Ticket routing → Rule Health** opens (backend **`IRoutingRuleHealthService`** per API wiring).
- [ ] Panels handle **sparse** outcomes gracefully.

---

## 9. Core workflow smoke test

| # | Step | Pass criteria |
|---|------|----------------|
| 1 | Create deliberately minimal ticket | Ticket row created |
| 2 | **Intake assist** (requires **OpenAI** if testing full AI path) | Request completes or degrades cleanly |
| 3 | Submit to **approval pipeline** per UI | Moves toward **`PendingApproval`** as designed |
| 4 | Open **Approval Queue** | Ticket appears for reviewer role |
| 5 | Open **Ticket Modal** | Details load |
| 6 | **Cortex rail** tabs (**Decision**, **Risk**, **Intake**, **Evidence**, **History**) | Render (**empty**/sparse OK on fresh DB) |
| 7 | **Approve** | Submission succeeds |
| 8 | Modal behavior | Closes per UX |
| 9 | Queue | Ticket clears from approval queue when status advances |
| 10 | **Owners** | Syniti/Business (or scripted) assignments appear per routing |
| 11 | **Board/list** | Lists refresh / reflect new state |

---

## 10. Return / reject workflow validation

Create **two** additional tickets beyond §9.

### Return for detail

- Return / request-more-info outcome (**status family **`NeedsMoreInfo`** in UI enums — **`ApprovalOutcomeMessage.tsx`**, **`TicketCard.tsx`**).
- [ ] Reason captured — modal closes — leaves approval queue appropriately.
- [ ] **`NeedsMoreInfo`** (or UX string **“More information requested”**) visible on card when applicable.
- [ ] **`TODO`:** Confirm **exact** revise/resubmit UX for your narrative (product-specific).

### Reject

- [ ] **Reject** path with reason (**`Rejected`** statuses in UI enums).
- [ ] Leaves queue — **Rejected** surfaced on board/list as implemented.

---

## 11. AI / evidence validation

Skip if **`OpenAI__ApiKey`** unset; AI paths should **not crash** API.

If key configured:

- [ ] Attach **PNG/JPEG/WebP** (product-supported).
- [ ] Trigger **screenshot insight** (**`IScreenshotInsightAiService`** pipeline).
- [ ] **Evidence** tab reflects result or graceful error.
- [ ] Misconfiguration does **not** take down hosting process (**exception handling** middleware).

---

## 12. Learning / reports validation

- [ ] **`Reports`** page loads (**`ReportsPage`**).
- [ ] **`Intake Learning`** tab (**`/api/reports/intake-learning`** backend) loads — **empty OK** on cold DB (**`docs/pilot-setup.md`**).
- [ ] **Routing Rule Health**: loads empty/sparse gracefully.
- [ ] **History** / similarity — **empty** cohort expected without related tickets (**`ticketOutcome`/embeddings** data).
- [ ] **`RebalanceOverviewPanel`** (**`docs/pilot-setup.md`** **Rebalance**) — **no opportunities** plausible on sparse workload.

---

## 13. Realtime / comments / attachments validation

Per **`Program.cs`**: **`/api/realtime/hub`** — **JWT** **`access_token`** query for SignalR; **Azure SignalR** vs **in-process** by connection string (**`Azure:SignalR:ConnectionString`** **`??`** **`ConnectionStrings:AzureSignalR`**).

- [ ] **Comments** CRUD (mapped in **`Program.cs`** via **`MapCommentEndpoints`**) — realtime refresh if enabled.
- [ ] Attachments (**`MapTicketAttachmentEndpoints`**) upload/delete per configuration.
- [ ] Notifications bell (**`notificationService`**) — no unhandled recurring errors (**check console**).
- [ ] Understand **fallback:** Without Azure SignalR conn string ⇒ **in-process SignalR** (single-process); multi-instance ⇒ configure Azure (**log line at startup**).

---

## 14. Expected empty states on fresh install

| Area | Typical empty expectation |
|------|---------------------------|
| Tickets / queues | Zero rows |
| Approval queue | No items |
| Outcomes/analytics aggregates | Sparse |
| Similar history | No neighbors |
| Rule health aggregates | Sparse / explanatory copy |
| Intake Learning cohorts | May be legitimately empty |
| Rebalance | No actionable moves |

**Principle:** UI should **tell the user what to do next** (create data, add rules, add owners), not silently fail (**`README`** product philosophy alignment).

---

## 15. Failure log

Copy for each pilot run:

| Step | Expected result | Actual result | Pass/Fail | Notes |
|------|-----------------|---------------|-----------|-------|
| Docker SQL | Listening **1433** / healthy | | | |
| Backend startup | Migrates + listens **5214** | | | |
| Frontend startup | Vite dev **5173** or Compose **4173** | | | |
| Login | Redirect + token **`aud`** match | | | |
| First admin bootstrap | Elevated **`User`** **`IsActive`** or denied per policy | | | |
| Routing rules | Visible in Configuration | | | |
| Ticket create | Persists ticket | | | |
| Approve | Owner assignment advances state | | | |
| Return | **`NeedsMoreInfo`** path OK | | | |
| Reject | **`Rejected`** path OK | | | |
| Reports / intake learning | No crash empty | | | |
| Rule Health | Loads without fatal error | | | |

Attach **screenshots**, **`docker compose logs cortex-api`**, **`dotnet`** console output, **`F12`** network errors.

---

## 16. Pass criteria

**Minimum bar** for pilot readiness check:

1. Backend **starts** fresh against empty DB (**`MigrateAsync`** succeeds).
2. **Login** validates JWT against **Auth0** configuration.
3. **Admin/users** path matches pilot policy (**bootstrap** vs **explicit provision** documented).
4. **Routing rules** configurable.
5. **Ticket lifecycle** **create → approve** works with modal/queue coherence.
6. Reports/learning/rule health surfaces **do not crash** on empty aggregates.
7. **Empty states** are explainable (**§14**).
8. Repo remains **without committed secrets**.

---

## 17. Recommended fix workflow

1. Record failure (**§15** row).
2. Capture **screenshot** + **`cortex-api` logs** (**`docker`** or **`dotnet`** ).
3. **One change per hypothesis** (config vs Auth0 vs data).
4. Re-run **only failing section**.
5. Re-run **`§9`** before external demo/commitment.

---

*Fresh install validation checklist — documentation only (Cortex Tier 12 Task 3).*
