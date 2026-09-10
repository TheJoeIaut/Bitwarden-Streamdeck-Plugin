using System.Net.Http.Json;
using System.Text;
using CliWrap;
using CliWrap.Buffered;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Newtonsoft.Json;

namespace BitwardenStreamdeckPlugin.Tests.Integration;

/// <summary>
/// Starts a throwaway Vaultwarden server and registers a single test account on it.
/// Vaultwarden speaks the Bitwarden API, so the real 'bw' CLI can talk to it.
/// </summary>
public sealed class VaultwardenFixture : IAsyncLifetime
{
    internal const string Email = "streamdeck-test@example.com";
    internal const string MasterPassword = "correct-horse-battery-staple-42";
    internal const string ItemName = "StreamDeck E2E Entry";
    internal const string ItemUsername = "e2e-user";
    internal const string ItemPassword = "p@ssw0rd with spaces";

    /// <summary>Session key from the one time login.</summary>
    internal string SessionKey { get; private set; } = string.Empty;

    private IContainer? container;

    internal string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// A throwaway BITWARDENCLI_APPDATA_DIR. Every 'bw' invocation in these tests must pass
    /// this, otherwise the CLI reads and writes the developer's real login and server
    /// configuration.
    /// </summary>
    internal IReadOnlyDictionary<string, string> CliEnvironment { get; private set; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Set when the container could not be started (no container runtime available). Tests
    /// report this rather than failing with an opaque connection error.
    /// </summary>
    internal string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        // These tests drive the real 'bw' binary. On a developer machine that binary is
        // usually signed in to a real vault, so running them has to be a deliberate act.
        if (Environment.GetEnvironmentVariable("BW_E2E") != "1")
        {
            SkipReason = "Set BW_E2E=1 to run the Vaultwarden end to end tests";
            return;
        }

        try
        {
            // Every bw invocation gets this explicitly rather than relying on the ambient
            // process environment, so the CLI can never read or write the developer's real
            // login, server configuration or session.
            string dataDirectory = Path.Combine(Path.GetTempPath(), "bw-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDirectory);
            CliEnvironment = new Dictionary<string, string> { ["BITWARDENCLI_APPDATA_DIR"] = dataDirectory };

            container = new ContainerBuilder("docker.io/vaultwarden/server:latest")
                .WithEnvironment("SIGNUPS_ALLOWED", "true")
                .WithEnvironment("ROCKET_PORT", "80")
                // Vaultwarden refuses to start without this when no persistent volume is set.
                .WithEnvironment("I_REALLY_WANT_VOLATILE_STORAGE", "true")
                .WithPortBinding(80, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(80).ForPath("/alive")))
                .Build();

            await container.StartAsync();

            BaseUrl = $"http://{container.Hostname}:{container.GetMappedPublicPort(80)}";

            await RegisterAccount();
            await LoginAndSeed();
        }
        catch (Exception ex)
        {
            SkipReason = $"Vaultwarden container unavailable: {ex.Message}";
        }
    }

    private async Task RegisterAccount()
    {
        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl) };

        object request = BitwardenAccount.BuildRegistrationRequest(Email, MasterPassword, "Stream Deck Test");

        HttpResponseMessage response = await http.PostAsJsonAsync("/identity/accounts/register", request);

        if (!response.IsSuccessStatusCode)
        {
            // Older Vaultwarden builds expose registration under /api instead.
            response = await http.PostAsJsonAsync("/api/accounts/register", request);
        }

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Registering the test account failed ({(int)response.StatusCode}): {body}");
        }
    }

    /// <summary>
    /// Logs the CLI in once and seeds a single login entry. 'bw config server' refuses to
    /// run against an authenticated profile, so this must happen exactly once per fixture
    /// rather than per test.
    /// </summary>
    private async Task LoginAndSeed()
    {
        await Bw(null, "config", "server", BaseUrl);

        SessionKey = (await Bw(null, "login", Email, MasterPassword, "--raw")).Trim();

        if (string.IsNullOrWhiteSpace(SessionKey))
        {
            throw new InvalidOperationException("bw login returned no session key");
        }

        string itemJson = JsonConvert.SerializeObject(new
        {
            type = 1,
            name = ItemName,
            login = new { username = ItemUsername, password = ItemPassword, totp = (string?)null }
        });

        await Bw(SessionKey, "create", "item",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(itemJson)));
        await Bw(SessionKey, "sync");
    }

    /// <summary>
    /// Unlocks again and returns a fresh session key. Used by the test that deliberately
    /// locks the vault, so it can leave the fixture usable for the others.
    /// </summary>
    internal async Task<string> Unlock()
    {
        SessionKey = (await Bw(null, "unlock", MasterPassword, "--raw")).Trim();
        return SessionKey;
    }

    internal async Task<string> Bw(string? sessionKey, params string[] arguments)
    {
        var variables = CliEnvironment.ToDictionary(e => e.Key, e => (string?)e.Value);

        if (!string.IsNullOrEmpty(sessionKey))
        {
            variables["BW_SESSION"] = sessionKey;
        }

        BufferedCommandResult result = await Cli.Wrap("bw")
            .WithArguments(arguments)
            .WithEnvironmentVariables(variables)
            .WithValidation(CommandResultValidation.None)
            .ExecuteBufferedAsync();

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"bw {arguments[0]} failed ({result.ExitCode}): {result.StandardError.Trim()}");
        }

        return result.StandardOutput;
    }

    public async Task DisposeAsync()
    {
        if (container != null)
        {
            await container.DisposeAsync();
        }
    }
}

[CollectionDefinition("Vaultwarden")]
public class VaultwardenCollection : ICollectionFixture<VaultwardenFixture>;
