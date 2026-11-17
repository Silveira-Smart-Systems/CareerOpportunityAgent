# COA — User Zero & Commercial Validation Plan

Status: Draft for review  
Tracking: #2

## Purpose
Test whether COA reliably improves opportunity evaluation for a real technology professional, then determine whether other professionals want and will pay for it. Do not conflate a functioning demo with market validation.

## Phase A — Baseline (before COA use)
For at least 1–2 weeks or a representative sample of manually reviewed opportunities:
- Record sources, time spent searching and evaluating.
- Record each opportunity's suitability decision and reason.
- Record unsuitable offers missed by simple keyword matches and opportunities wrongly prioritized.
- Assemble a labeled evaluation dataset of at least 30 real opportunities (initial target), preserving vacancy snapshots and human judgments.

## Phase B — User Zero dogfooding
- Use COA daily on real opportunities for an initial 2–4-week observation window.
- Compare COA rankings with manual judgments on the same opportunities.
- Track time saved and which evidence or warnings changed a decision.
- Review all false positives, false negatives, hallucinations, and hard-constraint violations.
- Maintain an issue-backed feedback log and revise scoring only through versioned policies.

## Core metrics
| Metric | Definition | Proposed evaluation |
|---|---|---|
| Precision@5 | Fraction of top five suggestions judged relevant by User Zero | Compare with manually labeled data |
| Hard-constraint violations | Recommended opportunities that violate a configured non-negotiable constraint | Target zero among Priority recommendations |
| Unsupported claims | Assessment claims without supporting profile/vacancy evidence | Target zero material invented claims |
| Time per relevant opportunity | Minutes of search + evaluation divided by relevant opportunities found | Compare baseline vs COA |
| Recommendation acceptance | Share of recommendations User Zero saves or acts on | Track by scoring-policy version |
| Duplicate rate | Repeated opportunities incorrectly surfaced as new | Monitor and investigate |
| Daily run reliability | Successful scheduled runs / planned runs | Track failures and recovery |
| Cost per assessed opportunity | Total model/provider cost / assessed opportunities | Measure for unit economics |

**Thresholds are provisional.** Establish baselines before setting go/no-go numeric targets; do not invent achieved results.

## Phase C — Private beta (5–10 professionals)
Prerequisites: stable end-to-end User Zero workflow, privacy review, no material hallucinations in evaluation set, and acceptable reliability/cost.
- Recruit participants from technology professions with different priorities.
- Collect consent and explain what data is processed by AI providers.
- Measure relevance, retention, usability, and reasons for abandonment.
- Interview participants about alternatives and willingness to pay.
- Avoid making price claims based only on stated interest.

## Phase D — Commercial decision
Evaluate:
1. Repeated real-world usefulness and measurable time savings.
2. Source access legality and sustainability.
3. Willingness to pay and evidence of purchase intent.
4. Expected model, infrastructure, support, and acquisition costs.
5. Ability to support multiple users without excessive manual operations.

Decision: **Go / Iterate / Stop**, with dated evidence recorded in GitHub.

## Pricing experiments
€9, €19, and €39/month are possible research anchors only. Test with interviews, landing-page experiments, and—when appropriate—explicit purchase commitments; never describe these as validated market prices.

## Data governance
- Do not publish CVs, personal contact information, or raw private vacancy/application data in a public GitHub repository.
- Use anonymized/redacted test fixtures and aggregated metrics.
- Document data retention, user consent, provider policies, and deletion flows before private beta.

## Evidence log template
| Date | Cohort | Experiment | Sample | Observation | Evidence link | Decision |
|---|---|---|---|---|---|---|
| TBD | User Zero | Manual baseline | TBD | Not started | TBD | Pending |

## Exit criteria
The User Zero stage ends only when the product repeatedly yields trustworthy, explainable recommendations and the user would prefer it to the manual workflow. Beta and SaaS launches require separate decisions supported by recorded evidence.