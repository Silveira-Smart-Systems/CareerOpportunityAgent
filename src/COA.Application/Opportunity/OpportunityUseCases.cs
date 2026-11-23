using COA.Application.Profile;
using COA.Application.SearchProfiles;
using COA.Domain.Matching;
using COA.Domain.Opportunity;
using OpportunityModel = COA.Domain.Opportunity.Opportunity;
namespace COA.Application.Opportunity;
public interface IOpportunityStore { Task<bool> AddAsync(OwnerIdentity owner, OpportunityModel opportunity, CancellationToken ct=default); Task<IReadOnlyList<OpportunityModel>> ListAsync(OwnerIdentity owner, CancellationToken ct=default); }
public sealed class OpportunityUseCases(IOpportunityStore store, IProfileStore? profileStore = null, IJobSearchProfileStore? searchProfileStore = null)
{
 public async Task<(bool Created, OpportunityModel Opportunity, MatchAssessment Assessment)> ImportAsync(OwnerIdentity owner,string url,string title,string description,CancellationToken ct=default){var o=OpportunityModel.Create(url,title,description);var c=await store.AddAsync(owner,o,ct);return(c,o,await AssessAsync(owner,o,ct));}
 public Task<IReadOnlyList<OpportunityModel>> ListAsync(OwnerIdentity owner,CancellationToken ct=default)=>store.ListAsync(owner,ct);
 public async Task<int> SeedSyntheticDemoAsync(OwnerIdentity owner,CancellationToken ct=default){var items=new[]{("https://jobs.example.test/demo/remote-dotnet","Synthetic demo: Remote .NET Engineer","Synthetic demo opportunity. Remote .NET role."),("https://jobs.example.test/demo/product","Synthetic demo: Product Analyst","Synthetic demo opportunity. Hybrid analyst role.")};var added=0;foreach(var item in items){if(await store.AddAsync(owner,OpportunityModel.Create(item.Item1,item.Item2,item.Item3,true),ct))added++;}return added;}
 public async Task<(OpportunityModel Opportunity, MatchAssessment Assessment)?> GetScoredAsync(OwnerIdentity owner,Guid id,CancellationToken ct=default){var opportunity=(await store.ListAsync(owner,ct)).SingleOrDefault(x=>x.Id==id);return opportunity is null?null:(opportunity,await AssessAsync(owner,opportunity,ct));}
 public async Task<IReadOnlyList<(OpportunityModel Opportunity, MatchAssessment Assessment)>> ListScoredAsync(OwnerIdentity owner,CancellationToken ct=default){var all=await store.ListAsync(owner,ct);var context=await LoadContextAsync(owner,ct);return all.Select(item=>(item,Assess(item,context.Profile,context.Active))).OrderByDescending(x=>x.Item2.Eligible).ThenByDescending(x=>x.Item2.Score).ToArray();}
 private async Task<MatchAssessment> AssessAsync(OwnerIdentity owner,OpportunityModel opportunity,CancellationToken ct){var context=await LoadContextAsync(owner,ct);return Assess(opportunity,context.Profile,context.Active);}
 private async Task<(COA.Domain.Profile.CareerProfile? Profile,COA.Domain.SearchProfiles.JobSearchProfile? Active)> LoadContextAsync(OwnerIdentity owner,CancellationToken ct){var profile=profileStore is null?null:await profileStore.GetCurrentAsync(owner,ct);var active=searchProfileStore is null?null:(await searchProfileStore.ListAsync(owner,ct)).Where(x=>x.IsActive).OrderByDescending(x=>x.UpdatedAtUtc).ThenBy(x=>x.Id).FirstOrDefault();return(profile,active);}
 private static MatchAssessment Assess(OpportunityModel opportunity,COA.Domain.Profile.CareerProfile? profile,COA.Domain.SearchProfiles.JobSearchProfile? active)
 {
  var evidence=new Dictionary<string,EvidenceState>{{"technical",EvidenceState.UnknownNotProven},{"commercial",EvidenceState.UnknownNotProven},{"work-model",EvidenceState.UnknownNotProven},{"seniority",EvidenceState.UnknownNotProven},{"career-alignment",EvidenceState.UnknownNotProven}};
  var text=$"{opportunity.Title} {opportunity.Description}";

  if(profile?.Evidence.Any(x=>x.Claim.Length>2&&text.Contains(x.Claim,StringComparison.OrdinalIgnoreCase))==true)
   evidence["technical"]=EvidenceState.Matched;
  if(active is not null&&active.Preferences.TargetRoles.Concat(active.Preferences.Skills).Any(x=>x.Length>2&&text.Contains(x,StringComparison.OrdinalIgnoreCase)))
   evidence["career-alignment"]=EvidenceState.Matched;

  var workMode=WorkModePreference(profile,active);
  evidence["work-model"]=AssessWorkMode(text,workMode.Modes);
  return ScoringPolicy.Initial.Evaluate(evidence,workMode.IsHardConstraint?["work-model"]:[]);
 }

