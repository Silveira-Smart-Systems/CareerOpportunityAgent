namespace COA.Application.Discovery;

/// <summary>
/// A provider-neutral planning input. The legacy request supplies search filters only:
/// its SourceMarkets are NOT interpreted as publication countries.
/// </summary>
public sealed class ScopedDiscoveryPlanningRequest
{
    public ScopedDiscoveryPlanningRequest(SearchRequest filters, DiscoveryGeographyIntent geography)
    {
        Filters = filters ?? throw new ArgumentNullException(nameof(filters));
        Geography = geography ?? throw new ArgumentNullException(nameof(geography));
        Filters.Validate();
    }

    public SearchRequest Filters { get; }
    public DiscoveryGeographyIntent Geography { get; }
}

public sealed class ScopedSourcePlanningState
{
    public ScopedSourcePlanningState(SourcePlanningState source, SourceScopeCapabilities scopes)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Scopes = scopes ?? throw new ArgumentNullException(nameof(scopes));
    }

    public SourcePlanningState Source { get; }
    public SourceScopeCapabilities Scopes { get; }
}

public sealed class ScopedDiscoveryPlanningSnapshot
{
    public ScopedDiscoveryPlanningSnapshot(IEnumerable<ScopedSourcePlanningState> sources)
    {
        var copied = (sources ?? throw new ArgumentNullException(nameof(sources))).ToArray();
        if (copied.Any(x => x is null))
            throw new ArgumentException("Null source is not allowed.", nameof(sources));
        if (copied.Select(x => x.Source.Capabilities.SourceId).Distinct().Count() != copied.Length)
            throw new ArgumentException("Duplicate source IDs are not allowed.", nameof(sources));
        Sources = Array.AsReadOnly(copied);
    }

    public IReadOnlyList<ScopedSourcePlanningState> Sources { get; }
}

public enum ScopedSourcePlanReason
{
    Eligible,
    UnsupportedScope,
    MandatoryCriterionUnsupported,
    NotAuthorized,
    LicenseDoesNotAllowDiscovery,
    SourceUnavailable,
    QuotaUnavailable,
    BudgetUnavailable,
    TemporarilyRestricted
}

public sealed class ScopedPlannedTarget
{
    public ScopedPlannedTarget(ScopedSourceExecutionTarget target, IEnumerable<ScopedSourcePlanReason> reasons,
        IEnumerable<PlannedCriterion> criteria)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Reasons = Array.AsReadOnly((reasons ?? throw new ArgumentNullException(nameof(reasons))).ToArray());
        Criteria = Array.AsReadOnly((criteria ?? throw new ArgumentNullException(nameof(criteria))).ToArray());
    }

    public ScopedSourceExecutionTarget Target { get; }
    public IReadOnlyList<ScopedSourcePlanReason> Reasons { get; }
    public IReadOnlyList<PlannedCriterion> Criteria { get; }
    public bool IsEligible => Reasons.Count == 1 && Reasons[0] == ScopedSourcePlanReason.Eligible;
}

public sealed class ScopedDiscoveryPlan
{
    public ScopedDiscoveryPlan(IEnumerable<ScopedPlannedTarget> targets)
    {
        Targets = Array.AsReadOnly((targets ?? throw new ArgumentNullException(nameof(targets))).ToArray());
    }

    public IReadOnlyList<ScopedPlannedTarget> Targets { get; }
    public IReadOnlyList<ScopedPlannedTarget> EligibleTargets => Targets.Where(x => x.IsEligible).ToArray();
}

/// <summary>
/// Pure planner only. Its result is diagnostic and never grants an execution permit.
/// Source capabilities must be registered only after independent evidence verification.
/// </summary>
public sealed class ScopedDiscoveryPlanner
{
    public ScopedDiscoveryPlan CreatePlan(ScopedDiscoveryPlanningRequest request, ScopedDiscoveryPlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        request.Filters.Validate();

        var scopes = request.Geography.PublicationMarket.Kind switch
        {
            PublicationMarketIntentKind.Unconstrained =>
                new SourceExecutionScope[] { new SourceExecutionScope.UnconstrainedPublication() },
            PublicationMarketIntentKind.KnownCountries =>
                request.Geography.PublicationMarket.Countries
                    .OrderBy(x => x.Value, StringComparer.Ordinal)
                    .Select(x => (SourceExecutionScope)new SourceExecutionScope.PublicationCountry(x))
                    .ToArray(),
            _ => throw new ArgumentException("Invalid publication market intent.", nameof(request))
        };

        var targets = snapshot.Sources
            .OrderBy(x => x.Source.Capabilities.SourceId.Value, StringComparer.Ordinal)
            .SelectMany(source => scopes.Select(scope => PlanTarget(request.Filters, source, scope)))
            .ToArray();
        return new ScopedDiscoveryPlan(targets);
    }

    private static ScopedPlannedTarget PlanTarget(SearchRequest request, ScopedSourcePlanningState state,
        SourceExecutionScope scope)
    {
        var source = state.Source;
        var capabilities = source.Capabilities;
        var criteria = request.MandatoryCriteria.OrderBy(x => x)
            .Select(x => Criterion(capabilities, x, true))
            .Concat(request.PreferredCriteria.OrderBy(x => x)
                .Select(x => Criterion(capabilities, x, false)))
            .ToArray();

        var reasons = new List<ScopedSourcePlanReason>();
        if (!state.Scopes.Supports(scope))
            reasons.Add(ScopedSourcePlanReason.UnsupportedScope);
        if (criteria.Any(x => x.IsMandatory &&
            x.Disposition is CriterionDisposition.Unsupported or CriterionDisposition.UnknownSemantics))
            reasons.Add(ScopedSourcePlanReason.MandatoryCriterionUnsupported);
        if (!source.ExecutionPolicy.IsAuthorized)
            reasons.Add(ScopedSourcePlanReason.NotAuthorized);
        if (!source.ExecutionPolicy.LicenseRights.AllowsDiscovery)
            reasons.Add(ScopedSourcePlanReason.LicenseDoesNotAllowDiscovery);
        if (source.RuntimeStatus.Health == SourceHealth.Unavailable)
            reasons.Add(ScopedSourcePlanReason.SourceUnavailable);
        if (source.RuntimeStatus.Quota != CapacityAvailability.Available)
            reasons.Add(ScopedSourcePlanReason.QuotaUnavailable);
        if (source.RuntimeStatus.Budget != CapacityAvailability.Available)
            reasons.Add(ScopedSourcePlanReason.BudgetUnavailable);
        if (source.RuntimeStatus.TemporaryRestrictions.Count != 0)
            reasons.Add(ScopedSourcePlanReason.TemporarilyRestricted);
        if (reasons.Count == 0)
            reasons.Add(ScopedSourcePlanReason.Eligible);

        return new ScopedPlannedTarget(
            new ScopedSourceExecutionTarget(capabilities.SourceId, scope), reasons, criteria);
    }

    private static PlannedCriterion Criterion(SourceCapabilities source, SearchCriterion criterion, bool mandatory) =>
        new(criterion, mandatory, source.GetFilterSupport(criterion) switch
        {
            FilterSupport.SupportedByProvider => CriterionDisposition.AppliedByProvider,
            FilterSupport.SafePostFilter => CriterionDisposition.AppliedPostFetch,
            FilterSupport.UnknownSemantics => CriterionDisposition.UnknownSemantics,
            _ => CriterionDisposition.Unsupported
        });
}
