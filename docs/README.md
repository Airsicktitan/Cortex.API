# Cortex Documentation

This folder contains pilot setup, validation, and product/learning notes for Cortex.

## Pilot Readiness

| Document | Description |
|----------|-------------|
| [pilot-setup.md](pilot-setup.md) | Runbook: bring the stack up, configure Auth0, SQL, migrations, health checks, and first-run sanity. |
| [fresh-install-validation-checklist.md](fresh-install-validation-checklist.md) | Checklist: prove a **clean** install and database—workflows, Cortex rail, reports, empty states. |

---

## Learning / Product Specs

| Document | Description |
|----------|-------------|
| [tier-11-intake-learning-aggregate-spec.md](tier-11-intake-learning-aggregate-spec.md) | Read-only aggregate spec for **Intake Learning** reporting: honest scope from existing relational data, no migration claims. |

---

## Suggested Reading Order

1. [pilot-setup.md](pilot-setup.md)
2. [fresh-install-validation-checklist.md](fresh-install-validation-checklist.md)
3. [tier-11-intake-learning-aggregate-spec.md](tier-11-intake-learning-aggregate-spec.md), if working on learning/reporting

---

## Notes

- **Secrets** must not be committed. Use environment variables, host settings, or `dotnet user-secrets` for local development (see [pilot-setup.md](pilot-setup.md)).
- Placeholder env variable names live in **[`../.env.example`](../.env.example)** at the repository root.
- The root **[`README.md`](../README.md)** links into this documentation packet under **Operations / Pilot Setup**.
