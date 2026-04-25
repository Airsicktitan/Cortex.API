# Test Strategy

## Purpose

Define what must NEVER break.

---

## Core Principle

Test behavior, not implementation.

---

## Critical Test Areas

### 1. Routing

- correct owner selection
- rule priority respected
- eligibility enforced

---

### 2. Rebalance

- suggestions match execution
- invalid suggestions are blocked
- workload improves after execution

---

### 3. AI

- output matches system vocabulary
- no invalid statuses/priorities
- structure is consistent

---

### 4. API Contracts

- responses include required fields
- no breaking changes

---

## Types of Tests

- unit tests (logic)
- integration tests (API + DB)
- scenario tests (end-to-end flows)

---

## Anti-Patterns

- ❌ testing implementation details
- ❌ skipping edge cases
- ❌ relying only on manual testing

---

## Golden Rule

If a decision changes, a test should fail