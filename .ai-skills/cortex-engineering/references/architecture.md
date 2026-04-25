# Architecture Reference

## Backend Structure

- Controllers / Minimal APIs
- Services (business logic)
- Repositories (data access)
- Models (domain)
- DTOs (transport)

---

## Key Domains

### Ticket

- Core entity
- Contains:
  - owners (Syniti + Business)
  - SLA
  - status
  - AI fields
  - routing decision

---

### Routing Decision

- Persisted per ticket
- Includes:
  - selected owner
  - alternatives
  - reasoning
  - confidence
  - override flag

---

### AI Triage

Stored on ticket:

- AiTriageSummary
- AiTriageSuggestedPriority
- AiTriagePriorityReason
- AiTriageMissingDetailsJson
- AiRiskLevel (optional)

Must persist across modal lifecycle.

---

### Rebalance

- Generates workload-aware recommendations
- Must match execution exactly
- Must explain:
  - why move
  - expected impact
  - blockers

---

## Realtime

SignalR is used for:

- ticket updates
- comments
- notifications

RULE:
Never force full UI reload if SignalR update is possible.

---

## Configuration

DO NOT hardcode:

- SLA values
- statuses
- priorities
- routing rules
- roles
- departments

These must come from DB/config.