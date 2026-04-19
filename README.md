# 🧠 CORTEX

**Central Operations & Routing Technology EXpert**

A modern operations platform designed to eliminate ambiguity in enterprise support workflows by making **ownership, readiness, and evidence explicit** — with AI that enhances decisions without breaking system rules.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![.NET](https://img.shields.io/badge/.NET-8-purple)
![React](https://img.shields.io/badge/React-19-blue)
![TypeScript](https://img.shields.io/badge/TypeScript-5-blue)

---

## 🚀 Why CORTEX Exists

Enterprise ticketing systems (SharePoint, email, legacy tools) create friction:

- Ownership is unclear
- Tickets are low-quality
- Follow-up is constant
- SLA risk is hidden

CORTEX was built to remove that friction.

> **The goal: better tickets → fewer follow-ups → faster decisions**

---

## 🎯 Core Value Proposition

CORTEX answers one critical question:

> _Can a reviewer act on this ticket immediately — or will they need follow-up?_

It achieves this by:

- Improving input quality (intake assist)
- Extracting signal from evidence (screenshot insight)
- Surfacing readiness (reviewer signal)
- Measuring outcomes (workflow metrics)

---

## ⚡ What Makes CORTEX Different

### 🧠 AI that works inside the system

- No hallucinated statuses or priorities
- Uses live system configuration
- Fully validated before persistence

### 🧩 Multi-source understanding

CORTEX understands:

- User input (description)
- System rules (triage + SLA)
- Visual evidence (screenshots)

---

### 👥 Dual ownership model

| Role           | Responsibility          |
| -------------- | ----------------------- |
| Syniti Owner   | Technical execution     |
| Business Owner | Validation / acceptance |

Ownership is explicit, enforced, and auditable.

---

## 🤖 AI Capabilities

### 1. Intake Assist (Requester-side)

- Improves descriptions before submission
- Structures input for reviewer clarity
- Identifies missing details

---

### 2. Reviewer Quality Signal

- Ready for Review
- Small Gaps Remain
- Needs Detail First

> Instantly answers: “Can I act on this?”

---

### 3. Screenshot Insight (Vision AI)

- Extracts meaning from attached screenshots
- Identifies visible issues
- Suggests likely causes
- Recommends follow-up

✔ Persisted with the ticket  
✔ Survives modal close  
✔ Auto-runs when needed (token-safe)

---

### 4. Workflow Metrics (Proof Layer)

CORTEX doesn’t just help — it measures impact:

- Intake assist usage
- Reviewer readiness distribution
- Follow-up proxy (comment count)
- Screenshot insight usage

> Moves from “this helps” → “this improves workflows”

---

## 🏗 Architecture

- .NET 8 (Minimal APIs)
- React + TypeScript
- SQL Server
- EF Core
- Auth0 authentication
- Docker deployment

---

## 🧠 Key Design Principles

- Model real workflows, not ideal states
- Make responsibility explicit
- Constrain AI with system rules
- Persist AI insight (not just generate it)
- Measure outcomes, not just actions

---

## 📁 Repository Layout

- `CortexBackend/`
- `CortexFrontend/`
- `Cortex.API.sln`
- `docker-compose.yml`

---

## 📊 Product Philosophy

- **Clarity over complexity**
- **Signal over noise**
- **System authority over AI guesswork**
- **Measurement over assumption**

---

## 📌 Status

- 🚧 Active development
- 🧪 Demo + pilot ready
- 🎯 Production direction

---

## 👨‍💻 Author

Adam Hooper  
Senior Consultant, Syniti  
GitHub: https://github.com/Airsicktitan
