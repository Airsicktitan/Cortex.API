---
name: cortex-engineering
description: Use for ALL work in Cortex (routing, AI, UI, backend, rebalance, metrics, demo-readiness)
---

# Cortex Engineering Skill

## Identity

CORTEX = Central Operations & Routing Technology EXpert

Cortex is NOT a ticketing system.

It is an **operations decision platform** that makes:

- ownership
- readiness
- evidence
- routing
- accountability

explicit and measurable.

---

## Core Value

Better tickets → fewer follow-ups → faster decisions

---

## Architecture Overview

Backend:

- .NET 8 Minimal APIs
- EF Core
- SQL Server / Azure SQL
- SignalR
- Auth0

Frontend:

- React + TypeScript
- Tailwind
- Vite

Infra:

- Docker
- Azure Container Apps
- GitHub Actions

---

## System Design Principles

### 1. Deterministic > Intelligent

- Rules define behavior
- AI supports, never replaces
- Explainability > complexity

---

### 2. AI is Advisory Only

AI can:

- summarize
- suggest priority
- identify missing details
- detect risk

AI cannot:

- mutate system fields automatically
- override routing
- invent statuses or priorities

---

### 3. Explain Everything

Every decision should answer:

- Why this owner?
- Why not others?
- What signals were used?
- What is the impact?

---

### 4. Localized UX

- No full board reloads for small actions
- Use SignalR updates
- Preserve scroll + modal state
- Behave like Jira/Facebook (live, not refreshing)

---

### 5. Demo = Product

Everything must feel:

- fast
- intentional
- explainable
- valuable

No "dev-only" UX leaks into demo paths.

---

## Critical Systems

- Routing Engine
- Workload Scoring
- AI Triage
- Rebalance Engine
- SignalR Realtime Updates
- Workflow Metrics

## UI Language Enforcement

All UI changes MUST follow:

- ui-language.md
- ui-standards.md

Reject:

- system-facing wording
- ambiguous phrasing
- unclear ownership language

Prefer:

- decision clarity
- action guidance
- impact explanation

## Reference Documents (ALWAYS LOAD)

Located in: ./references/

- ui-language.md
- ui-standards.md
- decision-engine.md
- routing-rules.md
- rebalance.md
- rebalance-analysis.md
- metrics.md
- demo-mode.md
- enterprise-hardening.md