 private static (IReadOnlySet<COA.Domain.SearchProfiles.SearchWorkMode> Modes,bool IsHardConstraint) WorkModePreference(COA.Domain.Profile.CareerProfile? profile,COA.Domain.SearchProfiles.JobSearchProfile? active)
 {
  var modes=(active?.Preferences.WorkModes ?? []).ToHashSet();
  var hard=modes.Count>0;
  if(profile?.Preferences.RemoteOnly==true)
  {
   if(modes.Count==0)modes.Add(COA.Domain.SearchProfiles.SearchWorkMode.Remote);
   else modes.IntersectWith([COA.Domain.SearchProfiles.SearchWorkMode.Remote]);
   hard=true;
  }
  else if(modes.Count==0&&profile is not null)
   modes.Add(ToSearchWorkMode(profile.Preferences.PreferredArrangement));
  return(modes,hard);
 }

 private static EvidenceState AssessWorkMode(string text,IReadOnlySet<COA.Domain.SearchProfiles.SearchWorkMode> permittedModes)
 {
  if(permittedModes.Count==0)return EvidenceState.UnknownNotProven;
  var supportedModes=new HashSet<COA.Domain.SearchProfiles.SearchWorkMode>();
  if(text.Contains("remote",StringComparison.OrdinalIgnoreCase))supportedModes.Add(COA.Domain.SearchProfiles.SearchWorkMode.Remote);
  if(text.Contains("hybrid",StringComparison.OrdinalIgnoreCase))supportedModes.Add(COA.Domain.SearchProfiles.SearchWorkMode.Hybrid);
  if(text.Contains("onsite",StringComparison.OrdinalIgnoreCase)||text.Contains("on-site",StringComparison.OrdinalIgnoreCase)||text.Contains("on site",StringComparison.OrdinalIgnoreCase))supportedModes.Add(COA.Domain.SearchProfiles.SearchWorkMode.Onsite);
  return supportedModes.Count==0?EvidenceState.UnknownNotProven:supportedModes.Overlaps(permittedModes)?EvidenceState.Matched:EvidenceState.Missing;
 }

 private static COA.Domain.SearchProfiles.SearchWorkMode ToSearchWorkMode(COA.Domain.Profile.WorkArrangement arrangement) => arrangement switch
 {
  COA.Domain.Profile.WorkArrangement.Remote=>COA.Domain.SearchProfiles.SearchWorkMode.Remote,
  COA.Domain.Profile.WorkArrangement.Hybrid=>COA.Domain.SearchProfiles.SearchWorkMode.Hybrid,
  COA.Domain.Profile.WorkArrangement.OnSite=>COA.Domain.SearchProfiles.SearchWorkMode.Onsite,
  _=>throw new ArgumentOutOfRangeException(nameof(arrangement))
 };
}
