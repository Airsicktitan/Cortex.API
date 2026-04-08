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
- `CortexFrontend/` - React + TypeScript + Vite frontend
- `Cortex.API.sln` - root solution entry point for the backend project
- `docker-compose.yml` - root container orchestration for local API + SQL Server

This keeps the repository organized while still making it easy to `cd` into either application directly.

### Backend (.NET 8)

- Minimal APIs with route grouping
- Repository pattern for persistence boundaries
- Domain rules enforced at the API boundary
- Immutable system fields protected server-side
- SQL Server + EF Core persistence
- Swagger/OpenAPI documentation
- DTO-based API contracts (no direct entity exposure)
- Centralized mapping layer (`Mappers.cs`) for entity → response transformation
- Auth0 integration for authentication
- JWT validation middleware
- Claims-based authorization policies
- Automatic user provisioning from identity provider
- User context abstraction (`IUserContextService`) to decouple handlers from `HttpContext`
- Stored procedure-backed ticket archiving

### Frontend (React + TypeScript)

- Strong typing across API boundaries
- Auth0 login/logout flow
- UI surfaces ownership and responsibility first
- Filtering designed for operational triage (status + priority + SLA)
- Modal workflows minimize context switching
- Comment side panel architecture
- Dashboard, reports, and Excel export
- Saved filters, search, and pagination
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
- Dual ownership tracking
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
- Excel export for reports
- Archived tickets page
- Archive policy configuration and manual archive execution
- SLA configuration page
- Admin users directory and edit flow
- User profile editing
- Expired / inactive account blocking in the UI
- DTO-based API responses
- Centralized mapping layer (`ToResponse()` pattern)
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
- 🔔 Notification workflows
- 🗂 Archived ticket detail / restore experience
- ⏱ Automatic archive scheduling
- 🧾 Audit history timeline
- 🤖 ML-assisted categorization (ML.NET)
- 🔄 Real-time updates (SignalR)
- 🏢 Multi-tenant support

---

## ⚠️ Known Limitations (Intentional)

CORTEX is under active development. The following limitations are **known, documented, and planned**:

- ❌ Routing / assignment logic is still manual
- ❌ Archive policy is manual-triggered today (no scheduler yet)
- ❌ Archived tickets currently preserve summary metadata, not full archived comment/attachment browsing
- ❌ No notifications yet for assignment, SLA, or archive events
- ❌ No real-time push (polling / refresh only)
- ❌ Mapping layer dependent on eager loading (navigation properties must be included)

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
