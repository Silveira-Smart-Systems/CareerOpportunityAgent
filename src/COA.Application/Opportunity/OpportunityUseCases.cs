using COA.Application.Profile;
using COA.Domain.Matching;
using COA.Domain.Opportunity;
using OpportunityModel = COA.Domain.Opportunity.Opportunity;
namespace COA.Application.Opportunity;
public interface IOpportunityStore { Task<bool> AddAsync(OwnerIdentity owner, OpportunityModel opportunity, CancellationToken ct=default); Task<IReadOnlyList<OpportunityModel>> ListAsync(OwnerIdentity owner, CancellationToken ct=default); }
public sealed class OpportunityUseCases(IOpportunityStore store)
{
 public async Task<(bool Created, OpportunityModel Opportunity, MatchAssessment Assessment)> ImportAsync(OwnerIdentity owner,string url,string title,string description,CancellationToken ct=default){var o=OpportunityModel.Create(url,title,description);var c=await store.AddAsync(owner,o,ct);var e=new Dictionary<string,EvidenceState>{{"technical",description.Contains(".NET",StringComparison.OrdinalIgnoreCase)?EvidenceState.Matched:EvidenceState.UnknownNotProven},{"commercial",EvidenceState.UnknownNotProven},{"work-model",description.Contains("remote",StringComparison.OrdinalIgnoreCase)?EvidenceState.Matched:EvidenceState.UnknownNotProven},{"seniority",EvidenceState.UnknownNotProven},{"career-alignment",EvidenceState.UnknownNotProven}};return(c,o,ScoringPolicy.Initial.Evaluate(e,["work-model"]));}
 public Task<IReadOnlyList<OpportunityModel>> ListAsync(OwnerIdentity owner,CancellationToken ct=default)=>store.ListAsync(owner,ct);
 public async Task<IReadOnlyList<(OpportunityModel Opportunity, MatchAssessment Assessment)>> ListScoredAsync(OwnerIdentity owner,CancellationToken ct=default){var all=await store.ListAsync(owner,ct);return all.Select(x=>{var e=new Dictionary<string,EvidenceState>{{"technical",x.Description.Contains(".NET",StringComparison.OrdinalIgnoreCase)?EvidenceState.Matched:EvidenceState.UnknownNotProven},{"commercial",EvidenceState.UnknownNotProven},{"work-model",x.Description.Contains("remote",StringComparison.OrdinalIgnoreCase)?EvidenceState.Matched:EvidenceState.UnknownNotProven},{"seniority",EvidenceState.UnknownNotProven},{"career-alignment",EvidenceState.UnknownNotProven}};return(Opportunity:x,Assessment:ScoringPolicy.Initial.Evaluate(e,["work-model"]));}).OrderByDescending(x=>x.Assessment.Eligible).ThenByDescending(x=>x.Assessment.Score).ToArray();}
}
