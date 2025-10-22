using Xunit;

namespace YouTubester.IntegrationTests.TestHost;

[CollectionDefinition(nameof(TestCollection))]
public class TestCollection : ICollectionFixture<TestFixture>
{
}
