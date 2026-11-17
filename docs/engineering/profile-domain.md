# Profile domain — Issue #14

Status: Draft, awaiting review and CI.

## Included
- `CareerProfile`: identity, version, headline, source-linked claims, preferences.
- `ProfileEvidence`: claim, source reference, and origin.
- `CareerPreferences`: work arrangement, remote-only, minimum hourly rate/currency, office-day limits.
- Immutable revisions via `Revise`, which increments version without mutating the prior snapshot.
- Unit tests using synthetic profile data only.

## Design boundaries
This increment intentionally introduces **no unauthenticated profile CRUD endpoint**. The current API has no identity/authorization; exposing private profiles would violate the security boundary. API workflow, authenticated ownership checks, persistence of versions, and integration tests are deferred to follow-up implementation. CV import/parsing is also deferred.

## Known limitations
- Domain revision does not by itself persist historical snapshots; storage must retain each version.
- Currency code checks only syntax, not ISO 4217 membership.
- The preference model is intentionally small and will evolve with real validation.
- No CI results claimed; verify restore/build/test in GitHub Actions before merge.

## Traceability
Partial progress toward FR-01/FR-02 and AC01/AC02 in `docs/product/requirements.md`. Full acceptance of #14 requires authenticated edit workflow, persistent versions, and authorization/integration tests.
