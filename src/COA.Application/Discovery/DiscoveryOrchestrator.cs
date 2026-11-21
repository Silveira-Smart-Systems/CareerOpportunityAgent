namespace COA.Application.Discovery;

public sealed class DiscoveryOrchestrator(IOpportunitySourceAdapterResolver adapters, ISourceExecutionGate gate, TimeProvider? timeProvider = null)
    : IDiscoveryOrchestrator
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DiscoveryOutcome> ExecuteAsync(SearchRequest request, DiscoveryPlan plan, DiscoveryExecutionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(options);
        options.Validate(); request.Validate();
        if (!plan.Matches(request)) throw new ArgumentException("The discovery plan does not match the search request.", nameof(plan));

        var targets = plan.EligibleSources.SelectMany(source => source.SelectedMarkets.Select(market => (source, market)))
            .OrderBy(item => item.source.SourceId.Value, StringComparer.Ordinal).ThenBy(item => item.market.Value, StringComparer.Ordinal).ToArray();
        using var limiter = new SemaphoreSlim(options.MaxConcurrentSourceExecutions);
        var tasks = targets.Select(item => ExecuteOneAsync(request, item.source, item.market, options, limiter, cancellationToken)).ToArray();
        var outcomes = await Task.WhenAll(tasks);
        return new DiscoveryOutcome(outcomes.OrderBy(outcome => outcome.SourceId.Value, StringComparer.Ordinal).ThenBy(outcome => outcome.Market.Value, StringComparer.Ordinal).ToArray());
    }

    private async Task<SourceExecutionOutcome> ExecuteOneAsync(SearchRequest request, PlannedSource source, CountryCode market,
        DiscoveryExecutionOptions options, SemaphoreSlim limiter, CancellationToken callerToken)
    {
        await limiter.WaitAsync(callerToken);
        try
        {
            if (!MandatoryCriteriaExecutable(source)) return Outcome(source.SourceId, market, SourceExecutionStatus.InvalidPlan, "MandatoryCriterionNotExecutable");
            var adapter = adapters.Find(source.SourceId);
            if (adapter is null) return Outcome(source.SourceId, market, SourceExecutionStatus.AdapterMissing, "AdapterMissing");
            var target = new SourceExecutionTarget(source.SourceId, market, request, source.Criteria);
            var authorization = await gate.AuthorizeAndReserveAsync(target, callerToken);
            if (authorization is SourceExecutionAuthorization.Blocked blocked)
                return Outcome(source.SourceId, market, SourceExecutionStatus.Blocked, blocked.Reason.ToString());

            var permit = ((SourceExecutionAuthorization.Granted)authorization).Permit;
            var started = false;
            var released = false;
            try
            {
                if (callerToken.IsCancellationRequested)
                {
                    await permit.ReleaseAsync(CancellationToken.None);
                    released = true;
                    callerToken.ThrowIfCancellationRequested();
                }
                await permit.MarkAttemptStartedAsync(callerToken); started = true;
                using var timeout = new CancellationTokenSource(options.SourceTimeout, _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
                SourceSearchResult result;
                try { result = await adapter.SearchAsync(target, linked.Token); }
                catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested) { return Outcome(source.SourceId, market, SourceExecutionStatus.TimedOut, "TimedOut"); }
                catch (OperationCanceledException) { return Outcome(source.SourceId, market, SourceExecutionStatus.AdapterCancelled, "AdapterCancelled"); }
                catch { return Outcome(source.SourceId, market, SourceExecutionStatus.Failed, "AdapterFailed"); }

                if (!MandatoryCriteriaApplied(source, result)) return Outcome(source.SourceId, market, SourceExecutionStatus.InvalidPlan, "MandatoryCriterionUnapplied");
                return new SourceExecutionOutcome(source.SourceId, market, SourceExecutionStatus.Succeeded, result.Candidates, []);
            }
            finally
            {
                if (started) await permit.ConsumeAsync(CancellationToken.None);
                else if (!released) await permit.ReleaseAsync(CancellationToken.None);
            }
        }
        finally { limiter.Release(); }
    }

    private static bool MandatoryCriteriaExecutable(PlannedSource source) => source.IsEligible && source.Criteria.Where(c => c.IsMandatory)
        .All(c => c.Disposition is CriterionDisposition.AppliedByProvider or CriterionDisposition.AppliedPostFetch);
    private static bool MandatoryCriteriaApplied(PlannedSource source, SourceSearchResult result) => source.Criteria.Where(c => c.IsMandatory).All(required =>
        result.AppliedCriteria.Any(applied => applied.Criterion == required.Criterion && (required.Disposition == CriterionDisposition.AppliedByProvider ? applied.Application == CriterionApplication.AppliedByProvider : applied.Application == CriterionApplication.AppliedPostFetch)));
    private static SourceExecutionOutcome Outcome(SourceId source, CountryCode market, SourceExecutionStatus status, string code) =>
        new(source, market, status, [], [new SourceExecutionDiagnostic(source, market, code)]);
}
