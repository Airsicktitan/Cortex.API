# Tier 9 - Predictive Risk Reference

## 1. Purpose

Tier 9 Predictive Risk identifies likely SLA misses early and recommends actions before a breach occurs. It exists to shift Cortex from reactive handling to proactive guidance while preserving human control. In tier context, it is the entry point to predictive behavior and the bridge from Tiers 1-8 to higher proactive tiers.

## 2. How It Works (High Level)

Inputs include ticket state, SLA timing, workload context, triage quality, and recent collaboration signals. The system produces a risk level, supporting reasons, and a recommended next action. Core logic combines deterministic checks (time remaining, priority pressure, backlog conditions) with explainable scoring so that every output can be traced to visible signals. It depends on the SLA calculation layer, routing/workload telemetry, and current ticket timeline context.

## 3. Signals / Inputs

- Ticket signals: priority, SLA clock status, triage completeness, recent comments, age/staleness.
- User signals: requester urgency cues in comments, assignee responsiveness indicators.
- System signals: owner workload, queue pressure, time-to-breach thresholds, stale workload snapshot age.
- AI signals: advisory interpretation of missing detail risk and narrative risk context.

## 4. Output / Behavior

The system produces an explicit risk level (for example low/medium/high), reason list, and action recommendation. It presents this as advisory guidance in Cortex language with evidence-backed reasoning. It influences escalation awareness, triage follow-up priority, and routing urgency decisions, but it does not directly execute changes.

## 5. Constraints (NON-NEGOTIABLE)

- Advisory only; no autonomous actions.
- No mutation of owner, priority, SLA, or workflow state.
- Must remain deterministic-first and explainable.
- Must not bypass routing rules, approvals, or policy gates.
- Must not expose sensitive data or internal-only fields.
- If a system already exists, extend it - do not recreate it.

## 6. UX Language Rules

Use:
- "Cortex has identified elevated SLA risk based on current signals."
- "Based on current signals, here is the recommended next action."

Avoid:
- "AI decided this ticket will fail."
- "Model prediction selected this escalation."

## 7. Tier Alignment

Belongs to Tier 9 (Predictive Risk). It must not overlap with Tier 11 controlled intervention or Tier 12 orchestration behavior. It may inform Tier 10 proactive operations but must not perform Tier 10+ autonomous effects.

## 8. Extension Guidelines (CRITICAL)

- Safe to extend by adding new risk signals, tuning score weights, and improving recommendation phrasing.
- Keep extension points in existing risk evaluation services and shared signal adapters.
- New dependencies must remain observable and auditable.
- Must not introduce auto-actions, hidden side effects, or external ML decision authority without a formal spec and governance review.
- Must not replace deterministic controls with opaque inference.

## 9. Common Failure Modes

- Over-escalation from overly aggressive risk weighting.
- Missing-detail misclassification causing false high-risk signals.
- Stale workload data inflating or suppressing risk.
- Recommendation text that lacks concrete reasons.
- Risk calculated without current SLA timer refresh.

## 10. Example Scenario

Sample input: a high-priority ticket has 45 minutes to SLA breach, incomplete triage notes, owner queue is saturated, and comments show unresolved dependency questions.

Expected output: `High` risk with reasons ("short time-to-breach", "incomplete triage", "high owner load") and recommendation ("request missing detail now and consider reassignment review").

Reasoning: deterministic pressure signals (time + workload) combine with quality signals (triage/comment completeness), producing a proactive recommendation without automating reassignment.
