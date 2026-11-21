# Jobicy experimental Discovery adapter

Status: experimental and disabled by default  
Tracking: #60; evidence and licensing decision: #46  
Related architecture: ADR-0013

## Scope

The Jobicy adapter implements only the provider-neutral `IOpportunitySourceAdapter` port. It
returns transient `SourceOpportunityCandidate` values and does not ingest, persist, schedule,
deduplicate globally, match, notify, or expose UI/API endpoints.

The adapter cannot be reached automatically: `Discovery:Jobicy:Enabled` defaults to `false`.
Jobicy documents `geo` as remote-work eligibility geography, not as a source-publication
market. Consequently this phase has **no** country-to-`geo` mapping and the enabled adapter
fails closed before HTTP: it cannot label a broader Jobicy geo response as a `SourceMarket`.
`SourceCapabilities`, execution policy, runtime state and `ISourceExecutionGate` remain the
separate authority that must permit a source-market request.

## Provenance and safety

For valid records the adapter preserves the Jobicy ID, Jobicy-hosted URL, source ID, requested
market and retrieval time. It carries observed title, company, `jobGeo`, publication date,
employment type, optional salary and HTML description with `Present` or `Unknown` field state.

`jobGeo` is advertised geography only. It is not converted to `SourceMarkets` or
`RemoteFromCountry`, and does not establish work-from-Portugal, work-from-Brazil, or any other
residency eligibility. The Europe sample in #46 does not prove PT or BR coverage.

Descriptions are external HTML and must be treated as untrusted. This adapter never renders
them; a future presentation component must sanitize before rendering. It does not log provider
payloads or personal data. Public Jobicy-hosted listing URLs and product attribution remain
required by the documented Jobicy terms; direct ATS URLs are not used.

## Filter and execution policy

No Jobicy search-filter semantics are declared by this implementation. In particular, it does
not claim `SafePostFilter` for any criterion. Until the capability registry has approved
provider semantics, Mandatory Category, TargetRole, WorkMode, RemoteFromCountry, Contract,
Salary, Language, Skill and ExcludedKeyword make the source ineligible or unknown in the
planner, before gate or HTTP.

The adapter defensively refuses a supplied Mandatory criterion marked as provider- or
post-fetch-applied. It returns no `AppliedCriteria` itself.

## HTTP boundary

The documented public jobs endpoint remains isolated behind the adapter transport, but is not
called by this phase because there is no safe source-market query scope. Once a later approved
contract supplies that scope, requests must use cancellation/timeout, a bounded cursor count,
defensive JSON parsing, one bounded retry for 429 respecting the full `Retry-After`, and duplicate
provider-ID suppression within one response sequence. The default process-local limiter keeps
requests one second apart.

The default limiter is suitable only for one execution instance. A multi-instance deployment
must supply a shared `IJobicyRequestRateLimiter` implementation before source enablement; this
PR intentionally does not introduce distributed rate-limit storage or infrastructure.

No public Jobicy request was made while implementing or testing this adapter. All HTTP tests use
an in-memory `HttpMessageHandler`.
