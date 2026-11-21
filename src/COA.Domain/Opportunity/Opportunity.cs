namespace COA.Domain.Opportunity;

public sealed record Opportunity(Guid Id, string OriginalUrl, string CanonicalUrl, string Title, string Description, DateTime ImportedAtUtc)
{
    public static Opportunity Create(string url, string title, string description)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new ArgumentException("A HTTP URL is required.");
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || string.IsNullOrWhiteSpace(description) || description.Length > 12000) throw new ArgumentException("Title and description are required within supported limits.");
        return new(Guid.NewGuid(), url.Trim(), uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant(), title.Trim(), description.Trim(), DateTime.UtcNow);
    }
}
