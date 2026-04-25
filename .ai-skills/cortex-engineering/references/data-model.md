# Data Model

## Purpose

Define core domain entities and relationships.

---

## Ticket

Core entity.

Fields include:

- id
- title
- description
- status
- priority
- createdDate
- synitiOwner
- businessOwner
- SLA data
- AI fields

---

## RoutingDecision

Represents system recommendation.

Includes:

- selectedOwner
- alternatives
- reasoning
- confidence
- isOverridden

---

## User

Fields:

- id
- displayName
- role
- department
- eligibility flags

---

## SLA

Includes:

- target time
- breach status
- remaining time

---

## AI Fields

Stored on Ticket:

- AiTriageSummary
- AiTriageSuggestedPriority
- AiTriageMissingDetailsJson
- AiRiskLevel

---

## Relationships

- Ticket → RoutingDecision (1:1)
- Ticket → User (owners)
- Ticket → SLA (computed or stored)

---

## Constraints

- ticket must have valid owner after routing
- routing must respect eligibility
- AI fields must follow system vocabulary

---

## Anti-Patterns

- ❌ duplicating ownership fields
- ❌ storing derived data without reason
- ❌ inconsistent relationships

---

## Golden Rule

Data must reflect reality clearly and consistently