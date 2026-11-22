namespace COA.Application.Discovery;

/// <summary>
/// Validates a scoped plan before any gate or adapter is invoked. A legacy market is
/// never a publication-country execution scope, even when the country code matches.
/// This validator does not authorize execution or verify provider evidence.
/// </summary>
public static class ScopedDiscoveryExecutionPlanValidator
{
    public static void Validate(ScopedDiscoveryPlanningRequest request, ScopedDiscoveryPlan plan)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        request.Filters.Validate();

        var expectedScopes = request.Geography.PublicationMarket.Kind switch
        {
            PublicationMarketIntentKind.Unconstrained =>
                new SourceExecutionScope[] { new SourceExecutionScope.UnconstrainedPublication() },
            PublicationMarketIntentKind.KnownCountries =>
                request.Geography.PublicationMarket.Countries
                    .Select(country => (SourceExecutionScope)new SourceExecutionScope.PublicationCountry(country))
                    .ToArray(),
            _ => throw new ArgumentException("Unknown publication market intent.", nameof(request))
        };

        var identities = new HashSet<ScopedSourceExecutionTarget>();
        foreach (var planned in plan.Targets)
        {
            if (planned is null || planned.Target is null || !identities.Add(planned.Target))
                throw new ArgumentException("Scoped plan contains null or duplicate targets.", nameof(plan));
            if (!expectedScopes.Contains(planned.Target.Scope))
                throw new ArgumentException("Scoped plan widens or changes the requested publication scope.", nameof(plan));
            if (planned.Reasons.Count == 0 ||
                (planned.Reasons.Contains(ScopedSourcePlanReason.Eligible) && !planned.IsEligible))
                throw new ArgumentException("Scoped plan has inconsistent eligibility reasons.", nameof(plan));

            var criteria = planned.Criteria;
            if (criteria.Any(x => x is null) ||
                criteria.Select(x => x.Criterion).Distinct().Count() != criteria.Count ||
                !request.Filters.MandatoryCriteria.All(x =>
                    criteria.Any(c => c.Criterion == x && c.IsMandatory)) ||
                !request.Filters.PreferredCriteria.All(x =>
                    criteria.Any(c => c.Criterion == x && !c.IsMandatory)) ||
                criteria.Any(x => x.IsMandatory
                    ? !request.Filters.MandatoryCriteria.Contains(x.Criterion)
                    : !request.Filters.PreferredCriteria.Contains(x.Criterion)))
                throw new ArgumentException("Scoped plan criteria do not match the request.", nameof(plan));

            if (planned.IsEligible && criteria.Any(x => x.IsMandatory && x.Disposition is not
                (CriterionDisposition.AppliedByProvider or CriterionDisposition.AppliedPostFetch)))
                throw new ArgumentException("Scoped plan has nonexecutable mandatory criteria.", nameof(plan));
        }
    }
}
