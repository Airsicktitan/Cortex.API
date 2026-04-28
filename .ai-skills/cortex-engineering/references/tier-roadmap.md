# Cortex Intelligence Tier Roadmap

## Purpose

Define the complete 12-tier Cortex intelligence model so AI capabilities evolve in a controlled sequence without scope drift.

## Core Principle

```text
Understand -> Recommend -> Learn -> Act -> Predict -> Prevent -> Orchestrate
```

---

## Tier 1 - Deterministic Rules

- routing rules
- explainable logic
- no AI mutation

## Tier 2 - AI Assist

- triage
- missing detail detection
- vision evidence
- advisory only

## Tier 3 - Memory

- embeddings
- semantic similarity
- related ticket insights

## Tier 4 - Feedback Capture

- overrides
- outcomes
- decision tracking

## Tier 5 - Learning System

- outcome ingestion
- learning signals

## Tier 6 - Learning-Adjusted Decisions

- confidence adjustment
- learning deltas

## Tier 7 - System Recommendations

- rule improvement suggestions
- optimization insights

## Tier 8 - Safe Autonomy

- controlled assignment
- audit trail
- control panel
- strict guardrails

## Tier 9 - Predictive + Prescriptive

- SLA risk prediction
- recommended actions
- no auto-action yet

## Tier 10 - Proactive Operations

### Purpose

Cortex identifies problems before a user opens a ticket.

### Capabilities

- detect patterns across tickets
- identify repeat issues
- detect systemic failures
- suggest preventative actions

### Examples

- "5 similar login failures detected in the last hour"
- "Authentication system degradation suspected"
- "Recommend creating a problem ticket or escalation"

### Constraints

- advisory only
- no auto-ticket creation yet

## Tier 11 - Automated Intervention (Controlled)

### Purpose

Cortex begins taking multi-step actions within strict guardrails.

### Capabilities

- auto-create follow-up tasks
- auto-request missing details
- auto-notify stakeholders
- escalate based on risk

### Examples

- request missing info automatically
- notify team when SLA risk is high
- suggest escalation workflows

### Constraints

- no destructive actions
- no silent actions
- all actions must be visible and reversible
- user awareness required

## Tier 12 - Operational Orchestration

### Purpose

Cortex becomes a system-level coordinator, not just a ticket tool.

### Capabilities

- orchestrate workflows across systems
- integrate with ServiceNow, Jira, and SharePoint
- cross-team coordination
- workload balancing across orgs
- end-to-end process optimization

### Examples

- reroute work across teams dynamically
- sync tickets across systems
- optimize global workload distribution
- recommend structural changes

### Constraints

- must remain explainable
- must remain auditable
- must never become a black box
- must preserve human control

---

## Current Program State

- Tier 8 complete (Safe Autonomy)
- Tier 9 in progress (Predictive + Prescriptive)

---

## Acceptance Alignment

This roadmap is complete when:

- all 12 tiers are clearly defined
- future AI work has direction
- tier boundaries prevent feature creep
- implementation remains aligned to current system reality
