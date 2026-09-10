using BitwardenStreamdeckPlugin.Models;
using Newtonsoft.Json;
using Xunit;

namespace BitwardenStreamdeckPlugin.Tests.Integration;

/// <summary>
/// Drives the real Bitwarden CLI against a real Vaultwarden server, so the plugin's
/// command arguments and output parsing are checked against the actual thing rather than
/// against recorded fixtures.
///
/// Requires a container runtime, the 'bw' CLI on PATH, and BW_E2E=1. See the README.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Vaultwarden")]
public class VaultwardenE2ETests(VaultwardenFixture fixture)
{
    private BwCli Cli()
    {
        var cli = new BwCli(environment: fixture.CliEnvironment);
        cli.SetSessionKey(fixture.SessionKey);
        return cli;
    }

    [SkippableFact]
    public async Task The_plugin_reads_a_real_vault_entry_through_the_real_cli()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        string output = await Cli().Run("get", "item", VaultwardenFixture.ItemName);
        Item item = Get.ParseItem(output);

        Assert.Equal(VaultwardenFixture.ItemUsername, item.UserName);
        Assert.Equal(VaultwardenFixture.ItemPassword, item.Password);
    }

    [SkippableFact]
    public async Task The_property_inspector_item_list_parses_real_cli_output()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        string output = await Cli().Run("list", "items");
        List<ItemListDto>? items = JsonConvert.DeserializeObject<List<ItemListDto>>(output);

        Assert.NotNull(items);
        Assert.Contains(items!, i => i.ItemName == VaultwardenFixture.ItemName);
        Assert.All(items!, i => Assert.NotEqual(Guid.Empty, i.ItemId));
    }

    [SkippableFact]
    public async Task Without_a_session_key_the_wrapper_throws_rather_than_returning_junk()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        // This is how the plugin behaves before the Unlock action has run.
        var cli = new BwCli(environment: fixture.CliEnvironment);

        BwCliException error = await Assert.ThrowsAsync<BwCliException>(
            () => cli.Run("get", "item", VaultwardenFixture.ItemName));

        Assert.NotEqual(0, error.ExitCode);
    }

    [SkippableFact]
    public async Task An_unknown_item_is_reported_as_a_failure()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        await Assert.ThrowsAsync<BwCliException>(
            () => Cli().Run("get", "item", "no such entry exists"));
    }

    [SkippableFact]
    public async Task Locking_the_vault_invalidates_the_session_key()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        BwCli cli = Cli();

        try
        {
            await cli.Run("lock");

            await Assert.ThrowsAsync<BwCliException>(
                () => cli.Run("get", "item", VaultwardenFixture.ItemName));
        }
        finally
        {
            // Leave the shared vault unlocked for whichever test runs next.
            await fixture.Unlock();
        }
    }
}
