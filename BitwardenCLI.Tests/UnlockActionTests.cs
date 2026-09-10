using BarRaider.SdTools;
using NSubstitute;

namespace BitwardenStreamdeckPlugin.Tests;

public class UnlockActionTests
{
    private static Unlock Build(ISDConnection connection, IBwCli cli, object settings)
    {
        return new Unlock(connection, TestPayloads.Initial(settings), cli);
    }

    [Fact]
    public void A_master_password_is_passed_to_bw_unlock_with_raw_output()
    {
        var settings = new Unlock.PluginSettings { MasterPassword = "s3cret" };

        Assert.Equal(new[] { "unlock", "s3cret", "--raw" }, Unlock.BuildUnlockArguments(settings));
    }

    [Fact]
    public void An_environment_variable_is_preferred_over_a_password_file()
    {
        var settings = new Unlock.PluginSettings
        {
            PasswordEnvVariable = "BW_PASSWORD",
            PasswordFile = "/tmp/pw.txt"
        };

        Assert.Equal(new[] { "unlock", "--passwordenv", "BW_PASSWORD", "--raw" },
            Unlock.BuildUnlockArguments(settings));
    }

    [Fact]
    public void A_password_file_is_used_when_nothing_else_is_configured()
    {
        var settings = new Unlock.PluginSettings { PasswordFile = "/tmp/pw.txt" };

        Assert.Equal(new[] { "unlock", "--passwordfile", "/tmp/pw.txt", "--raw" },
            Unlock.BuildUnlockArguments(settings));
    }

    [Fact]
    public void A_master_password_wins_over_every_other_source()
    {
        var settings = new Unlock.PluginSettings
        {
            MasterPassword = "s3cret",
            PasswordEnvVariable = "BW_PASSWORD",
            PasswordFile = "/tmp/pw.txt"
        };

        Assert.Equal("s3cret", Unlock.BuildUnlockArguments(settings)![1]);
    }

    [Fact]
    public void Nothing_configured_yields_no_arguments()
    {
        Assert.Null(Unlock.BuildUnlockArguments(Unlock.PluginSettings.CreateDefaultSettings()));
    }

    [Fact]
    public async Task A_successful_unlock_stores_the_session_key()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns("session-key-value\n");

        Unlock action = Build(connection, cli, new { masterpassword = "s3cret" });

        await action.UnlockVault();

        // Trailing newline from --raw output must not end up in BW_SESSION.
        cli.Received(1).SetSessionKey("session-key-value");
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task An_unconfigured_unlock_alerts_and_never_calls_the_cli()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();

        Unlock action = Build(connection, cli, new { masterpassword = "" });

        await action.UnlockVault();

        await cli.DidNotReceive().Run(Arg.Any<string[]>());
        await connection.Received(1).ShowAlert();
        await connection.DidNotReceive().ShowOk();
    }

    [Fact]
    public async Task A_wrong_master_password_alerts_and_stores_no_session_key()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>())
            .Returns<string>(_ => throw new BwCliException("Invalid master password.", 1));

        Unlock action = Build(connection, cli, new { masterpassword = "wrong" });

        await action.UnlockVault();

        cli.DidNotReceive().SetSessionKey(Arg.Any<string>());
        await connection.Received(1).ShowAlert();
        await connection.DidNotReceive().ShowOk();
    }

    [Fact]
    public async Task An_empty_session_key_is_treated_as_a_failure()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns("   \n");

        Unlock action = Build(connection, cli, new { masterpassword = "s3cret" });

        await action.UnlockVault();

        cli.DidNotReceive().SetSessionKey(Arg.Any<string>());
        await connection.Received(1).ShowAlert();
    }
}
