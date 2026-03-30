# 🧠 CORTEX

**Central Operations & Routing Technology EXpert**

A SaaS-style support operations platform designed to model **real enterprise ownership, accountability, and workflow flow** — not idealized ticket states.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)  
![.NET](https://img.shields.io/badge/.NET-10-purple)  
![React](https://img.shields.io/badge/React-18-blue)  
![TypeScript](https://img.shields.io/badge/TypeScript-5-blue)

---

## 🚀 Product Summary

CORTEX is an intelligent support operations platform that introduces **explicit, dual ownership** into ticket lifecycles.

Instead of assuming a single “owner,” CORTEX models how enterprise support actually works:

- Technical teams execute
- Business teams verify
- Responsibility shifts — and must be visible

CORTEX is built as a **product**, not a demo:

- Opinionated domain model
- API-enforced invariants
- Frontend designed around accountability
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

### Backend (.NET 10)

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
- Automatic user provisioning from identity provider
- User context abstraction (`IUserContextService`) to decouple handlers from `HttpContext`

### Frontend (React + TypeScript)

- Strong typing across API boundaries
- Auth0 login/logout flow
- UI surfaces ownership and responsibility first
- Filtering designed for operational triage (status + priority)
- Modal workflows minimize context switching
- Comment side panel architecture
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
- Priority & status filtering
- Audit metadata (created / modified)
- Immutable system fields
- API documentation
- Comment system per ticket
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

- 🔐 Role-based authorization policies
- 👤 Ticket ownership restrictions
- 🧠 Skill- and workload-based routing engine
- 📦 Environment progression tracking (Dev → QA → Prod)
- 📊 SLA and duration analytics
- 🤖 ML-assisted categorization (ML.NET)
- 🔄 Real-time updates (SignalR)
- 🏢 Multi-tenant support

---

## ⚠️ Known Limitations (Intentional)

CORTEX is under active development. The following limitations are **known, documented, and planned**:

- ❌ Authorization policies not fully implemented
- ❌ Ticket visibility rules still being implemented
- ❌ Routing logic currently manual
- ❌ No historical SLA analytics yet
- ❌ No real-time push (polling only)
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
- 🧪 Pre-alpha SaaS prototype
- 🎯 Long-term goal: internal platform-grade deployment
- 🎤 Target: Syniti DemoJam presentation

---

## 👨‍💻 Author

**Adam Hooper**  
Senior Consultant, Syniti  
Full-Stack / Platform-Focused Engineer

GitHub: https://github.com/Airsicktitan
