using COA.Application.Discovery;
using COA.Infrastructure.Discovery;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace COA.Infrastructure.Discovery.Jobicy;

public static class JobicyDiscoveryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the experimental adapter only. This does not register an execution trigger,
    /// source capability, policy or gate, and its default configuration remains disabled.
    /// </summary>
    public static IServiceCollection AddExperimentalJobicyDiscovery(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new JobicyAdapterOptions();
        configuration.GetSection(JobicyAdapterOptions.SectionName).Bind(options);
        options.Validate();

        services.AddSingleton(options);
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IJobicyRequestRateLimiter, InProcessJobicyRequestRateLimiter>();
        services.AddHttpClient(JobicyAdapterOptions.HttpClientName, (provider, client) =>
        {
            client.BaseAddress = provider.GetRequiredService<JobicyAdapterOptions>().BaseAddress;
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        services.TryAddSingleton<JobicyJobsTransport>();
        services.TryAddSingleton<JobicyOpportunitySourceAdapter>();
        services.AddSingleton<IOpportunitySourceAdapter>(provider => provider.GetRequiredService<JobicyOpportunitySourceAdapter>());
        services.TryAddSingleton<IOpportunitySourceAdapterResolver, OpportunitySourceAdapterResolver>();
        return services;
    }
}
