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

> *Who is responsible for this ticket right now — and why?*

By separating execution from verification, CORTEX:
- Reduces ownership ambiguity
- Shortens handoff delays
- Improves auditability
- Makes escalations factual instead of emotional

---

## 🧠 Dual Ownership Model

| Role | Responsibility |
|------|----------------|
| **Syniti Owner** | Technical execution (code, configuration, deployments) |
| **Business Owner** | Business validation and acceptance |

Ownership is **explicit, enforced, and auditable** — not inferred from comments or status names.

---

## 🏗 SaaS-Oriented Architecture

### Backend (.NET 10)
- Minimal APIs with route grouping
- Domain rules enforced at the API boundary
- Immutable system fields protected server-side
- SQL Server + EF Core persistence
- Swagger/OpenAPI documentation
- CORS configured for multi-client access
- DTO-based API contracts
- Password hashing infrastructure

### Frontend (React + TypeScript)
- Strong typing across API boundaries
- UI surfaces ownership and responsibility first
- Filtering designed for operational triage
- Modal workflows minimize context switching
- Comment side panel architecture

This architecture intentionally mirrors:
- Multi-tenant SaaS APIs
- Internal tooling platforms
- Operational dashboards

---

## ✨ Current Feature Set

### Implemented
- Ticket lifecycle management (CRUD)
- Dual ownership tracking
- Priority & status filtering
- Audit metadata (created / modified)
- Immutable system fields
- API documentation
- Responsive UI with real-time data binding
- Comment system per ticket
- DTO-based API responses
- Password hashing and user model foundation

### In Progress / Planned
- 🔐 Authentication & Authorization (JWT + policies)
- 🧠 Skill- and workload-based routing engine
- 📦 Environment progression tracking (Dev → QA → Prod)
- 📊 SLA and duration analytics
- 🤖 ML-assisted categorization (ML.NET)
- 🔄 Real-time updates (SignalR)
- 🏢 Multi-tenant support

---

## ⚠️ Known Limitations (Intentional)

CORTEX is under active development. The following limitations are **known, documented, and planned**:

- ❌ Login endpoint not yet implemented
- ❌ No role-based authorization policies yet
- ❌ Routing logic currently manual
- ❌ No historical SLA analytics yet
- ❌ No real-time push (polling only)

> These are staged deliberately to keep the core domain model stable before layering complexity.

---

## 🧭 Development Log

### Session: Authentication Foundation & Comment System Integration

Today’s work focused on strengthening the application’s **data model, architecture boundaries, and UI behavior**, while preparing the system for authentication and real-time collaboration features.

---

### 🔐 Authentication & User System

#### Implemented
- Introduced a `User` domain model
- Added:
  - Username
  - Email
  - PasswordHash
  - Role
  - Department
  - CreatedDate
  - LastLoginDate
  - ExpiryDate
  - IsActive

- Implemented:
  - `CreateUserRequest` DTO
  - `UserResponse` DTO
  - Password hashing using `PasswordHasher<T>`
  - UserHandlers for:
    - Creating users
    - Retrieving users

#### Architectural Decisions
- Passwords hashed server-side before persistence
- DTO pattern enforced to prevent exposing sensitive fields
- Domain models not returned directly from endpoints
- Responses shaped explicitly for API consumers

This establishes the foundation for:
- JWT issuance
- Login endpoints
- Role-based authorization
- Multi-tenant architecture

---

### 💬 Comment System

#### Implemented
- Comment model linked to tickets
- Ticket modal displays comments in a dedicated side panel
- Comment creation integrated into UI workflow
- Comments now load per ticket and persist correctly

#### Fixes & Improvements
- Fixed handler returning all comments instead of filtering by TicketId
- Prevented comments bleeding across tickets
- Improved comment loading lifecycle

---

### 🧠 UI Stability Improvements

Resolved subtle UI issues:

- Modal flicker when switching tickets
- React StrictMode double-render artifacts
- State timing issues during modal transitions
- Comment rendering race conditions

These changes improved perceived performance and stability.

---

### 🛠 Backend Corrections

- Refactored handlers for proper filtering logic
- Strengthened DTO mapping discipline
- Improved endpoint separation and structure

---

### 🧩 Architectural Improvements

Continued separation of:
- Models
- DTOs
- Handlers
- Endpoints

Reinforced API boundary discipline:
- No direct entity exposure
- Explicit response shaping
- Clear ownership of responsibilities

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
