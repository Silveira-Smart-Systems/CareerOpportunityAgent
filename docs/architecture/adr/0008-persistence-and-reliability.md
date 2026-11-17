# ADR-0008: SQL Server as system of record and durable processing state

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
Assessments must be reproducible, deduplicated, and recoverable across partial failures and provider timeouts.

## Decision
Use SQL Server for domain records, immutable vacancy snapshots, profile and scoring versions, execution checkpoints, and decisions. Keep external model calls outside long DB transactions. Use idempotent processing, bounded retries, and sanitized observability. Add an outbox only when durable event delivery across boundaries is needed.

## Alternatives considered
Event sourcing for all entities; immediate broker/outbox everywhere; document database as primary store.

## Consequences and trade-offs
Simplifies consistency and audit; requires careful indexing, retention, concurrency, and migration discipline. Vector data, if used, is derived/rebuildable.

## Revisit when
Measured throughput, query patterns, or integration reliability justify additional storage or messaging.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
