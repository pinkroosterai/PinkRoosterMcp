using Xunit;

namespace PinkRooster.Api.Tests.Fixtures;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Integration";
}
