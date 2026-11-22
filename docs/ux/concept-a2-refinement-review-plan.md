# COA Concept A2 — Web & Mobile refinement review plan

Status: **Exploratory design brief, not final approval**  
Parent: [#77](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/77)  
Web: [UX-03 #81](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/81)  
Mobile: [UX-04 #82](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/82)  
Evidence: [UX-08 #86](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/86)

## Decision history
- A (Cobalt Workspace) is the current **preferred exploratory direction**, not an approved product design.
- B (Warm Editorial), C (Cobalt Pro), D (Minimal Focus), and E (Midnight Signal) remain comparative alternatives.
- Original A: [Desktop](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=13-8) and [Mobile](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=13-35).
- A2 derivative: [Desktop 37:38](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=37-38), [Mobile 37:72](https://www.figma.com/design/ghHGjigqlIi8Qrk2Puucre?node-id=37-72).
- A2 is a **small incremental exploration**, not a complete high-fidelity redesign. No user sign-off.

## Product design intent
A premium, trustworthy, clear, accessible professional workspace for discovering and assessing opportunities. Prioritize useful evidence and decisions over decorative dashboards.

### Web screen sequence
1. **Overview:** headline, last-refresh provenance, clearly labelled illustrative summary metrics, shortlist and recent changes; no fictitious live provider status.
2. **Opportunities:** searchable/filterable list with role, source status, location/remote, rate disclosure, recency, fit confidence and save/review affordances.
3. **Opportunity details:** source attribution, original description, explicit unknowns, requirements and candidate profile evidence, next action.
4. **Match explanation:** separately identify (a) source job requirements, (b) verified CV/profile evidence, (c) AI inference, (d) missing information; no fabricated scores or guarantees.

### Mobile screen sequence
1. Compact Overview with priority actions and clear navigation.
2. Opportunities list with stacked cards, search and accessible filter sheet.
3. Opportunity detail with progressive disclosure, readable text and sticky primary action only where it does not obscure content.
4. Match explanation accessible from each card, with clear evidence/inference separation and missing-information state.

### Shared design rules
- Maintain the Cobalt Workspace visual direction and semantic hierarchy; tune contrast and spacing after screenshot review.
- Use editable native Figma layers and reusable components; avoid flattened screens.
- Show **synthetic/sample data** and explicit **provider not connected or authorised** notices.
- Never imply real jobs, live sync, validated CV claims or precise match percentages without evidence.
- Keyboard focus and touch targets must be accessible; verify colour contrast and text scaling.
- Account for loading, empty, error, unknown source, missing budget and restricted-provider states.
- Ensure labels do not rely on colour alone; include timestamps and provenance where relevant.
- Design must not authorize provider access or runtime agent execution.

## Acceptance and review gates
- [ ] Desktop and Mobile frames visually inspected; record screenshot dimensions and review date.
- [ ] A versus A2 differences documented with screenshots.
- [ ] Information hierarchy reviewed for Overview, Opportunities, Detail and Match explanation.
- [ ] Contrast, typography, focus, touch targets and responsive behaviour reviewed.
- [ ] Synthetic data and unknown-provider states visible.
- [ ] Versioned PNG evidence and Figma node references published under approved evidence policy.
- [ ] Review comments resolved and explicit product-owner decision recorded.
- [ ] No implementation, merge, deployment, credentials or provider activation without separate approvals.

## Current constraints
Figma MCP Starter call limit blocked a follow-up Mobile screenshot on 2026-11-22. The editable A2 frames were created, but Mobile visual QA and permanent GitHub PNG evidence remain outstanding. Do not mark the evidence or approval gates complete.

## Suggested next bounded design iteration
When Figma access resumes, inspect A2 Desktop/Mobile; correct any spacing/contrast issues; build separate editable Overview, Opportunity Detail and Match Explanation concept frames for Web/Mobile; capture durable evidence and request comparative review. All work remains exploratory and design-only.
