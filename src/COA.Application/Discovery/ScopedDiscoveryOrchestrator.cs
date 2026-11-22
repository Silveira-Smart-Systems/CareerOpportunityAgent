using System.Collections.Concurrent;

namespace COA.Application.Discovery;

/// <summary>
/// Additive scoped execution path. Never converts publication scope to legacy market.
/// The gate alone authorizes execution; a plan is only advisory.
/// </summary>
public sealed class ScopedDiscoveryOrchestrator(
    IScopedOpportunitySourceAdapterResolver adapters,
    IScopedSourceExecutionGate gate,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<ScopedDiscoveryOutcome> ExecuteAsync(
        ScopedDiscoveryPlanningRequest request, ScopedDiscoveryPlan plan,
        DiscoveryExecutionOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ScopedDiscoveryExecutionPlanValidator.Validate(request, plan);

        var targets = plan.EligibleTargets
            .OrderBy(x => x.Target.SourceId.Value, StringComparer.Ordinal)
            .ThenBy(x => x.Target.Scope.ToString(), StringComparer.Ordinal).ToArray();
        var limiter = new SemaphoreSlim(options.MaxConcurrentSourceExecutions);
        var lateOperations = new ConcurrentBag<Task>();
        try
        {
            var tasks = targets.Select(target =>
                ExecuteOneAsync(new ScopedSourceExecutionRequest(target.Target, request.Filters, target.Criteria),
                    options, limiter, lateOperations, cancellationToken)).ToArray();
            var outcomes = await Task.WhenAll(tasks);
            return new ScopedDiscoveryOutcome(outcomes);
        }
        finally
        {
            if (lateOperations.IsEmpty) limiter.Dispose();
            else _ = DisposeWhenLateOperationsCompleteAsync(lateOperations, limiter);
        }
    }

    private async Task<ScopedSourceExecutionOutcome> ExecuteOneAsync(
        ScopedSourceExecutionRequest request, DiscoveryExecutionOptions options,
        SemaphoreSlim limiter, ConcurrentBag<Task> lateOperations, CancellationToken callerToken)
    {
        await limiter.WaitAsync(callerToken);
        var releaseSlot = true;
        try
        {
            var adapter = adapters.Find(request.Target.SourceId);
            if (adapter is null || adapter.SourceId != request.Target.SourceId)
                return Failure(request.Target, SourceExecutionStatus.AdapterMissing, "AdapterMissing");

            ScopedSourceExecutionAuthorization authorization;
            try { authorization = await gate.AuthorizeAndReserveAsync(request, callerToken); }
            catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
            catch { return Failure(request.Target, SourceExecutionStatus.Failed, "GateFailed"); }

            if (authorization is ScopedSourceExecutionAuthorization.Blocked blocked)
                return Failure(request.Target, SourceExecutionStatus.Blocked, blocked.Reason.ToString());
            if (authorization is not ScopedSourceExecutionAuthorization.Granted granted ||
                granted.Permit is null || granted.Permit.Target != request.Target)
                return Failure(request.Target, SourceExecutionStatus.Failed, "InvalidPermit");

            var permit = granted.Permit;
            var started = false;
            try
            {
                callerToken.ThrowIfCancellationRequested();
                try
                {
                    await permit.MarkAttemptStartedAsync(callerToken);
                    started = true;
                }
                catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
                catch { return Failure(request.Target, SourceExecutionStatus.Failed, "PermitStartFailed"); }

                using var timeout = new CancellationTokenSource(options.SourceTimeout, _timeProvider);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(callerToken, timeout.Token);
                Task<ScopedSourceSearchResult> searchTask;
                try { searchTask = adapter.SearchAsync(request, linked.Token); }
                catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
                catch { return Failure(request.Target, SourceExecutionStatus.Failed, "AdapterFailed"); }

                ScopedSourceSearchResult result;
                try
                {
                    var completed = await Task.WhenAny(searchTask,
                        Task.Delay(options.SourceTimeout, _timeProvider, callerToken));
                    callerToken.ThrowIfCancellationRequested();
                    if (completed != searchTask)
                    {
                        releaseSlot = false;
                        lateOperations.Add(ObserveLateAsync(searchTask, limiter));
                        return Failure(request.Target, SourceExecutionStatus.TimedOut, "TimedOut");
                    }
                    result = await searchTask;
                }
                catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
                {
                    if (!searchTask.IsCompleted)
                    {
                        releaseSlot = false;
                        lateOperations.Add(ObserveLateAsync(searchTask, limiter));
                    }
                    throw;
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    if (!searchTask.IsCompleted)
                    {
                        releaseSlot = false;
                        lateOperations.Add(ObserveLateAsync(searchTask, limiter));
                    }
                    return Failure(request.Target, SourceExecutionStatus.TimedOut, "TimedOut");
                }
                catch (OperationCanceledException) { return Failure(request.Target, SourceExecutionStatus.AdapterCancelled, "AdapterCancelled"); }
                catch { return Failure(request.Target, SourceExecutionStatus.Failed, "AdapterFailed"); }

                if (!ValidResult(request, result))
                    return Failure(request.Target, SourceExecutionStatus.InvalidPlan, "AdapterResultInvalid");
                return new ScopedSourceExecutionOutcome(request.Target, SourceExecutionStatus.Succeeded,
                    result.Candidates, []);
            }
            finally
            {
                // A started attempt is consumed even on timeout, failure or caller cancellation.
                if (started) await permit.ConsumeAsync(CancellationToken.None);
                else await permit.ReleaseAsync(CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
        catch { return Failure(request.Target, SourceExecutionStatus.Failed, "PermitLifecycleFailed"); }
        finally { if (releaseSlot) limiter.Release(); }
    }

    private static bool ValidResult(ScopedSourceExecutionRequest request, ScopedSourceSearchResult result) =>
        result is not null && result.Candidates is not null && result.AppliedCriteria is not null &&
        result.Candidates.All(candidate => candidate is not null && candidate.Provenance is not null &&
            candidate.Provenance.SourceId == request.Target.SourceId &&
            candidate.Provenance.RequestedScope == request.Target.Scope &&
            !string.IsNullOrWhiteSpace(candidate.Provenance.ExternalId) &&
            candidate.Provenance.SourceUrl is { IsAbsoluteUri: true } && candidate.Fields is not null) &&
        result.AppliedCriteria.All(applied => request.Criteria.Any(planned => planned.Criterion == applied.Criterion)) &&
        result.AppliedCriteria.Select(applied => applied.Criterion).Distinct().Count() == result.AppliedCriteria.Count &&
        request.Criteria.Where(criterion => criterion.IsMandatory).All(required =>
            result.AppliedCriteria.Any(applied => applied.Criterion == required.Criterion &&
                applied.Application == (required.Disposition == CriterionDisposition.AppliedByProvider
                    ? CriterionApplication.AppliedByProvider : CriterionApplication.AppliedPostFetch)));

    private static ScopedSourceExecutionOutcome Failure(ScopedSourceExecutionTarget target,
        SourceExecutionStatus status, string code) =>
        new(target, status, [], [new ScopedSourceExecutionDiagnostic(target, code)]);

    private static async Task ObserveLateAsync(Task task, SemaphoreSlim limiter)
    {
        try { await task.ConfigureAwait(false); }
        catch { /* Late exceptions observed; timeout already returned. */ }
        finally { limiter.Release(); }
    }

    private static async Task DisposeWhenLateOperationsCompleteAsync(ConcurrentBag<Task> tasks, SemaphoreSlim limiter)
    {
        try { await Task.WhenAll(tasks); }
        finally { limiter.Dispose(); }
    }
}
