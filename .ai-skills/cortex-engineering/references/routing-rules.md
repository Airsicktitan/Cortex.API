# Routing Rules

## Priority Order

Routing decisions must follow:

1. RulePriority DESC
2. Weight DESC
3. Match Count DESC
4. Id ASC

---

## Inputs

- BoardId
- Priority
- RequesterDepartment
- RequesterRole

---

## Constraints

- Owner must be eligible
- Respect:
  - IsSynitiOwnerEligible
  - IsBusinessOwnerEligible

---

## Workload Integration

Routing must consider:

- active tickets
- high/critical count
- SLA risk
- overdue/stale tickets

---

## Output Requirements

Every routing decision must include:

- selected owner
- alternatives (excluding selected)
- reasons for selection
- reasons alternatives were rejected
- confidence score

---

## Override Behavior

If user changes owner:

- persist override
- mark decision as overridden
- keep original recommendation for explainability

---

## Anti-Patterns (DO NOT DO)

- Random selection
- AI choosing owner
- Ignoring workload
- Recomputing silently on UI actions