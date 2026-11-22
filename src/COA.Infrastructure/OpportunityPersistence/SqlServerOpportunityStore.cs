using COA.Application.Opportunity;
using COA.Application.Profile;
using COA.Domain.Opportunity;
using COA.Infrastructure.ProfilePersistence;
using Microsoft.EntityFrameworkCore;
namespace COA.Infrastructure.OpportunityPersistence;
internal sealed class OpportunityEntity { public Guid Id { get; set; } public byte[] OwnerKey { get; set; }=[]; public string OriginalUrl { get; set; }=""; public string CanonicalUrl { get; set; }=""; public string Title { get; set; }=""; public string Description { get; set; }=""; public DateTime ImportedAtUtc { get; set; } public bool IsSynthetic { get; set; } }
public sealed class SqlServerOpportunityStore(IDbContextFactory<CoaDbContext> factory) : IOpportunityStore
{
 public async Task<bool> AddAsync(OwnerIdentity owner, Opportunity o,CancellationToken ct=default){await using var db=await factory.CreateDbContextAsync(ct);db.Opportunities.Add(new OpportunityEntity{Id=o.Id,OwnerKey=OwnerKeyFactory.Create(owner),OriginalUrl=o.OriginalUrl,CanonicalUrl=o.CanonicalUrl,Title=o.Title,Description=o.Description,ImportedAtUtc=o.ImportedAtUtc,IsSynthetic=o.IsSynthetic});try{await db.SaveChangesAsync(ct);return true;}catch(DbUpdateException){return false;}}
 public async Task<IReadOnlyList<Opportunity>> ListAsync(OwnerIdentity owner,CancellationToken ct=default){await using var db=await factory.CreateDbContextAsync(ct);var key=OwnerKeyFactory.Create(owner);return await db.Opportunities.Where(x=>x.OwnerKey==key).OrderByDescending(x=>x.ImportedAtUtc).Select(x=>new Opportunity(x.Id,x.OriginalUrl,x.CanonicalUrl,x.Title,x.Description,x.ImportedAtUtc,x.IsSynthetic)).ToListAsync(ct);}
}
