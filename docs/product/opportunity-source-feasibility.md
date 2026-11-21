# Opportunity source feasibility — Phase 4A

Status: proposed decision — human approval required
Tracking: #46
Related: #45, #47, #53, ADR-0013
Last reviewed: 2026-11-21

## Scope and decision rule

This is a legal/technical feasibility record, not an adapter approval. It compares documented
source access with the product markets (Portugal, Brazil, Europe and international remote work)
and the provider-neutral boundaries in ADR-0013.

The first adapter may proceed only after a human approves its source-specific rights, a controlled
retrieval is authorized, and the observed payload confirms the fields below. A country being
searchable is not evidence that a remote role can be performed from that country. Missing hiring
eligibility is always Unknown.

No credentials were used and no new source request was made for this assessment. The Adzuna
observations below are recovered from the documented controlled evaluation in #46.

## Evidence classification

- **Verified**: provider or public-authority documentation, or the prior sanitized Adzuna observation recorded in #46.
- **Unknown**: not stated in the cited material; it must not be represented as a capability.
- **Hypothesis**: useful to test only after human approval; it is not a product claim.

## Feasibility matrix

| Candidate | Markets / professions | Search, data rights and commercial use | Access, quota and cost | Payload, provenance and freshness | Filter semantics / remote eligibility | Initial decision |
|---|---|---|---|---|---|---|
| **Jobicy Jobs API** | Remote feed; documented geo examples include Europe and global-like Anywhere, but individual PT/BR coverage is **unknown**. Documented industry and keyword filters support several professions. | **Verified:** its fair-use documentation permits commercial products, storage, cache, processing, retention and derived AI outputs when Jobicy is credited and listings are not presented as original COA postings. Keep the Jobicy listing URL for public access. | Public API: no key and no stated monetary price. Commercial direct-ATS URL is optional and billed per available direct URL. Enforce at least one request/second and Retry-After; public feed is limited to a seven-day publication window. | **Verified:** unique listing ID, Jobicy URL, title, company, geo, employment type, full HTML description, publication date and optional salary are documented. Status endpoint distinguishes active/closed/unknown. HTML is untrusted and must be sanitized. | Provider filters: geo, industry, tag, cursor; no documented contract/language/skill/exclusion filter semantics. jobGeo is not proof of work-from-PT/BR eligibility. | **CONDITIONAL GO** for the first adapter evaluation, conditional on human approval of a small public-API retrieval and confirmation of PT/BR relevance, attribution UX and payload observation. |
| **Adzuna Jobs API** | **Verified from #46:** BR and GB search worked; PT returns UNSUPPORTED_COUNTRY. Multi-profession keyword search works, but PT requires another source. | **Verified:** commercial/revenue-generating use after the 14-day evaluation requires written agreement; publishing has attribution/link obligations; termination requires removal of acquired data. No production retention, aggregation or redistribution right is approved. | app_id + app_key required. #46 records 25 test calls in Pending Activation and dashboard options of 5,000/month and 500/day; those are account-specific observations, not a production entitlement or price approval. | **Verified from #46:** ID, title, timestamp, redirect URL and 500-character description snippets in 10/10 BR samples; company absent in 2/10; contract/salary sparse. Old records appeared. Full requirements, redirect destination and ongoing activeness remain unknown. | Keywords/location/pagination are documented. Description snippets are not safe for full evidence matching; no SafePostFilter may be declared. Remote and work-from-country eligibility are unverified/Unknown. | **NO-GO for production; CONDITIONAL GO only for already-approved evaluation.** Obtain written commercial licence and retention/display terms before an adapter. |
| **EURES** | EU/EEA/Switzerland, including Portugal and many professions; vacancy database is updated daily. It does not address Brazil. Titles/free text can remain in the source language. | **Verified:** EURES terms prohibit screen scraping/extraction for further processing or republication. Only EURES partner organisations recognised by a national coordination office may extract via API/similar technology. | Public portal search is available, but there is no verified third-party API entitlement, quota or price for COA. | Portal states each vacancy includes application/contact information and daily updates; external API field access, stable external IDs and reuse rights are unknown for COA. | Portal supports country, region, occupation and contract search. Its cross-border recruitment signal is not proof of the candidate's work/residence eligibility; non-EU labour-market rules still apply. | **NO-GO** unless COA becomes an authorised EURES partner or receives written permission covering API, storage and display. |
| **Remotive Public API** | Remote jobs, international in theme; PT/BR availability and per-vacancy country eligibility are **unknown**. | Public API requires link-back to the returned Remotive URL and Remotive attribution; listings are delayed 24h. It forbids using listings to collect signups/email before display and directs commercial users to a private paid API. General terms also prohibit reproduction/redistribution or commercial job-database use absent written permission. | Public API documentation has no key/cost commitment relevant to COA; private paid API terms, quota and rights are unknown until written offer. | Public API documentation does not establish COA rights for retention, full description or source IDs. Freshness is intentionally delayed by 24h. | Remote label does not establish location eligibility. Filter completeness/semantics for the approved commercial API are unknown. | **NO-GO for the public API in COA's authenticated/commercial model; reassess only after written paid-API rights.** |

