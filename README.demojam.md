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
4. Progress the ticket with comments, attachments, and status updates
5. Open the ticket history modal to show who changed what and why
6. Observe SLA state on the board and dashboard
7. Open reports, show the SLA and Online Users report views, registered custom reports, and export to Excel
8. Show Configuration for SLA, Ticket Statuses, Archive Policies, Session Security, stored procedure registration, and report registration from SQL-backed views
9. Review Jobs, the failed-jobs queue, and the header notification experience
10. Move eligible tickets into Archived Tickets and reactivate one back into the queue

---

## 🧠 Key Design Decisions Highlighted

- Dual ownership as a **first-class concept**, not a workaround
- API-level enforcement of system invariants
- Frontend designed to surface responsibility and triage first
- Configuration-driven SLA, archive, and session behavior
- Ticket statuses are configurable instead of being hard-coded into the workflow
- Configurable jobs, stored procedures, and custom reports for operator control
- Reports and procedures can be registered from existing database objects or managed from the app
- Response mapping is resilient even when EF navigation properties are not preloaded
- Reporting and automation built into the platform, not bolted on
- Architecture aligned with future SaaS scaling

---

## 🏗 Technical Overview (High Level)

- .NET 8 Minimal API backend
- React + TypeScript frontend
- SQL Server-backed persistence
- Auth0-secured API with role / permission-aware views
- Dashboard, reports, jobs, configuration, and Excel export
- Swagger-documented API

---

## 🚧 What This Demo Does NOT Show

- Automatic archive scheduling
- ML-based routing
- Real-time updates
- Multi-tenant separation
- Notification workflows
- Auth0 role / permission sync for newly provisioned users
- Full archived comment / attachment restoration on reactivation

> These are intentionally out of scope to keep the demo focused on **core workflow modeling**.

---

## 🎤 Talking Points

- Why “owner” is insufficient in enterprise support
- How CORTEX aligns tooling with real operational behavior
- How dashboarding, reporting, automation, and configuration make the workflow actionable
- How audit history turns ticket changes into accountable operational events
- How configuration keeps the platform extensible without changing code for every new report, view, procedure, or job
- How explicit responsibility scales beyond ticketing into platform operations

---

## 👨‍💻 Author

Adam Hooper  
Senior Consultant, Syniti

---

**Status:** Demo-ready platform prototype | **Audience:** Internal Engineering & Leadership
