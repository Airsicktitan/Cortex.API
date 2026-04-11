# 🧠 CORTEX

**Central Operations & Routing Technology EXpert**

A SaaS-style support operations platform designed to model **real enterprise ownership, accountability, and workflow flow** — not idealized ticket states.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)  
![.NET](https://img.shields.io/badge/.NET-8-purple)  
![React](https://img.shields.io/badge/React-19-blue)  
![TypeScript](https://img.shields.io/badge/TypeScript-5-blue)

---

## 🚀 Product Summary

CORTEX is an intelligent support operations platform that introduces **explicit, dual ownership** into ticket lifecycles while adding the operational tooling teams expect in a real product.

Instead of assuming a single “owner,” CORTEX models how enterprise support actually works:

- Technical teams execute
- Business teams verify
- Responsibility shifts — and must be visible

CORTEX is built as a **product**, not a demo:

- Opinionated domain model
- API-enforced invariants
- Frontend designed around accountability and triage
- Architecture intended to scale

---

## 🎯 Core Value Proposition

**CORTEX answers one question clearly:**

> _Who is responsible for this ticket right now — and why?_

By separating execution from verification, CORTEX:

- Reduces ownership ambiguity
- Shortens handoff delays
- Improves auditability
- Makes escalations factual instead of emotional

---

## 🧠 Dual Ownership Model

| Role               | Responsibility                                         |
| ------------------ | ------------------------------------------------------ |
| **Syniti Owner**   | Technical execution (code, configuration, deployments) |
| **Business Owner** | Business validation and acceptance                     |

Ownership is **explicit, enforced, and auditable** — not inferred from comments or status names.

---

## 🏗 SaaS-Oriented Architecture

## 📁 Repository Layout

- `CortexBackend/` - ASP.NET Core API, EF Core models, handlers, services, and backend Dockerfile
- `CortexFrontend/` - React + TypeScript + Vite frontend with its own Dockerfile
- `Cortex.API.sln` - root solution entry point for the backend project
- `docker-compose.yml` - root container orchestration for local frontend + API + SQL Server

This keeps the repository organized while still making it easy to `cd` into either application directly.

### Backend (.NET 8)

- Minimal APIs with route grouping
- Repository pattern for persistence boundaries
- Domain rules enforced at the API boundary
- Immutable system fields protected server-side
- SQL Server + EF Core persistence
- Swagger/OpenAPI documentation
- Docker-ready backend image for local containerized development
- DTO-based API contracts (no direct entity exposure)
- Centralized mapping layer (`Mappers.cs`) for entity → response transformation
- Response mapping context resolves display metadata without requiring EF eager loading
- Auth0 integration for authentication
- JWT validation middleware
- Claims-based authorization policies
- Connection string resolution prefers `AzureCortexDb` while still supporting `CortexDB` as a compatibility fallback
- Automatic user provisioning from identity provider
- Admin / developer user creation pushed into Auth0 (Management API-backed)
- Auth0 management provisioning uses the DI-configured HTTP client pipeline end-to-end
- User context abstraction (`IUserContextService`) to decouple handlers from `HttpContext`
- Stored procedure-backed ticket archiving
- Scheduled jobs and stored procedure registry
- Database-backed custom SQL report registry for admin / developer reporting
- Custom report save flow accepts view-body SQL or full `CREATE VIEW ... AS ...` scripts
- Database-backed report and stored procedure management now runs inside the active EF transaction safely
- Ticket status registry for admin / developer workflow configuration
- Ticket audit history capture and retrieval
- Session security and online presence tracking
- Persisted in-app notifications for assignment, SLA, and archive events
- Background SLA notification monitoring with real-time updates
- Multi-policy archive configuration with automatic scheduling and run-now execution
- Legacy local databases can self-heal older ticket/comment author references during migration by seeding a fallback legacy user
- Docker Compose local stack for frontend, backend, and SQL Server

### Frontend (React + TypeScript)

- Strong typing across API boundaries
- Auth0 login/logout flow
- UI surfaces ownership and responsibility first
- Filtering designed for operational triage (status + priority + SLA)
- Modal workflows minimize context switching
- Comment side panel architecture
- Dashboard, reports, and Excel export
- Reports submenu with SLA, Online Users, and registered custom reports
- Saved filters, search, and pagination
- Persistent resizable left navigation
- Docker-ready frontend image with SPA routing support
- Jobs view with failed jobs queue
- Header notification for failed jobs
- Header notification center for assignment, SLA, and archive events
- Audit history modal from ticket workflow
- Session timeout warning and re-auth flow
- Admin / developer configuration for ticket statuses and archive-eligible states
- Configuration support for DB-backed views and stored procedures with discovery from SQL Server
- Real-time UI consistency with backend display name mapping

This architecture intentionally mirrors:

- Multi-tenant SaaS APIs
- Internal tooling platforms
- Operational dashboards

---

## 🔐 Authentication & Identity

CORTEX uses **Auth0 as an external identity provider**.

### Implemented

- OAuth2 Authorization Code flow
- JWT validation in ASP.NET middleware
- Swagger OAuth integration
- `/users/me` endpoint for current user resolution
- Automatic user creation on first login
- Claims-based identity mapping (`sub` as primary key)
- Display name resolution via Auth0 claims (`name`, `preferred_username`, fallback logic)
- Backend-driven identity normalization (API owns final user representation)
- Permission-aware UI and API authorization policies
- Admin-only user management and configuration endpoints
- Admin / developer-driven user creation into Auth0-backed login
- Configurable inactivity timeout with re-auth prompt
- Presence tracking for online-user reporting

### Runtime Configuration Note

- In-app user provisioning requires `Auth0:ManagementClientId`, `Auth0:ManagementClientSecret`, and `Auth0:DatabaseConnection` to be configured in the target environment.
- For local development, use user secrets or local app settings.
- For deployed environments, set them through environment variables, platform app settings, or a secret store such as Azure Key Vault.

### Why This Matters

This architecture reflects real SaaS platforms where:

- Identity is externalized
- Applications manage authorization and domain data
- Users are provisioned dynamically
- UI does not trust raw identity tokens for display logic

---

## ✨ Current Feature Set

### Implemented

- Ticket lifecycle management (CRUD)
- Plain numeric ticket IDs for new tickets with legacy ID compatibility for existing records
- Dual ownership tracking
- Department-based Syniti owner auto-routing with manual override and requester-default business ownership
- Ticket visibility rules by role and assignment
- Priority, status, SLA, and search filtering
- Saved ticket filters and pagination
- Audit metadata (created / modified)
- Immutable system fields
- API documentation
- Comment system per ticket
- Attachment upload, download, and drag-and-drop support
- SLA tracking with visual status indicators
- Dashboard view for operational summaries
- Reports view with SLA breakdowns
- Online users reporting for admin / developer users
- Custom SQL report registration and execution backed by SQL views
- Custom report creation with optional auto-generated SQL view names
- Excel export for reports
- Archived tickets page
- Archived ticket reactivation with archived comments and attachments restored into the active ticket
- Legacy archived tickets are backfilled from audit history so older archives no longer remain counts-only
- Legacy archived attachments with unrecoverable pre-upgrade file bytes now restore as a clear system note instead of fake placeholder attachment files
- Multi-policy archive configuration with add / edit / delete, automatic archive scheduling, and manual archive execution
- In-app notifications for assignment changes, SLA at-risk / breached alerts, and archive / reactivate events
- Ticket status registry with create, edit, enable / disable, and delete controls
- SLA configuration page
- Session security configuration
- Jobs page for scheduled automation and stored procedure execution
- Stored procedure registry with DB create / edit / enable / disable / delete controls
- Stored procedure deletion now detaches and disables dependent jobs instead of blocking the delete action
- Discovery and registration of existing SQL Server views and stored procedures
- Failed jobs queue with header notification
- Admin users directory and edit flow
- Admin / developer user creation flow
- Admin-only user deletion flow with Auth0 deprovisioning and safe legacy-reference reassignment
- User profile editing
- Expired / inactive account blocking in the UI
- Ticket audit history modal with change reasons
- Ticket modal creator labels stay user-friendly for both persisted tickets and new-ticket drafts
- Custom report create / delete flow
- Real-time ticket, comment, and attachment refresh via server push
- Real-time notification refresh via server push
- DTO-based API responses
- Centralized mapping layer (`ToResponse()` pattern)
- Response mapping independent of loaded navigation properties
- Display name resolution for comments and users
- Auth0 authentication
- Automatic user provisioning
- Repository-based persistence layer
- Swagger OAuth login flow
- Clean separation of:
  - Models
  - DTOs
  - Repositories
  - Handlers
  - Endpoints

### In Progress / Planned

- 🧠 Skill- and workload-based routing engine
- 🔐 Auth0 role / permission sync for provisioned users
- 🤖 ML-assisted categorization (ML.NET)
- 🏢 Multi-tenant support

---

## ⚠️ Known Limitations (Intentional)

CORTEX is under active development. The following limitations are **known, documented, and planned**:

- ❌ Notifications are currently in-app only (no email / Teams / Slack delivery yet)
- ❌ Existing legacy `TICKET-###` records are still supported as-is; only newly created tickets use the plain numeric sequence

## 🐳 Containerized Local Stack

- `docker compose up --build` starts:
  - `cortex-frontend` on `http://localhost:4173`
  - `cortex-api` on `http://localhost:5214`
  - `cortex-sql` on `localhost:1433`
- Docker Compose now provides both `ConnectionStrings__AzureCortexDb` and `ConnectionStrings__CortexDB`, with Azure preferred by the API
- The frontend container is built with `VITE_API_URL=http://localhost:5214/api`

> These are staged deliberately to keep the core domain model stable before layering complexity.

---

## 🧪 Product Philosophy

CORTEX follows a deliberate product approach:

- Model reality first
- Protect invariants early
- Add intelligence only when data exists
- Refactor openly as understanding deepens

This repository reflects real-world iteration, not frozen perfection.

---

## 📌 Project Status

- 🚧 Active development
- 🧪 Internal platform prototype with working end-to-end flows
- 🎯 Long-term goal: internal platform-grade deployment
- 🎤 Target: Syniti DemoJam presentation

---

## 👨‍💻 Author

**Adam Hooper**  
Senior Consultant, Syniti  
Full-Stack / Platform-Focused Engineer

GitHub: https://github.com/Airsicktitan
