# UX-08 — Design evidence manifest v0 (proposal)

Status: documentary proposal for #86; no workflow or credential provisioned. Parent #77; Draft PR #78.

## Contract

A design PR carries a small manifest that identifies exactly which Figma frames and issue acceptance criteria it intends to evidence. The manifest is **metadata, not proof**; screenshots must be exported, verified and durably published separately.

### Illustrative YAML

```yaml
schemaVersion: 0 # proposal; not a production contract
pullRequest: 78
reviewState: proposed # proposed | reviewed | approved
figma:
  fileKey: ghHGjigqlIi8Qrk2Puucre
frames:
  - issue: 81
    platform: web
    nodeId: '1:6'
    label: opportunities-list
    kind: new-screen
    expectedScreenshot: ux-03-web-opportunities.png
    evidenceStatus: pending-publication
    acceptance: [navigation, sample-data-disclosure]
  - issue: 82
    platform: mobile
    nodeId: '1:32'
    label: opportunities-feed
    kind: new-screen
    expectedScreenshot: ux-04-mobile-opportunities.png
    evidenceStatus: pending-publication
    acceptance: [navigation, sample-data-disclosure]
```

**Important:** filenames are intended outputs, not existing repository files. Screenshot bytes are not yet committed or attached.

## Validation rules proposed for a future implementation

- Each frame entry requires an existing issue, Figma file key, exact node ID, platform, descriptive label and declared review state.
- An export succeeds only if a valid PNG is produced and retained at a stable URL/path, with a digest and timestamp in the published evidence index.
- Missing export, empty PNG, stale URL, mismatched node ID, inaccessible artifact or missing issue mapping must fail the evidence gate, not merely warn.
- New screens declare `kind: new-screen`; revisions include links to both before and after evidence and identify the baseline version.
- The PR body must show the frame-level Figma links, screenshots or stable artifact links, acceptance checklist, limitations and review request.
- The reviewer confirms content is synthetic and does not expose CV details, provider secrets, credentials or unlicensed live listings.
- CI status cannot promote `reviewState` to `approved`; human approval is explicit and separately recorded.
- Retention, artifact store, credential mechanism, export API quotas, fork-PR trust boundaries and accessibility coverage remain open decisions before runtime automation.

## Evidence checklist for PR #78

- [x] Figma file and exact Web/Mobile frame IDs identified
- [x] Initial screenshot export manually verified (Web 1220×620; Mobile 390×612)
- [ ] PNG bytes durably attached or committed
- [ ] Stable URLs and checksums recorded
- [ ] Criteria reviewed against screenshots
- [ ] Human design approval recorded

No merge, deploy or provider activation authorized by this document.
