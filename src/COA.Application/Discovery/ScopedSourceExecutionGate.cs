namespace COA.Application.Discovery;

/// <summary>
/// A single trusted authority must evaluate the current verified scope registration,
/// authorization, license, health, restrictions and capacity AND reserve quota/budget
/// atomically. A planner snapshot or an evidence-reference string is not sufficient.
/// </summary>
public interface IScopedExecutionReservationAuthority
{
    Task<ScopedSourceExecutionAuthorization> ValidateAndReserveAtomicallyAsync(
        ScopedSourceExecutionRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Fail-closed scoped gate boundary. Deliberately does not adapt to the legacy gate:
/// its CountryCode market cannot represent unconstrained publication execution.
/// </summary>
public sealed class ScopedSourceExecutionGate : IScopedSourceExecutionGate
{
    private readonly IScopedExecutionReservationAuthority _authority;

    public ScopedSourceExecutionGate(IScopedExecutionReservationAuthority authority)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
    }

    public async Task<ScopedSourceExecutionAuthorization> AuthorizeAndReserveAsync(
        ScopedSourceExecutionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // No provider call or permit can be issued without the trusted atomic authority.
        var authorization = await _authority.ValidateAndReserveAtomicallyAsync(request, cancellationToken);
        if (authorization is null)
            throw new InvalidOperationException("The scoped reservation authority returned no decision.");

        if (authorization is ScopedSourceExecutionAuthorization.Granted granted)
        {
            if (granted.Permit is null || granted.Permit.Target != request.Target)
                throw new InvalidOperationException("The scoped reservation authority returned an invalid permit.");
        }
        else if (authorization is not ScopedSourceExecutionAuthorization.Blocked)
        {
            throw new InvalidOperationException("The scoped reservation authority returned an unknown decision.");
        }

        return authorization;
    }
}
