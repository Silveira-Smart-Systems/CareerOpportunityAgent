using System.Text.Json;
using COA.Domain.Matching;
using COA.Web.Api;
using Xunit;

namespace COA.Web.Tests;
public sealed class CoaApiJsonContractTests
{
    [Fact]
    public void Api_string_evidence_states_deserialize()
    {
        const string json = """[{"opportunity":{"id":"00000000-0000-0000-0000-000000000001","originalUrl":"https://jobs.example.test/1","canonicalUrl":"https://jobs.example.test/1","title":"Demo","description":"Synthetic","importedAtUtc":"2026-01-01T00:00:00Z","isSynthetic":true},"assessment":{"score":50,"eligible":false,"criteria":[{"code":"a","evidence":"matched","weight":1,"isHardConstraint":false},{"code":"b","evidence":"missing","weight":1,"isHardConstraint":false},{"code":"c","evidence":"unknownNotProven","weight":1,"isHardConstraint":true}]}}]""";
        var result = JsonSerializer.Deserialize<List<ScoredOpportunity>>(json, CoaApiClient.ApiJsonOptions);
        Assert.Equal([EvidenceState.Matched, EvidenceState.Missing, EvidenceState.UnknownNotProven], result![0].Assessment.Criteria.Select(x => x.Evidence));
    }
}
