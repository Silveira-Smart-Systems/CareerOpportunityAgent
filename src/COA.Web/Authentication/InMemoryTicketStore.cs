using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace COA.Web.Authentication;

/// <summary>
/// Stores authentication tickets on the server for the local MVP.
/// </summary>
/// <remarks>
/// This store is not suitable for multiple instances and tickets are lost when the application restarts.
/// A distributed, durable implementation is required before deployment beyond the local MVP.
/// </remarks>
public sealed class InMemoryTicketStore(IMemoryCache cache) : ITicketStore
{
    private const string CacheKeyPrefix = "authentication-ticket:";
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromHours(8);

    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var key = Guid.NewGuid().ToString("N");
        SetTicket(key, ticket);
        return Task.FromResult(key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(ticket);

        SetTicket(key, ticket);
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        return Task.FromResult(cache.Get<AuthenticationTicket>(CacheKeyPrefix + key));
    }

    public Task RemoveAsync(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        cache.Remove(CacheKeyPrefix + key);
        return Task.CompletedTask;
    }

    private void SetTicket(string key, AuthenticationTicket ticket)
    {
        var expiration = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(DefaultLifetime);
        cache.Set(CacheKeyPrefix + key, ticket, expiration);
    }
}
