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

## 🧠 Dual Ownership Model

| Role               | Responsibility                                         |
| ------------------ | ------------------------------------------------------ |
| **Syniti Owner**   | Technical execution (code, configuration, deployments) |
| **Business Owner** | Business validation and acceptance                     |

Ownership is **explicit, enforced, and auditable** — not inferred from comments or status names.

---

## 🧠 How It Works in Practice

CORTEX is designed to work **out of the box**, with intelligent defaults for routing, ownership, and SLA tracking — while still allowing deeper configuration where needed.

---

## 🏗 SaaS-Oriented Architecture

## 📁 Repository Layout

- `CortexBackend/` - ASP.NET Core API, EF Core models, handlers, services, and backend Dockerfile
- `CortexFrontend/` - React + TypeScript + Vite frontend with its own Dockerfile
- `Cortex.API.sln` - root solution entry point for the backend project
- `docker-compose.yml` - root container orchestration for local frontend + API + SQL Server

---

## 📸 See It in Action

> Screenshots coming soon

CORTEX includes:

- Real-time dashboard with SLA risk and ownership visibility
- “Needs Attention” workflow for high-priority tickets
- Inline ticket editing and collaboration
- Routing and notification configuration

---

## 🧪 Product Philosophy

CORTEX follows a deliberate product approach:

- Model reality first
- Protect invariants early
- Add intelligence only when data exists
- Refactor openly as understanding deepens

This repository reflects real-world operational problems and iterative product development — not theoretical design.

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
