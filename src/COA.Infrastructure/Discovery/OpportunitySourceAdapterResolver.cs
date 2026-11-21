using COA.Application.Discovery;

namespace COA.Infrastructure.Discovery;

public sealed class OpportunitySourceAdapterResolver(IEnumerable<IOpportunitySourceAdapter> adapters) : IOpportunitySourceAdapterResolver
{
    private readonly IReadOnlyDictionary<SourceId, IOpportunitySourceAdapter> _adapters =
        (adapters ?? throw new ArgumentNullException(nameof(adapters))).ToDictionary(adapter => adapter.SourceId);

    public IOpportunitySourceAdapter? Find(SourceId sourceId) => _adapters.GetValueOrDefault(sourceId);
}
