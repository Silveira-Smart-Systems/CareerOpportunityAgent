namespace COA.Application.Discovery;

public sealed class DiscoveryPlanner : IDiscoveryPlanner
{
    public DiscoveryPlan CreatePlan(SearchRequest request, DiscoveryPlanningSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        request.Validate();
        snapshot.Validate();

        var sources = snapshot.Sources
            .OrderBy(source => source.Capabilities.SourceId.Value, StringComparer.Ordinal)
            .Select(source => PlanSource(request, source))
            .ToArray();

        return new DiscoveryPlan(sources);
    }

    private static PlannedSource PlanSource(SearchRequest request, SourcePlanningState source)
    {
        var capabilities = source.Capabilities;
        var selectedMarkets = request.SourceMarkets
            .Intersect(capabilities.SupportedSourceMarkets)
            .ToHashSet();
        var criteria = request.MandatoryCriteria
            .OrderBy(criterion => criterion)
            .Select(criterion => CreateCriterion(capabilities, criterion, true))
            .Concat(request.PreferredCriteria
                .OrderBy(criterion => criterion)
                .Select(criterion => CreateCriterion(capabilities, criterion, false)))
            .ToArray();
        var reasons = new List<SourcePlanReason>();

        if (selectedMarkets.Count == 0)
            reasons.Add(SourcePlanReason.UnsupportedMarket);
        if (!source.ExecutionPolicy.IsAuthorized)
            reasons.Add(SourcePlanReason.NotAuthorized);
        if (!source.ExecutionPolicy.LicenseRights.AllowsDiscovery)
            reasons.Add(SourcePlanReason.LicenseDoesNotAllowDiscovery);
        if (source.RuntimeStatus.Health == SourceHealth.Unavailable)
            reasons.Add(SourcePlanReason.SourceUnavailable);
        if (source.RuntimeStatus.Quota != CapacityAvailability.Available)
            reasons.Add(SourcePlanReason.QuotaUnavailable);
        if (source.RuntimeStatus.Budget != CapacityAvailability.Available)
            reasons.Add(SourcePlanReason.BudgetUnavailable);
        if (source.RuntimeStatus.TemporaryRestrictions.Count > 0)
            reasons.Add(SourcePlanReason.TemporarilyRestricted);
        if (criteria.Any(criterion => criterion.IsMandatory &&
            criterion.Disposition is CriterionDisposition.Unsupported or CriterionDisposition.UnknownSemantics))
            reasons.Add(SourcePlanReason.MandatoryCriterionUnsupported);

        var eligible = reasons.Count == 0;
        if (eligible)
            reasons.Add(SourcePlanReason.Eligible);

        return new PlannedSource(
            capabilities.SourceId,
            selectedMarkets,
            eligible,
            reasons,
            criteria);
    }

    private static PlannedCriterion CreateCriterion(
        SourceCapabilities capabilities,
        SearchCriterion criterion,
        bool isMandatory)
    {
        var disposition = capabilities.GetFilterSupport(criterion) switch
        {
            FilterSupport.SupportedByProvider => CriterionDisposition.AppliedByProvider,
            FilterSupport.SafePostFilter => CriterionDisposition.AppliedPostFetch,
            FilterSupport.UnknownSemantics => CriterionDisposition.UnknownSemantics,
            _ => CriterionDisposition.Unsupported
        };

        return new PlannedCriterion(criterion, isMandatory, disposition);
    }
}
