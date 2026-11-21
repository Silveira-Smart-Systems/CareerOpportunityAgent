namespace COA.Application.Discovery;

using System.Collections.Concurrent;

public sealed class DiscoveryOrchestrator(IOpportunitySourceAdapterResolver adapters, ISourceExecutionGate gate, TimeProvider? timeProvider = null)
    : IDiscoveryOrchestrator
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DiscoveryOutcome> ExecuteAsync(SearchRequest request, DiscoveryPlan plan, DiscoveryExecutionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(plan); ArgumentNullException.ThrowIfNull(options);
        options.Validate(); request.Validate();
        if (!plan.Matches(request)) throw new ArgumentException("The discovery plan does not match the search request.", nameof(plan));
        if (!PlanIsValid(request, plan)) throw new ArgumentException("The discovery plan is not executable for the search request.", nameof(plan));

        var targets = plan.EligibleSources.SelectMany(source => source.SelectedMarkets.Select(market => (source, market)))
            .OrderBy(item => item.source.SourceId.Value, StringComparer.Ordinal).ThenBy(item => item.market.Value, StringComparer.Ordinal).ToArray();
        // A timed-out adapter can still be active. The limiter is deliberately not disposed here:
        // late-operation observers retain it until their slot has been released.
        var limiter = new SemaphoreSlim(options.MaxConcurrentSourceExecutions);
        var lateOperations = new ConcurrentBag<Task>();
        var finished = false;
        try
        {
            var tasks = targets.Select(item => ExecuteOneAsync(request, item.source, item.market, options, limiter, lateOperations, cancellationToken)).ToArray();
            var outcomes = await Task.WhenAll(tasks);
            await Task.WhenAll(lateOperations);
            finished = true;
            return new DiscoveryOutcome(outcomes.OrderBy(outcome => outcome.SourceId.Value, StringComparer.Ordinal).ThenBy(outcome => outcome.Market.Value, StringComparer.Ordinal).ToArray());
        }
        finally
        {
            if (finished || lateOperations.IsEmpty) limiter.Dispose();
            else _ = DisposeAfterLateOperationsAsync(lateOperations, limiter);
        }
    }

    private async Task<SourceExecutionOutcome> ExecuteOneAsync(SearchRequest request, PlannedSource source, CountryCode market,
        DiscoveryExecutionOptions options, SemaphoreSlim limiter, ConcurrentBag<Task> lateOperations, CancellationToken callerToken)
    {
        await limiter.WaitAsync(callerToken);
        var releaseSlot = true;
        try
        {
            if (!MandatoryCriteriaExecutable(source)) return Outcome(source.SourceId, market, SourceExecutionStatus.InvalidPlan, "MandatoryCriterionNotExecutable");
            var adapter = adapters.Find(source.SourceId);
            if (adapter is null) return Outcome(source.SourceId, market, SourceExecutionStatus.AdapterMissing, "AdapterMissing");
            var target = new SourceExecutionTarget(source.SourceId, market, request, source.Criteria);
            SourceExecutionAuthorization authorization;
            try { authorization = await gate.AuthorizeAndReserveAsync(target, callerToken); }
            catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
            catch { return Outcome(source.SourceId, market, SourceExecutionStatus.Failed, "GateFailed"); }
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
                try { await permit.MarkAttemptStartedAsync(callerToken); started = true; }
                catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
                catch { return Outcome(source.SourceId, market, SourceExecutionStatus.Failed, "PermitStartFailed"); }
                using var timeout = new CancellationTokenSource(options.SourceTimeout, _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
                SourceSearchResult result;
                var searchTask = adapter.SearchAsync(target, linked.Token);
                try
                {
                    var completed = await Task.WhenAny(searchTask, Task.Delay(options.SourceTimeout, _timeProvider, callerToken));
                    callerToken.ThrowIfCancellationRequested();
                    if (completed != searchTask)
                    {
                        releaseSlot = false;
                        lateOperations.Add(ObserveLateOperationAsync(searchTask, limiter));
                        return Outcome(source.SourceId, market, SourceExecutionStatus.TimedOut, "TimedOut");
                    }
                    result = await searchTask;
                }
                catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested) { return Outcome(source.SourceId, market, SourceExecutionStatus.TimedOut, "TimedOut"); }
                catch (OperationCanceledException) { return Outcome(source.SourceId, market, SourceExecutionStatus.AdapterCancelled, "AdapterCancelled"); }
                catch { return Outcome(source.SourceId, market, SourceExecutionStatus.Failed, "AdapterFailed"); }

                if (!ResultIsValid(source, market, adapter, result)) return Outcome(source.SourceId, market, SourceExecutionStatus.InvalidPlan, "AdapterResultInvalid");
                return new SourceExecutionOutcome(source.SourceId, market, SourceExecutionStatus.Succeeded, result.Candidates, []);
            }
            finally
            {
                try
                {
                    if (started) await permit.ConsumeAsync(CancellationToken.None);
                    else if (!released) await permit.ReleaseAsync(CancellationToken.None);
                }
                catch when (callerToken.IsCancellationRequested)
                {
                    // The caller's cancellation must remain the observable result.
                }
            }
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
        catch { return Outcome(source.SourceId, market, SourceExecutionStatus.Failed, "PermitLifecycleFailed"); }
        finally { if (releaseSlot) limiter.Release(); }
    }

    private static bool MandatoryCriteriaExecutable(PlannedSource source) => source.IsEligible && source.Criteria.Where(c => c.IsMandatory)
        .All(c => c.Disposition is CriterionDisposition.AppliedByProvider or CriterionDisposition.AppliedPostFetch);
    private static bool MandatoryCriteriaApplied(PlannedSource source, SourceSearchResult result) => source.Criteria.Where(c => c.IsMandatory).All(required =>
        result.AppliedCriteria.Any(applied => applied.Criterion == required.Criterion && (required.Disposition == CriterionDisposition.AppliedByProvider ? applied.Application == CriterionApplication.AppliedByProvider : applied.Application == CriterionApplication.AppliedPostFetch)));
    private static bool ResultIsValid(PlannedSource source, CountryCode market, IOpportunitySourceAdapter adapter, SourceSearchResult result) =>
        adapter.SourceId == source.SourceId && result is not null &&
        result.Candidates.All(candidate => candidate.Provenance.SourceId == source.SourceId && candidate.Provenance.RequestedMarket == market) &&
        result.AppliedCriteria.GroupBy(criterion => criterion.Criterion).All(group => group.Count() == 1) &&
        result.AppliedCriteria.All(applied => source.Criteria.Any(planned => planned.Criterion == applied.Criterion)) &&
        MandatoryCriteriaApplied(source, result);
    private static bool PlanIsValid(SearchRequest request, DiscoveryPlan plan) => plan.Sources.All(source =>
        source.SelectedMarkets.IsSubsetOf(request.SourceMarkets) &&
        source.Criteria.GroupBy(criterion => criterion.Criterion).All(group => group.Count() == 1) &&
        request.MandatoryCriteria.All(criterion => source.Criteria.Any(planned => planned.Criterion == criterion && planned.IsMandatory &&
            planned.Disposition is CriterionDisposition.AppliedByProvider or CriterionDisposition.AppliedPostFetch)) &&
        request.PreferredCriteria.All(criterion => source.Criteria.Any(planned => planned.Criterion == criterion && !planned.IsMandatory)) &&
        source.Criteria.All(planned => planned.IsMandatory ? request.MandatoryCriteria.Contains(planned.Criterion) : request.PreferredCriteria.Contains(planned.Criterion)));
    private static SourceExecutionOutcome Outcome(SourceId source, CountryCode market, SourceExecutionStatus status, string code) =>
        new(source, market, status, [], [new SourceExecutionDiagnostic(source, market, code)]);
    private static async Task ObserveLateOperationAsync(Task operation, SemaphoreSlim limiter)
    {
        try { await operation.ConfigureAwait(false); }
        catch { /* observed: outcome has already been reported as TimedOut */ }
        finally { limiter.Release(); }
    }
    private static async Task DisposeAfterLateOperationsAsync(ConcurrentBag<Task> operations, SemaphoreSlim limiter)
    {
        try { await Task.WhenAll(operations); }
        finally { limiter.Dispose(); }
    }
}
