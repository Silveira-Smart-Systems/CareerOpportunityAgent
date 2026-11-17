# ADR-0001: Modular monolith for the MVP

- **Status:** Proposed — awaiting human review
- **Date:** 2026-11-17
- **Issue:** #4
- **Related:** #2 (product), #3 (architecture), #5 (agent contracts)

## Context
COA is a User Zero product in validation. Early microservices increase deployment, tracing, integration, and cost burdens before domain boundaries are proven.

## Decision
Build a modular monolith with explicit Profile, Opportunities, Matching, Applications, and Agents boundaries. ASP.NET Core API and background Worker may run as separate processes while sharing application/domain modules.

## Alternatives considered
Per-agent microservices; a single unstructured monolith.

## Consequences and trade-offs
Lower operating cost and easier transactions/testing; enforce boundaries through architecture tests and clear ownership. Later extraction requires measured scale, deployment independence, or team ownership needs.

## Revisit when
A module demonstrates independent scaling/deployment needs or isolation requirements that outweigh distributed-system costs.

## Approval
Not yet accepted. Approval and any amendments must be recorded in GitHub before changing status to Accepted.
