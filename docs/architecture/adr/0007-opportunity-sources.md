# ADR-0007: Authorized sources and manual import first

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
Job-platform terms and API access may constrain automated discovery. The MVP must not depend on unapproved scraping.

## Decision
Use an opportunity-source port. Enable official Upwork ingestion only after approved access, permissions, quotas, and usage terms are verified. Support manual LinkedIn URL and description import; do not scrape LinkedIn. Persist provider provenance, canonical identity, and immutable source snapshots.

## Alternatives considered
Browser scraping; unofficial APIs; paid aggregators from day one; only manual imports forever.

## Consequences and trade-offs
Reduces compliance and maintenance risk but may constrain coverage. Source coverage and ingestion costs are explicit product-validation risks.

## Revisit when
An authorized, sustainable source with favorable coverage and economics becomes available.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
