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
2. Show ticket title and routing department rules auto-assigning the **Syniti Owner** and optional **Business Owner** while still allowing manual override
3. Show unmatched tickets defaulting the **Business Owner** back to the requester for validation ownership
4. Progress the ticket with comments, attachments, and status updates, showing the comment panel refreshing immediately with the right author
5. Open the ticket history modal to show who changed what and why
6. Observe SLA state on the board and dashboard
7. Show the notification bell surfacing assignment, SLA, and archive events in real time
8. Open reports, show the SLA and Online Users report views, registered custom reports, and export to Excel
9. Show Configuration for notification channels, SLA, Ticket Statuses, Ticket Routing, Archive Policies, Session Security, stored procedure registration, and report registration from SQL-backed views
10. Review Jobs, the failed-jobs queue, the header notification experience, and the automatic archive scheduler
11. Move eligible tickets into Archived Tickets and reactivate one back into the queue with its comments and attachments restored

---

## 🧠 Key Design Decisions Highlighted

- Dual ownership as a **first-class concept**, not a workaround
- API-level enforcement of system invariants
- Frontend designed to surface responsibility and triage first
- Dark-mode filter controls stay readable during live demo use
- Ticket details keep creator labels human-readable instead of falling back to generic system wording
- Configuration-driven SLA, archive, and session behavior
- Ticket statuses are configurable instead of being hard-coded into the workflow
- Title-phrase and department-based ticket routing is configurable instead of hard-coded into the create flow
- Change reasons are captured when they matter on edits and archive actions instead of cluttering new-ticket intake
- New tickets now use a clean numeric sequence, while older legacy ticket IDs still remain valid
- Configurable jobs, stored procedures, and custom reports for operator control
- Custom reports can be registered from a raw SELECT/CTE query or a full SQL view definition
- SQL-backed report registration now participates cleanly in transactional backend saves
- SQL-backed configuration features now prefer the Azure-named connection string while keeping the older `CortexDB` key as fallback support
- Auth0-backed user provisioning now stays inside the app’s DI-managed HTTP client pipeline
- Legacy local databases can upgrade cleanly even when older ticket/comment author references no longer map to real users
- Legacy archived tickets are backfilled from audit history so older archive records are no longer reduced to counts only
- Archive policies automatically maintain a background archive job while still supporting an immediate run-now action
- Reports and procedures can be registered from existing database objects or managed from the app
- Stored procedure deletion now safely disables dependent jobs instead of blocking operators in configuration
- Response mapping is resilient even when EF navigation properties are not preloaded
- Assignment, SLA, and archive activity now flow into a persisted in-app notification center
- Assignment and SLA-risk events can also fan out through backend-managed Email and Teams channels when those transports are configured
- Each user can keep the system default or override assignment and SLA-risk notifications to `Email`, `Teams`, `Both`, or `Neither`
- Self-assignment now still surfaces the assignment notification instead of being silently skipped
- Admins can remove users from the platform while preserving historical references through the fallback legacy user
- Legacy archived attachments with missing pre-upgrade binary content now come back as a clear restore note instead of fake attachment files
- Auth0-backed user provisioning is deployment-ready as long as management credentials are supplied through environment-specific app settings or secret storage
- Reporting and automation built into the platform, not bolted on
- Realtime updates are resilient to malformed stream messages during demo usage
- Architecture aligned with future SaaS scaling

---

## 🏗 Technical Overview (High Level)

- .NET 8 Minimal API backend
- React + TypeScript frontend
- SQL Server-backed persistence
- Auth0-secured API with role / permission-aware views
- Dashboard, reports, jobs, configuration, notifications, and Excel export
- Docker Compose full-stack startup for frontend, API, and SQL Server using an Azure-first connection-string setup with `CortexDB` still retained for compatibility
- Swagger-documented API

---

## 🚧 What This Demo Does NOT Show

- ML-based routing
- Multi-tenant separation
- External Slack delivery
- Auth0 role / permission sync for newly provisioned users

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
