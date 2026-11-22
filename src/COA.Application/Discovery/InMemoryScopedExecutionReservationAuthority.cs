namespace COA.Application.Discovery;

/// <summary>
/// Immutable, explicitly verified registration. The caller is responsible for verifying
/// source evidence independently; an evidence reference alone cannot create this record.
/// </summary>
public sealed class VerifiedScopedSourceRegistration
{
    public VerifiedScopedSourceRegistration(SourcePlanningState state,
        IEnumerable<SourceExecutionScope> independentlyVerifiedScopes, int availableReservations)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        var scopes = (independentlyVerifiedScopes ??
            throw new ArgumentNullException(nameof(independentlyVerifiedScopes))).ToArray();
        if (scopes.Any(scope => scope is null) || scopes.Distinct().Count() != scopes.Length)
            throw new ArgumentException("Verified scopes must be unique and nonnull.", nameof(independentlyVerifiedScopes));
        if (availableReservations < 0)
            throw new ArgumentOutOfRangeException(nameof(availableReservations));
        VerifiedScopes = Array.AsReadOnly(scopes);
        AvailableReservations = availableReservations;
    }

    public SourcePlanningState State { get; }
    public IReadOnlyList<SourceExecutionScope> VerifiedScopes { get; }
    public int AvailableReservations { get; }
}

/// <summary>
/// A bounded in-memory reference authority. Registration is explicitly trusted and
/// must be replaced with a shared durable transactional authority for multi-pod use.
/// All decisions and slot reservations are serialized under one lock.
/// </summary>
public sealed class InMemoryScopedExecutionReservationAuthority : IScopedExecutionReservationAuthority
{
    private readonly object _sync = new();
    private readonly Dictionary<SourceId, Registration> _registrations = new();

    public void RegisterVerified(VerifiedScopedSourceRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (_sync)
        {
            var id = registration.State.Capabilities.SourceId;
            if (_registrations.ContainsKey(id))
                throw new InvalidOperationException("Source already registered.");
            _registrations.Add(id, new Registration(registration));
        }
    }

    public Task<ScopedSourceExecutionAuthorization> ValidateAndReserveAtomicallyAsync(
        ScopedSourceExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_registrations.TryGetValue(request.Target.SourceId, out var entry) ||
                !entry.Verified.VerifiedScopes.Contains(request.Target.Scope))
                return Block(ScopedSourceExecutionBlockReason.EvidenceNotVerified);

            var state = entry.Verified.State;
            if (!state.ExecutionPolicy.IsAuthorized)
                return Block(ScopedSourceExecutionBlockReason.NotAuthorized);
            if (!state.ExecutionPolicy.LicenseRights.AllowsDiscovery)
                return Block(ScopedSourceExecutionBlockReason.LicenseDenied);
            if (state.RuntimeStatus.Health == SourceHealth.Unavailable)
                return Block(ScopedSourceExecutionBlockReason.SourceUnavailable);
            if (state.RuntimeStatus.TemporaryRestrictions.Count > 0)
                return Block(ScopedSourceExecutionBlockReason.TemporarilyRestricted);
            if (state.RuntimeStatus.Quota != CapacityAvailability.Available)
                return Block(ScopedSourceExecutionBlockReason.QuotaUnavailable);
            if (state.RuntimeStatus.Budget != CapacityAvailability.Available)
                return Block(ScopedSourceExecutionBlockReason.BudgetUnavailable);
            foreach (var criterion in request.Criteria.Where(c => c.IsMandatory))
            {
                var support = state.Capabilities.GetFilterSupport(criterion.Criterion);
                if (support is FilterSupport.UnknownSemantics or FilterSupport.Unsupported)
                    return Block(ScopedSourceExecutionBlockReason.UnsupportedScope);
                if (support == FilterSupport.SupportedByProvider &&
                    criterion.Disposition != CriterionDisposition.AppliedByProvider)
                    return Block(ScopedSourceExecutionBlockReason.UnsupportedScope);
                if (support == FilterSupport.SafePostFilter &&
                    criterion.Disposition != CriterionDisposition.AppliedPostFetch)
                    return Block(ScopedSourceExecutionBlockReason.UnsupportedScope);
            }

            if (entry.Available == 0)
                return Block(ScopedSourceExecutionBlockReason.ReservationUnavailable);
            entry.Available--;
            return Task.FromResult<ScopedSourceExecutionAuthorization>(
                new ScopedSourceExecutionAuthorization.Granted(new Permit(this, entry, request.Target)));
        }
    }

    private static Task<ScopedSourceExecutionAuthorization> Block(ScopedSourceExecutionBlockReason reason) =>
        Task.FromResult<ScopedSourceExecutionAuthorization>(
            new ScopedSourceExecutionAuthorization.Blocked(reason));

    private sealed class Registration(VerifiedScopedSourceRegistration verified)
    {
        public VerifiedScopedSourceRegistration Verified { get; } = verified;
        public int Available { get; set; } = verified.AvailableReservations;
    }

    private sealed class Permit(InMemoryScopedExecutionReservationAuthority owner, Registration entry,
        ScopedSourceExecutionTarget target) : ScopedSourceExecutionPermit
    {
        // 0 reserved, 1 attempt started, 2 consumed, 3 released
        private int _state;
        public override ScopedSourceExecutionTarget Target => target;

        public override Task MarkAttemptStartedAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (owner._sync)
            {
                if (_state != 0) throw new InvalidOperationException("Permit is not reserved.");
                _state = 1;
            }
            return Task.CompletedTask;
        }

        public override Task ConsumeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (owner._sync)
            {
                if (_state != 1) throw new InvalidOperationException("Permit attempt has not started.");
                _state = 2;
            }
            return Task.CompletedTask;
        }

        public override Task ReleaseAsync(CancellationToken cancellationToken)
        {
            // Cleanup must remain possible even when execution was cancelled.
            lock (owner._sync)
            {
                if (_state == 3) return Task.CompletedTask;
                if (_state != 0) throw new InvalidOperationException("Started permits cannot be refunded.");
                _state = 3;
                entry.Available++;
            }
            return Task.CompletedTask;
        }
    }
}
