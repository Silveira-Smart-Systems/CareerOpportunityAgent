# ADR-0006: Human control of applications and outbound actions

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
Application submissions and recruiter messages can have reputational, privacy, and financial consequences.

## Decision
The MVP can generate and edit application drafts only. It must not expose submission or recruiter-messaging capabilities in APIs, workers, adapters, or agents. Human decisions are persisted with timestamps and rationale.

## Alternatives considered
Autonomous job applications; agent approval via prompt only; silent submission after drafting.

## Consequences and trade-offs
Strong safety boundary and simpler compliance posture; user must submit manually. Any future submission capability needs a new ADR, security review, explicit per-action approval, and auditable permissions.

## Revisit when
Validated user demand and a secure, compliant, human-approved submission workflow warrant an explicit new decision.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
