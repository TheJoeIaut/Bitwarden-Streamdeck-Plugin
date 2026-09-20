using System.Globalization;
using BarRaider.SdTools;
using NSubstitute;

namespace BitwardenStreamdeckPlugin.Tests;

/// <summary>
/// Exercises the Sync action against a stub CLI, so no real vault is contacted.
/// </summary>
public class SyncActionTests
{
    private static Sync Build(ISDConnection connection, IBwCli cli, object? settings = null)
    {
        return new Sync(connection, TestPayloads.Initial(settings), cli);
    }

    [Fact]
    public void An_ordinary_sync_passes_no_options()
    {
        Assert.Equal(new[] { "sync" }, Sync.BuildSyncArguments(Sync.PluginSettings.CreateDefaultSettings()));
    }

    [Fact]
    public void A_full_re_download_is_asked_for_with_force()
    {
        var settings = new Sync.PluginSettings { Force = true };

        Assert.Equal(new[] { "sync", "--force" }, Sync.BuildSyncArguments(settings));
    }

    [Fact]
    public async Task Syncing_invokes_bw_sync_and_confirms()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns("Syncing complete.");

        Sync action = Build(connection, cli);

        await action.SyncVault();

        await cli.Received(1).Run("sync");
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task A_locked_vault_alerts_instead_of_reporting_success()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>())
            .Returns<string>(_ => throw new BwCliException("Vault is locked.", 1));

        Sync action = Build(connection, cli);

        await action.SyncVault();

        await connection.Received(1).ShowAlert();
        await connection.DidNotReceive().ShowOk();
    }

    [Fact]
    public async Task The_last_sync_time_is_only_asked_for_when_it_is_shown()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns("Syncing complete.");

        Sync action = Build(connection, cli, new { showlastsync = false });

        await action.SyncVault();

        await cli.DidNotReceive().Run("sync", "--last");
        await connection.DidNotReceive().SetTitleAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Showing_the_last_sync_time_puts_it_on_the_key()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run("sync").Returns("Syncing complete.");
        cli.Run("sync", "--last").Returns("2024-03-05T14:32:07.000Z\n");

        Sync action = Build(connection, cli, new { showlastsync = true });

        await action.SyncVault();

        // The expectation is computed the same way the action converts, so the test does
        // not depend on the machine's time zone.
        string expected = DateTimeOffset.Parse("2024-03-05T14:32:07.000Z", CultureInfo.InvariantCulture)
            .ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        await connection.Received(1).SetTitleAsync(expected);
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task A_title_that_cannot_be_read_does_not_fail_a_finished_sync()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run("sync").Returns("Syncing complete.");
        cli.Run("sync", "--last")
            .Returns<string>(_ => throw new BwCliException("boom", 1));

        Sync action = Build(connection, cli, new { showlastsync = true });

        await action.SyncVault();

        await connection.DidNotReceive().SetTitleAsync(Arg.Any<string>());
        await connection.Received(1).ShowOk();
        await connection.DidNotReceive().ShowAlert();
    }

    [Fact]
    public void An_iso_timestamp_becomes_a_local_time_of_day()
    {
        string expected = DateTimeOffset.Parse("2024-03-05T14:32:07.000Z", CultureInfo.InvariantCulture)
            .ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        Assert.Equal(expected, Sync.FormatLastSync("2024-03-05T14:32:07.000Z\n"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n")]
    [InlineData("You are not logged in.")]
    public void Anything_but_a_timestamp_leaves_the_title_alone(string output)
    {
        // A vault that has never been synced prints nothing at all.
        Assert.Null(Sync.FormatLastSync(output));
    }

    [Fact]
    public void A_null_output_leaves_the_title_alone()
    {
        Assert.Null(Sync.FormatLastSync(null));
    }

    [Fact]
    public void Turning_the_last_sync_time_off_clears_the_key()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();

        Sync action = Build(connection, cli, new { showlastsync = true });

        action.ReceivedSettings(TestPayloads.Received(new { showlastsync = false }));

        connection.Received(1).SetTitleAsync(null);
    }
}
