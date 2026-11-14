namespace RmMcp.Tests.Integration;

/// <summary>
/// Shared fixture that holds Docker connection strings for integration tests.
/// Expects Docker containers to be running (docker-compose up from tests/docker).
/// </summary>
public class DockerFixture
{
    public string SqlConnectionString { get; } =
        "Server=localhost;Database=Corpore;User Id=sa;Password=RmMcp@Test2026!;TrustServerCertificate=true";

    public string SoapBaseUrl { get; } = "http://localhost:8051";
    public string SoapUser { get; } = "mestre";
    public string SoapPass { get; } = "totvs";
}

[CollectionDefinition("Docker")]
public class DockerCollection : ICollectionFixture<DockerFixture> { }
