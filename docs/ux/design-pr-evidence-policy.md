# COA — Design PR evidence policy (proposal)

Status: proposed, pending human review in Draft PR #78. Parent: #77.

## Principle

Every design-related pull request must provide reproducible, reviewable visual evidence of its changes. A passing CI run or a Figma URL alone is **not** design approval. GitHub remains the source of truth for review decisions; Figma contains editable visual artifacts.

## Required evidence in each design PR

1. **Issue and scope:** link UX issue(s), state the affected Web/Mobile screens and whether the work is discovery, wireframe or high fidelity.
2. **Direct Figma frame links:** deep links to the exact editable frame(s), not just the file homepage; mark unavailable/pending links explicitly.
3. **Visual snapshot:** embedded screenshot(s) or reviewable image attachments showing the actual current frame(s). Do not claim screenshots exist when only links are provided.
4. **Before/after:** include both views for revisions; for a new screen label 'new screen — no previous version'.
5. **Acceptance evidence:** short checklist mapping issue criteria to specific frame(s)/states; explicitly identify untested accessibility or responsive requirements.
6. **Coverage:** Web and Mobile where relevant; include representative loading, empty, error, denied/unavailable-source states when those states are in scope.
7. **Review metadata:** date, design version/checkpoint, open assumptions, known limitations, and requested human decision.
8. **Safety of evidence:** synthetic data only; no CV personal data, tokens, provider credentials, fabricated live opportunities or unauthorized provider claims.

## Review states

- **Proposed:** design and evidence available; no approval implied.
- **Reviewed:** reviewer has supplied comments; outstanding actions tracked.
- **Approved:** explicit human product/design approval recorded in the issue or PR, with approved Figma frame/version reference.

A design approval **does not** authorize runtime implementation, provider activation, merge or deployment.

## PR evidence template

### Design evidence
- Related issue(s): #...
- Figma file: ...
- Frame links (Web / Mobile): ...
- Snapshot(s): ... (actual image attachments or paths)
- Before / after: ... (or 'new screen')
- States covered: ...
- Acceptance criteria: [ ] ...
- Accessibility and responsive review: ...
- Open assumptions / limitations: ...
- Requested decision: review / approve / request changes
- Status: **Proposed** (until explicit human review)

## Current baseline — PR #78

The first editable Figma frames are [Web Opportunities](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=1-6) (#81) and [Mobile Opportunities](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=1-32) (#82). These links are **not a substitute for screenshot attachments**. PR #78 remains Draft and must not be described as meeting the full evidence gate until snapshots and acceptance review are present.
