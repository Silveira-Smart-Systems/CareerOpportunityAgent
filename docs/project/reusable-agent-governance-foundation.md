# Reusable agent-delivery governance foundation

**Status:** documentary baseline / proposal for future adoption. COA is the reference implementation context, not a universally approved organization policy. Recorded 2026-11-22. Tracked via UX parent #77 and Draft PR #78; see architecture governance issue #74 for D0 context.

## Purpose

Preserve the COA engineering and product-governance approach as a reusable starting point for new workstreams and products, without assuming that new scopes inherit existing approvals.

## Foundational principles

1. **Single source of truth:** GitHub issues, PRs, versioned docs and explicit decision records are authoritative for work scope and review; specialized tools (e.g. Figma) host their native editable artifacts and are linked bidirectionally.
2. **Agent autonomy within explicit boundaries:** agents may investigate, draft, design, test and propose within approved scope. Autonomous operation is a capability to implement and authorize separately; a role document does not mean an agent is running.
3. **Human accountability:** named accountable human(s) approve consequential scope, provider access, production changes, releases and exceptions. Automated checks cannot impersonate a reviewer.
4. **Evidence before promotion:** each deliverable must have independently reviewable, durable evidence tied to exact version, issue and PR. CI success is evidence of checks run, not universal product approval.
5. **Separation of permissions:** conceptual/documentary approval, design approval, implementation authorization, merge approval, deployment approval and third-party provider permissions are distinct gates. No implicit escalation.
6. **Fail closed on external rights and claims:** unknown provider rights, unverified evidence, missing authorization or unclear provenance are not treated as permission. Use synthetic data until approved.
7. **Security and privacy by default:** least privilege, no credentials or personal data in screenshots/PRs, conservative audit logging, clear provenance, explicit trust boundaries.
8. **Incremental delivery:** small issues and PRs, traceable dependencies, documented trade-offs, reversible changes and explicit unresolved decisions.
9. **Cross-tool traceability:** a design issue links to its Figma frames and the PR; Figma frames identify issue IDs; the PR includes visual snapshots, acceptance criteria and reviewer outcome.
10. **Reusable patterns, not inherited authority:** a new project may copy the templates and process but must separately appoint owners, select tools, approve rights and authorize runtime actions.

## COA-specific baseline and limits

- UX parent: [#77](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/77); UX tasks #79–#86.
- Figma editable source: https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre
- Design evidence policy: `docs/ux/design-pr-evidence-policy.md`.
- Evidence manifest proposal: `docs/ux/design-evidence-manifest-v0.md`.
- Evidence storage RFC: `docs/ux/design-evidence-storage-and-ci-rfc.md`.
- Approved **storage strategy** (recorded in #86): versioned PNGs in GitHub as the canonical MVP design evidence; Figma remains editable source. This is not permission to enable a workflow or merge.
- D0 architecture direction: conceptual/documentary approval only; evidence-class allowlist remains empty and provider ingestion unapproved.

## Reuse checklist for a new workstream

- [ ] Name the product/workstream, business objective, accountable human and initial scope.
- [ ] Create a parent issue, decision log, dependency graph and draft PR workflow.
- [ ] Identify which policies are adopted, changed or rejected; document deviations.
- [ ] Assign agent roles and explicit permissions; distinguish agent specification from operational automation.
- [ ] Define native tool sources (design, code, observability) and their GitHub links.
- [ ] Define durable evidence and reviewer acceptance criteria per deliverable type.
- [ ] Review privacy, provider terms, security, data provenance and operational risks.
- [ ] Record separate human approvals for design, implementation, merge, deploy and provider access.

## Open governance decisions

This document does not appoint organization-wide owners, prescribe a runtime orchestrator, select retention periods, enable GitHub Actions, authorize Figma credentials, activate providers, or approve merge/deploy. A future cross-project standard should be separately proposed and reviewed before adoption.
