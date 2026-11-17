# ADR-0003: Hangfire for scheduled processing

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
Daily opportunity intelligence requires scheduled runs, retries, job visibility, and manual reruns without a distributed event platform.

## Decision
Use Hangfire as initial scheduler and background-job host, with persistent state in SQL Server, bounded retries, idempotency keys, and Europe/Lisbon schedule configuration.

## Alternatives considered
HostedService with custom scheduler; Quartz.NET; cloud-native functions and queues.

## Consequences and trade-offs
Simplifies MVP scheduling; Hangfire must not become the domain state machine. Persist business checkpoints independently, handle overlap and partial failures, and monitor dashboard access.

## Revisit when
Scale, isolation, throughput, or provider rate-limit constraints justify a queue/event-driven alternative.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
