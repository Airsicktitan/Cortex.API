# Tier 11 - Controlled Intervention Reference

## 1. Purpose

Tier 11 Controlled Intervention enables constrained automated multi-step actions when explicit guardrails and visibility requirements are satisfied. It exists to safely convert qualified recommendations into controlled execution. In tier context, it is the first intervention tier and must remain bounded, reversible, and user-aware.

## 2. How It Works (High Level)

The system evaluates intervention eligibility using policy gates, confidence thresholds, and approval/visibility requirements. It outputs a controlled intervention plan and executes only allowed actions within defined scope. Core logic applies guardrail checks first, then executes reversible steps with audit logs and user-visible status. Dependencies include safe-autonomy gates, decision engine reasoning, approval state, and intervention audit logs.

## 3. Signals / Inputs

- Ticket signals: risk severity, missing detail state, SLA pressure, current owner/queue context.
- User signals: approval context, override history, stakeholder notification preferences.
- System signals: guardrail policy scope, allowed action catalog, reversibility capability, audit logging health.
- AI signals: advisory confidence/context signals supporting intervention prioritization only.

## 4. Output / Behavior

The system produces visible intervention actions such as follow-up task creation, missing-detail requests, stakeholder notifications, and risk-based escalation triggers. It presents intervention reasoning, action scope, and reversibility status in audit-friendly form. It influences issue containment speed while preserving explicit control boundaries.

## 5. Constraints (NON-NEGOTIABLE)

- No destructive actions.
- No silent actions.
- No black-box decisioning.
- Guardrails must enforce allowed action scope.
- All actions must be reversible.
- User awareness is required for all interventions.
- Intervention logs must be retained for audit.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex executed a controlled intervention within approved guardrails."
- "Based on current signals, this reversible action was triggered."

Avoid:
- "AI took full control."
- "The model acted without review."

## 7. Tier Alignment

Belongs to Tier 11 controlled intervention. It must not overlap with Tier 10 advisory-only behavior or Tier 12 orchestration-level cross-system authority. Tier 11 actions must remain scoped and reversible.

## 8. Extension Guidelines (CRITICAL)

- Safe extensions: add new reversible action types, refine guardrail checks, improve intervention audit detail.
- Extend existing intervention pipeline and approval-aware policy engines.
- Keep action eligibility deterministic and testable.
- Must not introduce hidden, irreversible, or destructive behaviors.
- Must not bypass approval/visibility requirements.

## 9. Common Failure Modes

- Triggering intervention on weak confidence or ambiguous signals.
- Partial execution where notification succeeds but audit log fails.
- Reversibility gaps for newly added action types.
- Guardrail drift allowing out-of-scope actions.
- Excessive intervention causing alert fatigue.

## 10. Example Scenario

Sample input: high-risk ticket with missing triage details, valid guardrail match, and approved intervention policy for requesting missing data and notifying stakeholders.

Expected output: controlled intervention executes visible detail request and stakeholder notification, logs each action, and marks intervention reversible where applicable.

Reasoning: policy gates and risk context meet Tier 11 criteria, enabling bounded execution without opaque autonomy.
