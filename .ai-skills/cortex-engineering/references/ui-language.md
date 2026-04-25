# 🧠 UI Language Standards — Cortex

## 🎯 Purpose
Ensure all user-facing language is:
- clear
- intentional
- decision-focused
- enterprise-ready

Cortex UI must communicate **decisions and outcomes**, not system behavior.

---

## 🧠 Core Principle

Cortex does NOT:
- describe system state
- expose internal limitations

Cortex DOES:
- explain decisions
- guide action
- communicate impact

---

## ❌ Avoid (System Language)

Never use:
- assigned (when assignment failed)
- unavailable
- invalid
- no data
- system-generated phrasing

These create:
- confusion
- distrust
- perception of instability

---

## ✅ Preferred Language Patterns

### Ownership

❌ No owner assigned  
✅ No clear owner identified  

---

### Confidence

❌ Limited routing signals  
✅ Low confidence — insufficient routing signals  

---

### Missing Information

❌ Not specified in draft  
✅ Needs clarification  

---

### Workload

❌ Workload unavailable  
✅ Workload data not available for comparison  

---

### Rebalance

❌ No executable actions  
✅ No safe improvements available  

---

### Blocking / Safety

❌ Would create ping-pong  
✅ Move blocked to prevent repeated reassignment  

---

## 🧩 UI Intent Mapping

Every UI element must answer ONE of these:

1. What should happen?
2. Why was this chosen?
3. What happens if we do it?
4. Why was something NOT chosen?

If it does not answer one of these → it should not exist

---

## 📊 Metrics Language

Metrics must:
- explain meaning
- connect to operational impact

### Examples

❌ 0% ready  
✅ 0% ready for review  

❌ 0.0 comments  
✅ Average follow-up: 0.0 comments  

❌ 0% saved  
✅ Cortex Assist impact: 0% time saved  

---

## ⚖️ Decision System Alignment

All wording must align with:

- decision-engine.md
- rebalance.md
- metrics.md

UI should reflect:
- deterministic routing
- advisory AI
- workload-aware decisions

---

## 🚫 Anti-Patterns

Do NOT:
- expose internal errors
- show raw IDs or system placeholders
- use vague phrasing (“something failed”)
- use developer terminology

---

## ✅ Expected Outcome

Cortex should feel like:

- a decision system
- an operational tool
- a trusted advisor

NOT:
- a technical dashboard
- a system status viewer