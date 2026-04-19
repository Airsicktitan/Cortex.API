# 🧠 CORTEX

**Central Operations & Routing Technology EXpert**

A SaaS-style support operations platform designed to model **real enterprise ownership, accountability, and workflow flow** — not idealized ticket states.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-8-purple)
![React](https://img.shields.io/badge/React-19-blue)
![TypeScript](https://img.shields.io/badge/TypeScript-5-blue)

---

## 🚀 Why CORTEX Exists

At enterprise clients like Johnson & Johnson, ticketing is often handled through SharePoint, email, and manual coordination.

It works — but it creates friction:

- Ownership is unclear
- Assignment is manual
- SLA risk is hidden
- Teams rely on constant follow-ups

CORTEX was built to remove that friction.

It is an operations platform designed to:

- Automatically assign ownership
- Surface SLA risk in real time
- Show exactly what needs attention
- Allow users to act immediately without context switching

> **The goal: eliminate manual coordination and make responsibility obvious.**

---

## 🎯 Core Value Proposition

**CORTEX answers one critical operational question:**

> _Who is responsible for this ticket right now — and why?_

By separating execution from verification and enforcing ownership through the system:

- Responsibility is always visible
- Handoffs are faster and less ambiguous
- SLA risk is surfaced instead of discovered late
- Escalations are based on facts, not guesswork

---

## ⚡ What Makes CORTEX Different

Unlike generic ticketing systems, CORTEX is opinionated around real operational workflows:

- **Dual ownership model**
  Separates technical execution from business validation

- **Built-in routing intelligence**
  Automatically assigns ownership based on context, not manual triage

- **SLA awareness by default**
  Risk is surfaced in real time, not buried in reports

- **Action-first UI**
  Users can resolve issues directly from priority views without navigation overhead

- **Out-of-the-box usability**
  Works immediately without requiring heavy configuration

---

## 🤖 AI-Assisted Triage (System-Constrained Intelligence)

CORTEX includes an AI-assisted triage layer designed to enhance — not override — operational workflows.

Unlike generic AI integrations, CORTEX enforces **strict system boundaries**:

- AI operates within **CORTEX-defined statuses and priorities**
- AI cannot invent or assume workflow states
- All recommendations are validated against **live system configuration**

### 🧠 How It Works

- The system dynamically provides the AI with:
  - Valid ticket statuses (from configuration)
  - Valid priorities (from SLA configuration)
  - Optional descriptions and sequencing hints

- The AI generates:
  - Suggested priority
  - Suggested status (when applicable)
  - SLA risk assessment (Low / Medium / High)

### 🛡 Safety & Control

- AI outputs are **validated twice**:
  - At generation time (normalization to allowed values)
  - At persistence time (final enforcement)

- Invalid or unknown values are **rejected and logged**
- The system remains the **final authority** over all ticket updates

### 🎯 Design Principle

> AI does not define the workflow — it operates inside it.

### 🚀 Outcome

- Faster triage decisions
- Reduced manual prioritization
- No risk of AI corrupting workflow state
- Immediate compatibility with new statuses and priorities without code changes

---

## 🧠 Dual Ownership Model

| Role               | Responsibility                                         |
| ------------------ | ------------------------------------------------------ |
| **Syniti Owner**   | Technical execution (code, configuration, deployments) |
| **Business Owner** | Business validation and acceptance                     |

Ownership is **explicit, enforced, and auditable**.

---

## 🏗 SaaS-Oriented Architecture

## 🔧 Platform Maturity & Engineering Focus

CORTEX is actively evolving from a prototype into a production-ready platform.

Recent engineering work has focused on:

- **Frontend architecture decomposition**

- **Backend data integrity enforcement**

- **Stored procedure contract hardening**

- **Production debugging practices**

- **Safe deployment workflow**

- **AI integration with system constraints**
  - AI triage bounded by dynamic system vocabulary
  - No hardcoded workflow assumptions
  - Dual-layer validation for safety
  - Future-proof against new statuses and priorities

> CORTEX is built to handle real-world data safely.

---

## 📁 Repository Layout

- `CortexBackend/`
- `CortexFrontend/`
- `Cortex.API.sln`
- `docker-compose.yml`

---

## 📸 See It in Action

- Real-time dashboard
- Needs Attention workflow
- Inline editing
- Multi-board workflows
- CSV export (Excel / Google Sheets compatible)
- Auth0 session persistence
- Notification dropdown system
- AI-assisted triage suggestions

---

## 🧪 Product Philosophy

- Model reality first
- Protect invariants early
- Add intelligence only when data exists
- Refactor as understanding deepens

---

## 📌 Project Status

- 🚧 Active development
- 🧪 Internal prototype
- 🎯 DemoJam target

---

## 👨‍💻 Author

Adam Hooper
Senior Consultant, Syniti
GitHub: https://github.com/Airsicktitan
