# ADR-0014: Separate Discovery publication, advertised geography and remote eligibility

- **Status:** Accepted
- **Date:** 2026-11-22
- **Issue:** #63
- **Related:** #46, #60, #62, ADR-0013

## Context

ADR-0013 models `SearchRequest.SourceMarkets` as ISO country markets and carries the selected
market through planning, execution and provenance. Jobicy documents its `geo` parameter and
`jobGeo` response field as advertised remote-work geography. Neither is evidence of a vacancy's
publication country or that a candidate may work remotely from a country.

Equating `GB` with `europe`, `PT` with `portugal`, or `Anywhere` with no constraint would widen a
search and fabricate provenance. These concepts must remain separate for every provider and
profession.

## Decision

The Application has provider-neutral contracts for three independent axes:

1. `PublicationMarketIntent` is either `KnownCountries` or `Unconstrained`. Known requires one
   or more `CountryCode` values. Unconstrained means no publication-country filter was requested;
   it is not an observation of Unknown.
2. `AdvertisedGeographicScope` is `Country`, `Region`, or `Anywhere`. The initial controlled
   region taxonomy contains only `Europe`. A scope is never converted to another scope or a
   publication market implicitly.
3. `PublicationMarketProvenance` is `Known(country)` or `Unknown`. Unknown never satisfies a
   known publication-market constraint. `RemoteWorkEligibilityByCountry` records `Eligible`,
   `NotEligible`, or `Unknown` per country. Missing evidence is Unknown and cannot be derived
   from advertised geography.

`DiscoveryGeographyIntent` combines publication intent with zero or more advertised scopes. It
is intentionally independent from legacy `SearchRequest` in this phase. Existing
`SearchRequest.SourceMarkets`, constructors, validation, equality, planner, orchestrator, gate
and provenance remain unchanged and therefore retain their existing semantics.

The three collection-bearing contracts are sealed classes, not value-equal records:
`PublicationMarketIntent.HasSameIntent`, `DiscoveryGeographyIntent.HasSameIntent` and
`RemoteWorkEligibilityByCountry.HasSameObservations` are the explicit semantic comparators.
This avoids reference-based equality of frozen collections appearing to be value equality. Every
new country boundary rejects `default(CountryCode)` as well as invalid country values.

`RemoteWorkEligibilityByCountry` is an immutable snapshot of values only. It carries neither
source evidence nor observation time; a future `SourceProvenance` evolution must own those
metadata. Its values alone never authorize a Mandatory filter.

## Consequences

- The current Jobicy adapter remains disabled and fail-closed. This ADR adds no capability and
  does not authorize a `geo` query.
- `Country(PT)`, `Region(Europe)` and `Anywhere` are distinct typed values. New regions require a
  product decision and an ADR update or successor.
- Mandatory publication/geography/eligibility behavior will be attached to source capabilities
  only in a later approved Planner/Orchestrator/Provenance phase. Until then no
  `SafePostFilter` is declared and unknown semantics block execution.
- A future additive `SearchRequest` overload may accept `DiscoveryGeographyIntent` only together
  with a coordinated planner/execution-scope/provenance evolution. No country placeholder is
  permitted to satisfy the current `SourceMarkets` invariant.

## Migration and validation

The first delivery introduces immutable value contracts and unit tests only. The next phase must
add capability evidence, planning diagnostics, execution scopes and provenance fields together,
with legacy overloads translating only existing known source markets. Ingestion, persistence,
deduplication and Jobicy activation remain outside this ADR.
