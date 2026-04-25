# Enterprise Hardening

## Purpose

Prepare Cortex for:

- real customers
- production environments
- enterprise expectations
- procurement scrutiny

---

## Core Principle

Enterprise buyers do not buy features.

They buy:

- reliability
- security
- scalability
- control

---

## 1. Security

### Authentication

- Auth0 integration (already present)
- Role-based access control (RBAC)

---

### Requirements

- enforce role permissions at API level
- do not rely on frontend-only checks
- ensure token validation on all requests

---

### Sensitive Data

- never expose secrets in frontend
- use environment variables / user-secrets
- secure connection strings

---

## 2. Authorization Model

Roles must be:

- clearly defined
- enforced consistently
- configurable (not hardcoded)

---

### Example Roles

- Admin
- Syniti Owner
- Business Owner
- Business Manager
- Guest
- User

---

## 3. Data Isolation (Future)

For multi-tenant:

- tenantId on all entities
- query filtering by tenant
- no cross-tenant leakage

---

## 4. Performance

### Requirements

- fast API responses (<200ms typical)
- efficient DB queries
- avoid N+1 queries

---

### Caching

Use for:

- routing snapshots
- workload scoring
- metrics aggregation

---

## 5. Realtime Stability

SignalR must:

- handle reconnects gracefully
- not spam updates
- not trigger full UI reloads

---

## 6. Database

### Requirements

- migrations must be stable
- no breaking schema changes
- stored procedures must be versioned

---

### Resilience

- handle Azure SQL pause/resume
- retry transient failures

---

## 7. Logging & Observability

### MUST HAVE

- structured logging
- error tracking
- request tracing

---

### SHOULD HAVE

- metrics logging (latency, failures)
- audit logs for routing decisions

---

## 8. Auditability

System must be able to answer:

- who changed ownership?
- why was it changed?
- what was the recommendation?

---

## 9. API Design

### Requirements

- consistent endpoints
- clear DTOs
- versioning strategy (future)

---

## 10. Configuration

Everything should be configurable:

- SLA rules
- routing rules
- roles
- AI behavior (model, toggles)

---

## 11. Deployment

### Requirements

- Dockerized services
- environment-specific config
- CI/CD pipeline (GitHub Actions)

---

## 12. Failure Handling

System must:

- fail gracefully
- not crash UI
- surface meaningful errors

---

## 13. UX Stability

Enterprise users expect:

- no data loss
- predictable behavior
- consistent UI

---

## 14. Scaling (Future)

Prepare for:

- increased ticket volume
- more users
- more boards

---

## 15. Anti-Patterns

- ❌ hardcoded business rules
- ❌ frontend-only validation
- ❌ silent failures
- ❌ inconsistent data models
- ❌ non-auditable decisions

---

## 16. Enterprise Readiness Signal

System is ready when:

- behavior is predictable
- decisions are explainable
- data is secure
- performance is stable

---

## Final Rule

If a company cannot trust the system:

They will not buy it.

Trust > Features