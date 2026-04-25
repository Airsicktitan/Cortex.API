# Workflow Metrics (Executive & Operational)

## Purpose

Metrics in Cortex exist to answer:

"Is this system reducing operational friction and cost?"

NOT:
- raw activity tracking
- vanity dashboards
- technical metrics

---

## Core Value Mapping

All metrics must tie back to:

- fewer follow-ups
- faster decisions
- clearer ownership
- reduced SLA risk

---

## Primary Metric Categories

### 1. Intake Quality

Measures how ready tickets are when created.

#### Metrics:

- % Ready for Review
- % Needs Detail
- Average Missing Details per Ticket
- AI Intake Assist Usage Rate

---

#### Interpretation:

High readiness → fewer back-and-forth cycles  
Low readiness → operational inefficiency

---

### 2. Follow-Up Reduction (MOST IMPORTANT)

Proxy for wasted time.

#### Metrics:

- Average Comments per Ticket
- % Tickets with > X Comments
- Time to First Meaningful Response

---

#### Interpretation:

More comments = more confusion  
Fewer comments = clearer tickets

---

### 3. SLA Risk

Measures operational pressure.

#### Metrics:

- % At Risk
- % Breached
- Average Time to Resolution
- Tickets Near SLA Threshold

---

#### Interpretation:

High SLA risk = poor routing or overload  
Low SLA risk = healthy system

---

### 4. Routing Effectiveness

Measures decision quality.

#### Metrics:

- % Routing Overrides
- Override Reason Distribution
- Average Confidence Score
- % Tickets Routed Without Reassignment

---

#### Interpretation:

High overrides = system mismatch  
Low overrides = routing trust

---

### 5. Workload Balance

Measures fairness and sustainability.

#### Metrics:

- Tickets per Owner (distribution)
- High Priority Tickets per Owner
- Overloaded vs Underutilized Owners
- Rebalance Actions Taken

---

#### Interpretation:

Uneven distribution = burnout risk  
Balanced workload = scalable system

---

### 6. AI Impact (Support Metric)

Measures usefulness of AI (not control).

#### Metrics:

- AI Suggestion Acceptance Rate
- AI Usage Rate
- Avg Missing Details (AI vs No AI)
- SLA Risk (AI-assisted vs non-assisted)

---

#### Interpretation:

AI should improve outcomes, not drive them

---

## UI Requirements

Metrics must be presented as:

### NOT:
- tables of raw data
- generic charts
- technical dashboards

---

### INSTEAD:

- grouped insights
- clear labels
- minimal cognitive load
- executive-readable language

---

## Visualization Rules

### Use:

- stacked bars (readiness distribution)
- simple bar charts (comparisons)
- trend lines (over time)
- small multiples (side-by-side insights)

---

### Avoid:

- overly complex charts
- pie charts with too many segments
- dense tables without filtering

---

## Labeling Rules

### GOOD:

- "Tickets Ready for Review"
- "Follow-Up Required (Comments > 3)"
- "Ownership Overrides"
- "SLA Risk Increasing"

---

### BAD:

- "Metric A"
- "Value Count"
- "System Activity"
- "AI Output Score"

---

## Executive Framing (CRITICAL)

Every metric panel should imply:

"So what?"

Example:

BAD:
"Avg Comments per Ticket: 6.2"

GOOD:
"High Follow-Up Rate  
Tickets require 6.2 comments on average, indicating unclear intake"

---

## Derived Business Value (WHAT YOU SELL)

Metrics should support:

- Reduced meeting time
- Faster decision cycles
- Lower operational overhead
- Improved accountability

---

## Example Insight Blocks

### Intake

"Only 42% of tickets are ready for review  
→ Majority require follow-up, slowing decision flow"

---

### Follow-Up

"Tickets average 5.8 comments  
→ High back-and-forth indicates unclear requests"

---

### Routing

"22% of tickets are manually reassigned  
→ Routing rules may not align with real workload"

---

### SLA

"18% of tickets at risk of breach  
→ Workload imbalance or delayed ownership"

---

## Filtering & Interactivity

All reports must support:

- filtering by board
- filtering by owner
- filtering by status
- date range selection

---

## Anti-Patterns (DO NOT DO)

- ❌ show raw DB data without interpretation
- ❌ expose technical fields (IDs, enums)
- ❌ overload screen with charts
- ❌ create dashboards without clear purpose
- ❌ mix operational and technical metrics

---

## Golden Rule

If a metric cannot answer:

"What decision should be made from this?"

It should not exist.

---

## Demo Readiness Rule

A stakeholder should be able to:

- look at the metrics page
- understand the problem
- understand the impact
- see the value

within 30 seconds.

---

## Connection to Cortex Story

Metrics must reinforce:

Cortex reduces:

- unclear ownership
- unnecessary follow-ups
- SLA risk

And enables:

- faster decisions
- better routing
- measurable operational improvement