## Sources

- [Jobicy Jobs API, fields, rate limit, retention and fair use](https://jobicy.com/jobs-rss-feed)
- [Adzuna API terms](https://developer.adzuna.com/docs/terms_of_service), [overview](https://developer.adzuna.com/overview) and [search API](https://developer.adzuna.com/docs/search)
- [EURES terms shown on Find a Job](https://europa.eu/eures/portal/jv-se/home?lang=en) and [EURES help: coverage, fields and daily update](https://eures.europa.eu/eures-services/help-and-support_en)
- [Remotive Public API terms](https://remotive.com/remote-jobs/api) and [general terms](https://remotive.com/terms-of-use)
- #46: controlled Adzuna BR/GB/PT observations (internal, sanitized; no new calls in Phase 4A)

## Recommendation

Select **Jobicy as CONDITIONAL GO for the first source-adapter evaluation**, not as a production launch authorization. It has the clearest published permission for the intended product operations: display with attribution, commercial use, retention, processing and derived AI outputs. It also documents an ID, Jobicy URL, full description, timestamp, pagination, status endpoint and rate limiting that fit the provenance and lifecycle requirements of ADR-0013.

This does **not** solve the market objective by itself: the public documentation does not prove Portugal or Brazil availability, nor that an Anywhere/regional listing permits work from Portugal or Brazil. Those are explicit acceptance checks for a controlled test, and missing evidence must remain Unknown.

Adzuna remains a useful BR/GB discovery candidate only after a written commercial licence; it is not a PT source and its short descriptions rule it out for full-requirements matching. EURES is valuable market context but is not a lawful COA adapter without partner status/permission. Remotive requires a commercial agreement compatible with an authenticated product before reconsideration.

## Approval gates and first-adapter plan

1. **Human decision:** approve or reject the Jobicy CONDITIONAL GO and confirm that Jobicy attribution and Jobicy-hosted application links are acceptable in the product UX.
2. **Controlled retrieval approval:** perform a small, no-credential public request within the documented one-request-per-second limit. Record only sanitized schema facts: supported geo taxonomy, PT/BR/Europe relevance, ID/URL stability, description HTML, timestamp, salary/contract presence, application-link behaviour, and status endpoint semantics. Do not use the retrieval as a production feed.
3. **Capability record:** turn only observed/documented behaviour into SourceCapabilities. Until then, Contract, Salary, Language, Skill, ExcludedKeyword and RemoteFromCountry remain Unsupported or UnknownSemantics; no SafePostFilter is assumed.
4. **Adapter PR, only after gates 1–3:** Infrastructure-only adapter implementing the existing neutral port, secret-free configuration, attribution/provenance tests, rate-limit/backoff behaviour and fakes. It must preserve provider ID, Jobicy URL, requested market and retrieval timestamp; it must neither schedule, persist, deduplicate, match nor notify.
5. **#47 remains separate:** global catalogue, retention implementation, canonicalisation, deduplication and scheduled ingestion require their own approved work after the source decision.

## Residual risks

- Provider documentation can change; re-check terms immediately before any production activation.
- Jobicy's published seven-day feed can omit still-open jobs; status checks and active-listing policy must be designed before persistence.
- Full descriptions are HTML and external content: sanitize and treat as untrusted.
- A source may expose personal contact data in free text; minimise data and assess data protection before storage.
- Cross-border remote eligibility, salary semantics, employment classification, skills and language completeness must remain Unknown unless verified per source and vacancy.
