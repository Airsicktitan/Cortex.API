# 🧠 CORTEX — DemoJam Edition

**Central Operations & Routing Technology EXpert**

A demonstration of how enterprise support tooling should model **ownership, accountability, and responsibility — explicitly, not implicitly — while still giving operators the dashboards, reporting, and controls they need.**

---

## 🎯 Demo Objective

This demo focuses on **one core idea**:

> Ownership ambiguity is the root cause of most support delays.

CORTEX shows how explicitly modeling responsibility:

- Eliminates unclear ownership
- Reduces handoff friction
- Makes escalations factual instead of emotional
- Turns status reporting into something operationally useful

---

## 🧩 Problem Demonstrated

Traditional ticketing systems:

- Assume a single owner
- Hide responsibility shifts
- Rely on comments to explain intent

In real enterprise operations, this leads to:

- Delays during handoffs
- Confusion during escalation
- Lack of accountability

---

## 💡 Demo Flow

1. Log in and create a ticket with an attachment
2. Assign **Syniti Owner** (technical execution)
3. Assign **Business Owner** (validation)
4. Progress the ticket with comments and status updates
5. Observe SLA state on the board and dashboard
6. Open reports and export the SLA workbook to Excel
7. Show Configuration for SLA and Archive Policy
8. Move eligible tickets into the Archived Tickets view

---

## 🧠 Key Design Decisions Highlighted

- Dual ownership as a **first-class concept**, not a workaround
- API-level enforcement of system invariants
- Frontend designed to surface responsibility and triage first
- Configuration-driven SLA and archive behavior
- Architecture aligned with future SaaS scaling

---

## 🏗 Technical Overview (High Level)

- .NET 8 Minimal API backend
- React + TypeScript frontend
- SQL Server-backed persistence
- Auth0-secured API with role / permission-aware views
- Dashboard, reports, and Excel export
- Swagger-documented API

---

## 🚧 What This Demo Does NOT Show

- Automatic archive scheduling
- ML-based routing
- Real-time updates
- Multi-tenant separation
- Full audit timeline / notification workflows

> These are intentionally out of scope to keep the demo focused on **core workflow modeling**.

---

## 🎤 Talking Points

- Why “owner” is insufficient in enterprise support
- How CORTEX aligns tooling with real operational behavior
- How dashboarding, reporting, and configuration make the workflow actionable
- How explicit responsibility scales beyond ticketing into platform operations

---

## 👨‍💻 Author

Adam Hooper  
Senior Consultant, Syniti

---

**Status:** Demo-ready platform prototype | **Audience:** Internal Engineering & Leadership
