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
    public async Task The_picker_labels_real_entries_with_their_usernames()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        string output = await Cli().Run("list", "items");
        List<ItemListDto> items = Get.ParseItemList(output);

        Assert.Contains(items,
            i => i.ItemName == $"{VaultwardenFixture.ItemName} ({VaultwardenFixture.ItemUsername})");
    }

    [SkippableFact]
    public async Task The_stored_picker_list_carries_no_credentials()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        // Real CLI output really does contain the passwords and seeds; this is the check
        // that they never reach the Stream Deck's saved settings.
        string output = await Cli().Run("list", "items");
        Assert.Contains(VaultwardenFixture.ItemPassword, output);

        string persisted = JsonConvert.SerializeObject(Get.ParseItemList(output));

        Assert.DoesNotContain(VaultwardenFixture.ItemPassword, persisted);
        Assert.DoesNotContain(VaultwardenFixture.TotpSecret, persisted);
    }

    [SkippableFact]
    public async Task A_totp_request_returns_a_six_digit_code_not_the_stored_secret()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        string code = (await Cli().Run("get", "totp", VaultwardenFixture.TotpItemName)).Trim();

        Assert.Matches(@"^\d{6}$", code);
        Assert.NotEqual(VaultwardenFixture.TotpSecret, code);
    }

    [SkippableFact]
    public async Task The_item_payload_carries_only_the_totp_seed()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        // This is the trap the Get action used to fall into: the seed is right there in the
        // item and looks enough like a password to be typed by mistake.
        string output = await Cli().Run("get", "item", VaultwardenFixture.TotpItemName);
        Item item = Get.ParseItem(output);

        Assert.Equal(VaultwardenFixture.TotpSecret, item.TotpSecret);
        Assert.DoesNotMatch(@"^\d{6}$", item.TotpSecret);
    }

    /// <summary>
    /// Generation needs no server and no login; this reuses the fixture only for its
    /// isolated CLI profile. It checks that the arguments the action builds actually
    /// produce what they claim - a dropped flag would silently weaken the password.
    /// </summary>
    [SkippableFact]
    public async Task A_generated_password_honours_the_configured_options()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        Generate.PluginSettings settings = Generate.PluginSettings.CreateDefaultSettings();
        settings.Length = 32;
        settings.Special = true;
        settings.MinSpecial = 2;

        var cli = new BwCli(environment: fixture.CliEnvironment);
        string password = (await cli.Run(Generate.BuildGenerateArguments(settings))).Trim();

        Assert.Equal(32, password.Length);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.True(password.Count(c => !char.IsLetterOrDigit(c)) >= 2, $"expected 2+ specials in '{password}'");
    }

    [SkippableFact]
    public async Task A_generated_password_excludes_the_character_types_that_are_off()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        Generate.PluginSettings settings = Generate.PluginSettings.CreateDefaultSettings();
        settings.Uppercase = false;
        settings.Number = false;
        settings.Special = false;
        settings.Length = 24;

        string[] arguments = Generate.BuildGenerateArguments(settings);
        var cli = new BwCli(environment: fixture.CliEnvironment);
        string password = (await cli.Run(arguments)).Trim();

        string context = $"args: bw {string.Join(" ", arguments)} -> '{password}'";
        Assert.Equal(24, password.Length);
        Assert.All(password, c => Assert.True(char.IsLower(c), $"unexpected '{c}'; {context}"));
    }

    [SkippableFact]
    public async Task A_generated_passphrase_has_the_requested_shape()
    {
        Skip.If(fixture.SkipReason != null, fixture.SkipReason ?? string.Empty);

        Generate.PluginSettings settings = Generate.PluginSettings.CreateDefaultSettings();
        settings.GeneratorType = "passphrase";
        settings.Words = 5;
        settings.Separator = "_";
        settings.Capitalize = true;

        var cli = new BwCli(environment: fixture.CliEnvironment);
        string passphrase = (await cli.Run(Generate.BuildGenerateArguments(settings))).Trim();

        string[] words = passphrase.Split('_');
        Assert.Equal(5, words.Length);
        Assert.All(words, w => Assert.True(char.IsUpper(w[0]), $"'{w}' is not capitalized"));
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
