# UX-08 — RFC: Durable design evidence and CI publication

Status: **Proposed / decision required**, not an implemented workflow. Issue #86, parent #77, Draft PR #78.

## Decision proposal

For the MVP, prefer **versioned PNG snapshots committed under `docs/ux/evidence/<issue>/<revision>/`** together with a manifest recording source Figma file key, node ID, capture timestamp, SHA-256 and reviewer context. This makes screenshots available from PR diffs and preserves them with the corresponding Git commit. Keep image sizes bounded and prohibit sensitive content. Reassess if repository growth becomes material.

Alternative: GitHub Actions artifacts are suitable for transient diagnostics, but their retention and access constraints make them insufficient as the **sole** long-term review record. PR comment images are convenient but their provenance and lifecycle must be verified before being treated as canonical. External object storage adds access and retention governance and is deferred.

## Proposed pipeline — not enabled

1. On an explicitly trusted PR workflow, read a versioned manifest (see `design-evidence-manifest-v0.md`). Validate schema and allowlisted Figma file/node IDs against issue metadata.
2. Export only specified Figma frames via an approved API/credential flow. **Never expose credentials to untrusted fork PR code**; avoid `pull_request_target` checkout of untrusted code. Validate the workflow's event and permissions before implementation.
3. Decode PNG, verify dimensions, reasonable maximum size, nonempty content, checksum, and image metadata; capture exact Figma file/node IDs and export time. An export URL is short-lived and is never itself the permanent evidence.
4. Save files to the proposed versioned evidence directory and generate an index Markdown with stable repository links, before/after pairing where relevant, and the issue/PR references.
5. Verify images are present in the PR diff or other approved durable store. A missing, mismatched, inaccessible or stale snapshot is a **blocking failure**, not a warning.
6. A human reviewer assesses design quality, accessibility and product assumptions. CI cannot set the human design approval state.

## Publication model / trust boundary

A PR-origin job should **not** receive a write token or privileged Figma secret by default. Prefer a trusted maintainer-triggered evidence generation workflow with narrow repository write permissions, an allowlisted PR head and exact node manifest, and an auditable commit back to the same design branch. An ordinary read-only PR check then validates the committed images and metadata. A future implementation must separately assess whether the Figma export can be done without secrets in the PR context.

## Proposed naming

```
docs/ux/evidence/81/<revision>/ux-03-web-opportunities.png
docs/ux/evidence/82/<revision>/ux-04-mobile-opportunities.png
docs/ux/evidence/index.md
```

These are **proposed paths, not existing files**. Use a stable immutable revision identifier such as the design checkpoint date + short commit SHA, never overwrite evidence silently.

## Governance / acceptance decisions required

- [ ] Approve repository-committed PNGs as the canonical MVP evidence store and set file size/retention bounds.
- [ ] Choose Figma API authentication and confirm licensing, export permissions, quotas and acceptable use.
- [ ] Approve a trusted manual or maintainer-only evidence export trigger and least-privilege GitHub permissions.
- [ ] Specify checksum verification and provenance representation, including handling of Figma edits after capture.
- [ ] Define how the PR reviewer signs off on each frame/revision, and whether to require CODEOWNERS / branch protection.
- [ ] Decide whether snapshot checks are required for all UX PRs or only those changing visible frames.
- [ ] Validate screenshot accessibility, synthetic data policy and acceptable dimensions.

## Current baseline

UX-03 [Web frame 1:6](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=1-6) (1220×620 export verified) and UX-04 [Mobile frame 1:32](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=1-32) (390×612 export verified). No durable PNGs are yet published; the evidence gate is **not passed**.

This RFC grants **no** authorization to implement CI, provision credentials, merge, deploy or activate providers.
