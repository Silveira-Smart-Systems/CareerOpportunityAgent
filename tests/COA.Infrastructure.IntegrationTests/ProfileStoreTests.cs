using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using COA.Application.Profile;
using COA.Domain.Profile;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace COA.Infrastructure.IntegrationTests;

public sealed class ProfileStoreTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task Initial_migration_is_applied_to_an_isolated_database()
    {
        await using var context = await fixture.ContextFactory.CreateDbContextAsync();

        var applied = await context.Database.GetAppliedMigrationsAsync();

        Assert.Contains(applied, migration => migration.EndsWith("_InitialProfilePersistence", StringComparison.Ordinal));
        Assert.StartsWith("COA_Integration_", fixture.DatabaseName);
    }

    [Fact]
    public async Task Create_and_read_round_trip_preserves_preferences_and_evidence_order()
    {
        var owner = Owner("round-trip");
        var profile = Profile("Engineer", Evidence("second"), Evidence("first"));
        var store = fixture.CreateStore();

        var created = await store.CreateAsync(owner, profile);
        var current = await store.GetCurrentAsync(owner);

        Assert.IsType<CreateProfileResult.Created>(created);
        Assert.NotNull(current);
        Assert.Equal(profile.Id, current.Id);
        Assert.Equal(1, current.Version);
        Assert.Equal(80.01m, current.Preferences.MinimumHourlyRate);
        Assert.Equal(new[] { "second", "first" }, current.Evidence.Select(item => item.Claim));
    }

    [Fact]
    public async Task Revision_keeps_the_previous_snapshot_immutable()
    {
        var owner = Owner("history");
        var original = Profile("Engineer", Evidence("C#"));
        var revised = original.Revise("Architect", new[] { Evidence("Azure") }, Preferences());
        var store = fixture.CreateStore();
        await store.CreateAsync(owner, original);

        var result = await store.ReviseAsync(owner, 1, revised);
        var current = await store.GetCurrentAsync(owner);
        var versionOne = await store.GetVersionAsync(owner, 1);

        Assert.IsType<ReviseProfileResult.Revised>(result);
        Assert.Equal(2, current?.Version);
        Assert.Equal("Architect", current?.Headline);
        Assert.Equal(1, versionOne?.Version);
        Assert.Equal("Engineer", versionOne?.Headline);
        Assert.Equal("C#", versionOne?.Evidence.Single().Claim);
    }

    [Fact]
    public async Task Reads_and_revisions_do_not_cross_owner_boundaries()
    {
        var ownerA = Owner("owner-a");
        var ownerB = Owner("owner-b");
        var original = Profile("Private A", Evidence("private"));
        var store = fixture.CreateStore();
        await store.CreateAsync(ownerA, original);

        var currentForB = await store.GetCurrentAsync(ownerB);
        var historyForB = await store.GetVersionAsync(ownerB, 1);
        var reviseAsB = await store.ReviseAsync(
            ownerB,
            1,
            original.Revise("Leaked", new[] { Evidence("leaked") }, Preferences()));

        Assert.Null(currentForB);
        Assert.Null(historyForB);
        Assert.IsType<ReviseProfileResult.NotFound>(reviseAsB);
        Assert.Equal("Private A", (await store.GetCurrentAsync(ownerA))?.Headline);
    }

    [Fact]
    public async Task Issuer_and_subject_are_case_sensitive()
    {
        var firstOwner = new OwnerIdentity("https://issuer.example", "Subject-A");
        var issuerCaseOwner = new OwnerIdentity("https://Issuer.example", "Subject-A");
        var subjectCaseOwner = new OwnerIdentity("https://issuer.example", "subject-a");
        var store = fixture.CreateStore();

        var results = await Task.WhenAll(
            store.CreateAsync(firstOwner, Profile("first")),
            store.CreateAsync(issuerCaseOwner, Profile("issuer-case")),
            store.CreateAsync(subjectCaseOwner, Profile("subject-case")));

        Assert.All(results, result => Assert.IsType<CreateProfileResult.Created>(result));
        Assert.Equal("first", (await store.GetCurrentAsync(firstOwner))?.Headline);
        Assert.Equal("issuer-case", (await store.GetCurrentAsync(issuerCaseOwner))?.Headline);
        Assert.Equal("subject-case", (await store.GetCurrentAsync(subjectCaseOwner))?.Headline);
    }

    [Fact]
    public async Task Owner_key_is_sha256_of_length_prefixed_utf8_identity()
    {
        var owner = Owner("owner-key");
        await fixture.CreateStore().CreateAsync(owner, Profile("owner key"));
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [OwnerKey]
            FROM [ProfileOwners]
            WHERE [Issuer] = @issuer AND [Subject] = @subject
            """;
        command.Parameters.AddWithValue("@issuer", owner.Issuer);
        command.Parameters.AddWithValue("@subject", owner.Subject);

        var storedKey = Assert.IsType<byte[]>(await command.ExecuteScalarAsync());

        Assert.Equal(ExpectedOwnerKey(owner), storedKey);
    }

    [Fact]
    public async Task Concurrent_creation_has_one_success_and_one_verified_conflict()
    {
        var owner = Owner("concurrent-create");
        var firstStore = fixture.CreateStore();
        var secondStore = fixture.CreateStore();

        var results = await Task.WhenAll(
            firstStore.CreateAsync(owner, Profile("first contender")),
            secondStore.CreateAsync(owner, Profile("second contender")));

        Assert.Single(results.OfType<CreateProfileResult.Created>());
        Assert.Single(results.OfType<CreateProfileResult.Conflict>());
        Assert.NotNull(await fixture.CreateStore().GetCurrentAsync(owner));
    }

    [Fact]
    public async Task Unexpected_unique_violation_is_not_mapped_to_conflict()
    {
        var firstOwner = Owner("unexpected-unique-a");
        var secondOwner = Owner("unexpected-unique-b");
        var reusedProfileIdentifier = Profile("duplicate profile id");
        var store = fixture.CreateStore();
        await store.CreateAsync(firstOwner, reusedProfileIdentifier);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            store.CreateAsync(secondOwner, reusedProfileIdentifier));

        Assert.Null(await fixture.CreateStore().GetCurrentAsync(secondOwner));
    }

    [Fact]
    public async Task Concurrent_revision_has_one_success_and_one_conflict()
    {
        var owner = Owner("concurrent-revise");
        var original = Profile("original");
        var setupStore = fixture.CreateStore();
        await setupStore.CreateAsync(owner, original);
        var firstRevision = original.Revise("first", Array.Empty<ProfileEvidence>(), Preferences());
        var secondRevision = original.Revise("second", Array.Empty<ProfileEvidence>(), Preferences());

        var results = await Task.WhenAll(
            fixture.CreateStore().ReviseAsync(owner, 1, firstRevision),
            fixture.CreateStore().ReviseAsync(owner, 1, secondRevision));

        Assert.Single(results.OfType<ReviseProfileResult.Revised>());
        Assert.Single(results.OfType<ReviseProfileResult.Conflict>());
        Assert.Equal(2, (await fixture.CreateStore().GetCurrentAsync(owner))?.Version);
    }

    [Fact]
    public async Task Failure_after_cas_rolls_back_head_and_snapshot()
    {
        var owner = Owner("rollback");
        var original = Profile("before", Evidence("original"));
        var store = fixture.CreateStore();
        await store.CreateAsync(owner, original);
        await fixture.ExecuteSqlAsync("""
            CREATE TRIGGER [TR_ProfileEvidence_ForceFailure]
            ON [ProfileEvidence]
            AFTER INSERT
            AS
            THROW 51000, 'Synthetic integration-test failure.', 1;
            """);

        try
        {
            var revised = original.Revise("after", new[] { Evidence("new") }, Preferences());

            await Assert.ThrowsAsync<DbUpdateException>(() => store.ReviseAsync(owner, 1, revised));
        }
        finally
        {
            await fixture.ExecuteSqlAsync("DROP TRIGGER IF EXISTS [TR_ProfileEvidence_ForceFailure]");
        }

        var current = await fixture.CreateStore().GetCurrentAsync(owner);
        var versionTwo = await fixture.CreateStore().GetVersionAsync(owner, 2);
        Assert.Equal(1, current?.Version);
        Assert.Equal("before", current?.Headline);
        Assert.Null(versionTwo);
    }

    [Fact]
    public async Task Database_constraints_reject_invalid_evidence_origin()
    {
        var owner = Owner("constraints");
        var profile = Profile("constraints");
        await fixture.CreateStore().CreateAsync(owner, profile);
        await using var connection = new SqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO [ProfileEvidence]
                ([ProfileId], [Version], [Ordinal], [Claim], [SourceReference], [Origin])
            VALUES
                (@profileId, 1, 199, N'invalid', N'synthetic', 9)
            """;
        command.Parameters.AddWithValue("@profileId", profile.Id);

        await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
    }

    private static OwnerIdentity Owner(string suffix) =>
        new("https://issuer.example", $"synthetic-{suffix}-{Guid.NewGuid():N}");

    private static CareerProfile Profile(string headline, params ProfileEvidence[] evidence) =>
        CareerProfile.Create(Guid.NewGuid(), headline, evidence, Preferences());

    private static ProfileEvidence Evidence(string claim) =>
        new(claim, $"synthetic-{claim}", EvidenceOrigin.UserProvided);

    private static CareerPreferences Preferences() =>
        new(WorkArrangement.Remote, true, 80.01m, "EUR", 0);

    private static byte[] ExpectedOwnerKey(OwnerIdentity owner)
    {
        var issuer = Encoding.UTF8.GetBytes(owner.Issuer);
        var subject = Encoding.UTF8.GetBytes(owner.Subject);
        var payload = new byte[sizeof(int) + issuer.Length + sizeof(int) + subject.Length];
        var offset = 0;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, sizeof(int)), issuer.Length);
        offset += sizeof(int);
        issuer.CopyTo(payload, offset);
        offset += issuer.Length;
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(offset, sizeof(int)), subject.Length);
        offset += sizeof(int);
        subject.CopyTo(payload, offset);
        return SHA256.HashData(payload);
    }
}
