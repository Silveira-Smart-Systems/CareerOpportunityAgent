# COA UX-01 / UX-02 — MVP journeys and navigation (proposal)

Status: **design discovery draft**, pending product review. Tracking: #77, Draft PR #78. Mock-only; no Figma file or autonomous runner yet.

## User goals and boundaries

Primary user: a professional seeking relevant remote opportunities with clear requirements, transparent matching rationale and low-friction triage. This is a working design assumption, **not validated user research**.

**Grounded product direction:** opportunity discovery and CV-to-role matching; daily reporting; initial Upwork and LinkedIn focus is a product goal, **not authorization to ingest or use either provider**. Present provider availability honestly.

**Hypotheses requiring separate approval:** saved opportunities, application status tracking, notifications, custom alerts and account-level integration settings.

## Journey J1 — Find and evaluate an opportunity

1. Open overview or opportunities feed.
2. See synthetic opportunity cards: title, organization placeholder, remote status, location/time-zone constraints, contract duration, rate/budget when supplied, source, discovery date.
3. Filter by remote, seniority, contract type, rate and match explanation availability. Unavailable fields must show “Not specified,” not invented values.
4. Open detail. Show requirements and evidence-backed *explanation* of fit, missing requirements, and uncertainty; sample match score must be explicitly marked **illustrative**.
5. Open the original posting only where a legitimate source URL exists; mock prototype uses non-clickable placeholder.
6. Choose “Back to results”; save/triage is visually marked as a **proposed** feature.

States: loading, no results, partial data, unavailable source, expired link, source access not authorized, analysis unavailable.

## Journey J2 — Understand matching

1. From card/detail, open “Why this matches”.
2. Show aligned skills and experience, potential gaps, constraints and provenance/freshness.
3. Clearly distinguish confirmed CV data, extracted job requirements and model-generated inference.
4. Explain uncertainty; do not imply an automated decision is objectively correct.
5. Allow user to inspect the underlying requirement text when permitted; otherwise show a reason for unavailable evidence.

## Journey J3 — Review profile and preferences

1. Open profile/preferences.
2. Review role targets, remote preference, availability, target rate and geographic constraints.
3. Edit controls in mock-only prototype without persistence.
4. Preview how preferences would affect results; label preview illustrative.
5. Source/integration status page shows **not connected / not authorized** by default; no active provider badges.

## Navigation

### Web sitemap
- Overview
- Opportunities
  - Results + filters
  - Opportunity detail
    - Match rationale
- Profile & preferences
- Sources & status
- Proposed later: Saved / Applications / Notifications

### Mobile navigation
- Opportunities (primary)
- Overview (compact)
- Profile
- Sources & status
- Opportunity detail is a pushed screen; filters in bottom sheet; match rationale as an accessible full-screen detail, not a hover-only tooltip.
- Proposed later: Saved / Applications / Notifications

## Wireframe specifications (low fidelity)

### Web W1 — Overview (1440px reference canvas, responsive)
Header: COA logo / navigation / profile.
Main: heading + “Sample data” banner; three summary placeholders (new opportunities, reviewed, analysis unavailable); latest opportunities list; source-status warning; clear primary action “Explore opportunities”.
No production counts or live provider state.

### Web W2 — Opportunity results
Left sidebar: filter controls. Main: result count placeholder and sort; opportunity cards with title, organization, rate “Not specified” fallback, remote/time-zone constraints, source state, requirements excerpt, and “View details”. Empty/error/source-unavailable variants.

### Web W3 — Opportunity detail
Header with title and source transparency; two-column desktop layout: job description/requirements and “Match explanation” with aligned skills, gaps, unknowns and confidence caveat. Back navigation. Never use a fabricated provider link.

### Web W4 — Profile / sources
Profile preferences with mock edit state; separate source status panel: not connected, pending permission review, unavailable. No credential entry or real OAuth.

### Mobile M1 — Feed (390px reference canvas)
Top bar + sample-data banner; search/filter entry; stacked opportunity cards; fixed bottom navigation with accessible labels.

### Mobile M2 — Detail
Back, title, requirements, rate fallback, source status; tab/sections for description and match rationale; large tap targets; no hover dependencies.

### Mobile M3 — Preferences / sources
Simple grouped fields, explanatory copy and explicit mock-state labels. Source status clearly not authorized.

## Design system seed (not approved brand)
- Tokens: spacing 4/8/12/16/24/32; typography scale and contrast targets to be verified in Figma.
- Components: opportunity card, match rationale panel, source-status indicator, filter chip, banner, empty/error state, responsive navigation.
- Accessibility: WCAG 2.2 AA as design target; keyboard focus, semantic reading order, non-color status, reduced motion and touch target sizing.
- All mock data must be synthetic and labelled.

## Review questions / gates

1. Are **Overview**, **Opportunities**, **Profile** and **Sources & status** the right MVP navigation?
2. Should **Saved opportunities** be promoted into MVP scope or remain a hypothesis?
3. Does match explanation need to be the principal detail-screen feature?
4. Is the visual tone professional/neutral, with a later product-brand decision?

**Next after review:** UX-03/04 screen-by-screen wireframe assets; UX-05 tokens; then editable Figma prototype when workspace integration is configured. No implementation, merge, deployment or provider activation is implied.
