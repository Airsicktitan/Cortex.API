# Integrations

## Purpose

Enable Cortex to connect with external systems.

---

## Strategy

Cortex enhances—not replaces—existing tools.

---

## Target Integrations

### Jira / ServiceNow

- import tickets
- sync status
- push routing decisions

---

### SAP (Key Advantage)

- integrate with support workflows
- attach ticket context to SAP issues
- sync ownership

---

### Email

- create tickets from email
- notify users
- send updates

---

## Integration Pattern

- ingestion → normalize → process → output

---

## Requirements

- mapping layer (external → Cortex)
- idempotent operations
- error handling

---

## Anti-Patterns

- ❌ tightly coupling to one system
- ❌ duplicating external data unnecessarily
- ❌ breaking core logic for integrations

---

## Golden Rule

Cortex remains the decision layer