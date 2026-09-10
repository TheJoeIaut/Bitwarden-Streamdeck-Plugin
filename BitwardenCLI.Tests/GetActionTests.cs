using BarRaider.SdTools;
using NSubstitute;

namespace BitwardenStreamdeckPlugin.Tests;

/// <summary>
/// Exercises the Get action against a stub CLI and a stub keyboard, so nothing is typed
/// and no real vault is touched.
/// </summary>
public class GetActionTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine("Fixtures", name));

    private static (Get action, IBwCli cli, IKeyboardTyper typer, ISDConnection connection) Build(
        string selectedInformation, string itemName = "GitHub", string? cliOutput = null)
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        IKeyboardTyper typer = Substitute.For<IKeyboardTyper>();

        cli.Run(Arg.Any<string[]>()).Returns(cliOutput ?? Fixture("get-item.json"));

        var payload = TestPayloads.Initial(new
        {
            iteminformation = selectedInformation,
            itemname = itemName
        });

        return (new Get(connection, payload, cli, typer), cli, typer, connection);
    }

    [Fact]
    public async Task Typing_a_password_sends_only_the_password()
    {
        var (action, _, typer, connection) = Build("password");

        await action.TypeSelectedInformation();

        await typer.Received(1).TypeText("correct horse battery staple");
        await typer.DidNotReceive().PressTab();
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task Typing_a_username_sends_only_the_username()
    {
        var (action, _, typer, _) = Build("username");

        await action.TypeSelectedInformation();

        await typer.Received(1).TypeText("octocat");
    }

    [Fact]
    public async Task Typing_a_totp_sends_the_totp_seed()
    {
        var (action, _, typer, _) = Build("totp");

        await action.TypeSelectedInformation();

        await typer.Received(1).TypeText("JBSWY3DPEHPK3PXP");
    }

    [Fact]
    public async Task Username_and_password_are_separated_by_a_tab_in_that_order()
    {
        var (action, _, typer, _) = Build("usernamepassword");

        await action.TypeSelectedInformation();

        Received.InOrder(() =>
        {
            typer.TypeText("octocat");
            typer.PressTab();
            typer.TypeText("correct horse battery staple");
        });
    }

    [Fact]
    public async Task The_requested_item_name_is_passed_through_to_the_cli()
    {
        var (action, cli, _, _) = Build("password", itemName: "Example Mail");

        await action.TypeSelectedInformation();

        await cli.Received(1).Run("get", "item", "Example Mail");
    }

    [Fact]
    public async Task A_failing_cli_shows_an_alert_and_types_nothing()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        IKeyboardTyper typer = Substitute.For<IKeyboardTyper>();

        cli.Run(Arg.Any<string[]>())
            .Returns<string>(_ => throw new BwCliException("vault is locked", 1));

        var action = new Get(connection, TestPayloads.Initial(new { iteminformation = "password" }), cli, typer);

        await action.TypeSelectedInformation();

        await connection.Received(1).ShowAlert();
        await connection.DidNotReceive().ShowOk();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
    }

    [Fact]
    public async Task An_unconfigured_action_shows_an_alert_rather_than_typing_nothing_silently()
    {
        var (action, _, typer, connection) = Build(selectedInformation: "");

        await action.TypeSelectedInformation();

        await connection.Received(1).ShowAlert();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
    }

    [Fact]
    public async Task A_locked_vault_while_loading_items_does_not_throw()
    {
        // ReceivedSettings runs this on the Stream Deck event loop; an escaping exception
        // would take the plugin down.
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns<string>(_ => throw new BwCliException("locked", 1));

        var action = new Get(connection, TestPayloads.Initial(new { iteminformation = "password" }),
            cli, Substitute.For<IKeyboardTyper>());

        await action.LoadItems();
    }

    [Fact]
    public void An_action_dropped_with_no_settings_persists_its_defaults()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();

        _ = new Get(connection, TestPayloads.Empty(), Substitute.For<IBwCli>(), Substitute.For<IKeyboardTyper>());

        connection.Received(1).SetSettingsAsync(Arg.Any<Newtonsoft.Json.Linq.JObject>());
    }
}
