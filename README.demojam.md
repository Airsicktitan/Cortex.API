# 🧠 CORTEX — DemoJam Edition

**Central Operations & Routing Technology EXpert**

A demonstration of how enterprise support tooling should model **ownership, accountability, and responsibility explicitly — while still delivering real-time visibility, automation, and operational control.**

---

## 🎯 Demo Objective

This demo focuses on one core idea:

> **Ownership ambiguity is the root cause of most support delays.**

CORTEX demonstrates how explicitly modeling responsibility:

- Eliminates unclear ownership  
- Reduces handoff friction  
- Makes escalations factual instead of emotional  
- Turns status reporting into actionable operations  

---

## 🧩 Problem Demonstrated

At enterprise organizations, ticketing is often handled through tools like SharePoint, email, or generic platforms.

These approaches:

- Assume a single owner  
- Hide responsibility shifts  
- Rely on comments to explain intent  

In real operations, this leads to:

- Delays during handoffs  
- Confusion during escalation  
- Lack of accountability  
- Constant manual coordination  

---

## 💡 What CORTEX Changes

CORTEX replaces manual coordination with system-driven clarity:

- Ownership is explicit and enforced  
- Routing is automatic but flexible  
- SLA risk is visible in real time  
- Priority work is surfaced without searching  
- Actions can be taken immediately, without context switching  

> **The system answers: who owns this, what’s at risk, and what needs to happen next.**

---

## 💡 Demo Flow

1. Log in and create a ticket with an attachment  
2. Show title and department-based routing automatically assigning the **Syniti Owner** and optional **Business Owner**  
3. Show unmatched tickets defaulting **Business Owner** to the requester  
4. Progress the ticket with comments, attachments, and status updates (live refresh with correct author identity)  
5. Open audit history to show who changed what and why  
6. Observe SLA state on the board and dashboard  
7. Show notification center surfacing assignment, SLA, and archive events  
8. Open reports (SLA, Online Users, custom reports, Excel export)  
9. Show configuration (notifications, SLA, routing, statuses, archive policies, jobs, reports)  
10. Review job system and automatic archive scheduler  
11. Archive and restore tickets with full data integrity  

---

## 🧠 Key Design Decisions Highlighted

- Dual ownership as a **first-class concept**, not a workaround  
- API-level enforcement of system invariants  
- Frontend optimized for **triage and responsibility visibility**  
- System works **out of the box**, with configuration as optional enhancement  
- Routing logic is configurable instead of hardcoded  
- SLA, archive, and session behavior are configuration-driven  
- Ticket statuses are configurable instead of workflow-locked  
- Change reasons captured only when operationally meaningful  
- Notification system supports in-app, Email, and Teams delivery  
- Per-user notification preferences with system defaults  
- Assignment, SLA, and archive activity are fully observable  
- Realtime updates are resilient during live system usage  
- Architecture aligned with future SaaS scaling  

---

## 🏗 Technical Overview (High Level)

- .NET 8 Minimal API backend  
- React + TypeScript frontend  
- SQL Server persistence  
- Auth0-secured API with role/permission enforcement  
- Dashboard, reporting, automation, configuration, and notifications  
- Docker Compose full-stack startup  
- Swagger-documented API  

---

## 🚧 What This Demo Does NOT Show

- ML-based routing  
- Multi-tenant separation  
- External Slack delivery  
- Auth0 role/permission sync for newly provisioned users  

> These are intentionally out of scope to keep focus on **core workflow modeling and operational value**.

---

## 🎤 Talking Points

- Why “owner” is insufficient in enterprise support  
- How explicit ownership removes coordination overhead  
- How automation reduces manual assignment  
- How SLA visibility changes operational behavior  
- How audit history enforces accountability  
- How configuration enables flexibility without code changes  
- How this model extends beyond ticketing into platform operations  

---

## 👨‍💻 Author

Adam Hooper  
Senior Consultant, Syniti  

---

**Status:** Demo-ready platform prototype  
**Audience:** Internal Engineering & Leadership  
