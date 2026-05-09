# Agent Operating Rules Reference

## 1. Purpose

This reference defines mandatory operating behavior for Cortex agents across all tiers. It exists to prevent unsafe autonomy, inconsistent implementation, and tier boundary violations. It sits as a governance layer that all capability-specific references must follow.

## 2. How It Works (High Level)

Agents consume system references, tier constraints, and approved policy definitions before implementing or invoking logic. They output compliant behaviors, explainable changes, and tier-appropriate recommendations/actions. Core logic enforces "policy first, implementation second" so work is routed through existing systems rather than ad hoc paths. Dependencies include tier references, decision/routing references, and security guardrails.

## 3. Signals / Inputs

- Ticket signals: current workflow state, owner context, SLA status, triage completeness.
- User signals: explicit instructions, overrides, approval status.
- System signals: tier configuration, policy constraints, service capabilities, audit requirements.
- AI signals: confidence and suggestion metadata used only within allowed tier boundaries.

## 4. Output / Behavior

The system produces compliant agent behavior: what to do, what not to do, and what to escalate for human review. It presents this as operating rules that govern implementation and runtime decisions. It influences all code changes, routing decisions, and automation boundaries.

## 5. Constraints (NON-NEGOTIABLE)

- AI behavior must remain explainable and auditable.
- Agents must respect configured Cortex vocabulary and policy language.
- Agents must not exceed approved tier scope.
- Tier boundaries must not be skipped.
- Tier 9 remains advisory; Tier 10 remains proactive insight only unless explicitly expanded by spec.
- Must not bypass approval gates, routing rules, or safety controls.
- If a system already exists, extend it - do not recreate it.
- **Integrations:** external provider fields and sync paths must not directly control routing, owners, or approvals. Follow `references/integration-architecture.md` for credential handling, health/test honesty, and field-mapping governance. Do not return secrets to clients or log credential payloads.

## 6. UX Language Rules

Use:
- "Cortex has identified..."
- "Based on current signals..."

Avoid:
- "AI decided..."
- "The model overruled..."

## 7. Tier Alignment

This reference applies across all tiers as a control layer. It must not overlap with capability-specific logic implementation details; those belong in system references such as routing, predictive risk, and security audit docs.

## 8. Extension Guidelines (CRITICAL)

- Add new operating rules only when they are cross-cutting and tier-relevant.
- Keep capability-specific logic in its own system reference file.
- Extensions must preserve deterministic and safety-first operation.
- Must not weaken approval requirements, auditability, or human visibility.
- Must not create alternate policy channels outside approved references.

## 9. Common Failure Modes

- Tier leakage where higher-tier behavior appears in lower-tier features.
- Inconsistent language causing user confusion about autonomy level.
- Rules documented but not enforced in implementation reviews.
- Parallel policy definitions across multiple files causing conflict.

## 10. Example Scenario

Sample input: an agent is asked to auto-reassign a risky Tier 9 ticket with no approval context.

Expected output: advisory recommendation and escalation request, not auto-reassignment.

Reasoning: operating rules enforce tier boundary and approval-first behavior.

## Anti-Rediscovery Rule

Before implementing ANY logic:

- Check existing references.
- Do not reimplement systems already defined.
- Do not create parallel logic paths.
- Extend existing services instead.
