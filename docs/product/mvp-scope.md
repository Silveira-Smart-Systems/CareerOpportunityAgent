# COA — MVP Scope

Status: Draft for review  
Tracking: #2

## Intended outcome
Enable User Zero to receive a useful daily shortlist of relevant opportunities, understand each recommendation, record decisions, and optionally prepare a tailored application draft.

## Included in MVP
1. **Profile intelligence:** import CV (PDF/DOCX), extract structured skills and experience with provenance, and allow correction.
2. **Preferences and constraints:** configure minimum compensation, remote/hybrid expectations, geography, role types, and priorities.
3. **Opportunity ingestion:** authorized Upwork API **only if access is approved**; manual LinkedIn URL and vacancy description import.
4. **Normalization/deduplication:** provider IDs and normalized URLs; preserve source, timestamps, and original currency.
5. **Requirements extraction:** distinguish mandatory/preferred requirements and unproven claims.
6. **Explainable assessment:** Technical (40%), Commercial (25%), Work Model (15%), Seniority (10%), Career Alignment (10%) as **initial configurable weights**, not validated optimal values.
7. **Critical review:** independently check constraints, unsupported claims, language, location, and risks.
8. **Daily report:** scheduled evaluation and ranked summary with execution/failure visibility.
9. **Feedback:** shortlist/accept/reject decisions and reasons; history for later evaluation.
10. **Application assistance:** editable English proposal/recruiter message drafts; **never auto-submit**.
11. **Basic dashboard:** profile, opportunities, details, decisions, reports, and drafts.

## Not included
- Automated LinkedIn scraping or browser automation.
- Automatic applications, messages, or recruiter outreach.
- Subscription billing, tenant self-service, or complex multi-tenancy.
- Guaranteed coverage of Upwork or other job platforms.
- Broad occupational taxonomy and support for every profession.
- Fully autonomous decisions or self-modifying agent policies.
- Microservices for individual agents.

## MVP workflow
Profile import and correction → preferences → opportunity ingestion → normalization/dedup → requirements extraction → scoring and critical review → ranked daily report → human decision → optional application draft.

## Delivery assumptions
- .NET 10 / ASP.NET Core, React, SQL Server, Hangfire, Docker for local development, with Azure readiness.
- Modular monolith with agent orchestration and provider-neutral model interfaces.
- NVIDIA NIM is the initial model provider candidate; availability, quota, accuracy, latency, and cost must be benchmarked.
- Embedding, reranking, parsing, and chat models are distinct capabilities.
- All jobs are idempotent where feasible and observable.

## Dependency / fallback
**Upwork API approval is a delivery risk.** Until authorized access is verified, support manual opportunity import as a fallback and do not represent Upwork discovery as operational.

## Release gates
- User Zero can complete the end-to-end workflow on real opportunities.
- Explanations cite actual profile/vacancy evidence.
- Hard constraints and unproven skills are surfaced reliably.
- Repeated ingestion does not produce duplicate active opportunities.
- Daily processing reports failures and does not silently lose opportunities.
- Application submission is impossible through the MVP.
- Validation metrics and test dataset are recorded before beta.

See [Requirements](requirements.md) and [Validation Plan](validation-plan.md).