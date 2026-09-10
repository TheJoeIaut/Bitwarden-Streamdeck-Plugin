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
    public async Task Typing_a_totp_sends_the_current_code_not_the_stored_seed()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        IKeyboardTyper typer = Substitute.For<IKeyboardTyper>();

        // 'bw get item' carries the seed; only 'bw get totp' computes a usable code.
        cli.Run("get", "item", "GitHub").Returns(Fixture("get-item.json"));
        cli.Run("get", "totp", "GitHub").Returns("123456\n");

        var action = new Get(connection,
            TestPayloads.Initial(new { iteminformation = "totp", itemname = "GitHub" }), cli, typer);

        await action.TypeSelectedInformation();

        await typer.Received(1).TypeText("123456");
        await typer.DidNotReceive().TypeText("JBSWY3DPEHPK3PXP");
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task Typing_a_totp_asks_the_cli_for_the_code_rather_than_reading_the_item()
    {
        var (action, cli, _, _) = Build("totp");

        await action.TypeSelectedInformation();

        await cli.Received(1).Run("get", "totp", "GitHub");
        await cli.DidNotReceive().Run("get", "item", "GitHub");
    }

    [Fact]
    public async Task An_item_without_a_totp_alerts_instead_of_typing_nothing()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        IKeyboardTyper typer = Substitute.For<IKeyboardTyper>();

        // The CLI prints nothing when the entry has no TOTP configured.
        cli.Run(Arg.Any<string[]>()).Returns("");

        var action = new Get(connection,
            TestPayloads.Initial(new { iteminformation = "totp", itemname = "GitHub" }), cli, typer);

        await action.TypeSelectedInformation();

        await connection.Received(1).ShowAlert();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
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

    // --- resolving the picker's search box to something the CLI understands ---

    private static Get WithSelection(string selection, IBwCli cli, params (string label, string id)[] loaded)
    {
        var payload = TestPayloads.Initial(new
        {
            iteminformation = "password",
            itemname = selection,
            items = loaded.Select(entry => new { name = entry.label, id = entry.id }).ToArray()
        });

        return new Get(Substitute.For<ISDConnection>(), payload, cli, Substitute.For<IKeyboardTyper>());
    }

    [Fact]
    public async Task A_label_from_the_picker_resolves_to_the_entry_id()
    {
        // The label carries the username in parentheses, which the CLI would never match.
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns(Fixture("get-item.json"));

        Get action = WithSelection("GitHub (octocat)", cli,
            ("GitHub (octocat)", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f"));

        await action.GetItem();

        await cli.Received(1).Run("get", "item", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f");
    }

    [Fact]
    public async Task Identical_names_stay_distinguishable_because_ids_are_used()
    {
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns(Fixture("get-item.json"));

        Get action = WithSelection("acme.com (bob)", cli,
            ("acme.com (alice)", "11111111-1111-4111-8111-111111111111"),
            ("acme.com (bob)", "22222222-2222-4222-8222-222222222222"));

        await action.GetItem();

        await cli.Received(1).Run("get", "item", "22222222-2222-4222-8222-222222222222");
    }

    [Fact]
    public async Task Free_text_that_matches_nothing_is_handed_to_the_cli_as_a_search_term()
    {
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns(Fixture("get-item.json"));

        Get action = WithSelection("github", cli, ("GitHub (octocat)", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f"));

        await action.GetItem();

        await cli.Received(1).Run("get", "item", "github");
    }

    [Fact]
    public async Task A_key_configured_before_the_picker_changed_still_works()
    {
        // Those settings hold a bare id, which matches no label and is passed straight
        // through - exactly what the CLI wants anyway.
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns(Fixture("get-item.json"));

        Get action = WithSelection("8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f", cli);

        await action.GetItem();

        await cli.Received(1).Run("get", "item", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f");
    }

    [Fact]
    public async Task Surrounding_whitespace_from_the_search_box_is_ignored()
    {
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns(Fixture("get-item.json"));

        Get action = WithSelection("  GitHub (octocat)  ", cli,
            ("GitHub (octocat)", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f"));

        await action.GetItem();

        await cli.Received(1).Run("get", "item", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f");
    }

    [Fact]
    public async Task An_empty_search_box_alerts_instead_of_querying_the_whole_vault()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();

        var action = new Get(connection,
            TestPayloads.Initial(new { iteminformation = "password", itemname = "   " }),
            cli, Substitute.For<IKeyboardTyper>());

        await action.TypeSelectedInformation();

        await cli.DidNotReceive().Run(Arg.Any<string[]>());
        await connection.Received(1).ShowAlert();
    }

    [Fact]
    public async Task A_totp_request_resolves_the_selection_the_same_way()
    {
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns("123456");

        Get action = WithSelection("GitHub (octocat)", cli,
            ("GitHub (octocat)", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f"));

        await action.GetTotpCode();

        await cli.Received(1).Run("get", "totp", "8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f");
    }

    // --- the vault is listed once, not once per keystroke ---

    private static (Get action, IBwCli cli, ISDConnection connection) ForSettings()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        cli.Run(Arg.Any<string[]>()).Returns(File.ReadAllText(Path.Combine("Fixtures", "list-items.json")));

        var action = new Get(connection, TestPayloads.Initial(new { iteminformation = "password" }),
            cli, Substitute.For<IKeyboardTyper>());

        connection.ClearReceivedCalls();
        return (action, cli, connection);
    }

    [Fact]
    public void Typing_in_the_search_box_does_not_list_the_vault()
    {
        // Every keystroke arrives as a settings change. Listing here meant a Bitwarden CLI
        // process per character.
        var (action, cli, _) = ForSettings();

        action.ReceivedSettings(TestPayloads.Received(new { itemname = "gi" }));
        action.ReceivedSettings(TestPayloads.Received(new { itemname = "git" }));
        action.ReceivedSettings(TestPayloads.Received(new { itemname = "gith" }));

        cli.DidNotReceive().Run(Arg.Any<string[]>());
    }

    [Fact]
    public void Typing_in_the_search_box_does_not_echo_settings_back()
    {
        // Saving here came back as didReceiveSettings and rewrote the box mid-typing,
        // which is what made characters jump and disappear.
        var (action, _, connection) = ForSettings();

        action.ReceivedSettings(TestPayloads.Received(new { itemname = "gith" }));

        connection.DidNotReceive().SetSettingsAsync(Arg.Any<Newtonsoft.Json.Linq.JObject>());
    }

    [Fact]
    public void Pressing_load_lists_the_vault_and_sends_the_result_back()
    {
        var (action, cli, connection) = ForSettings();

        action.ReceivedSettings(TestPayloads.Received(new { itemname = "", loadtoken = "1700000000-abc" }));

        cli.Received(1).Run("list", "items");
        connection.Received(1).SetSettingsAsync(Arg.Any<Newtonsoft.Json.Linq.JObject>());
    }

    [Fact]
    public void Typing_after_a_load_still_does_not_list_the_vault_again()
    {
        // The token stays in the settings once stamped, so it must only count as a request
        // the first time it is seen.
        var (action, cli, _) = ForSettings();

        action.ReceivedSettings(TestPayloads.Received(new { itemname = "", loadtoken = "1700000000-abc" }));
        action.ReceivedSettings(TestPayloads.Received(new { itemname = "g", loadtoken = "1700000000-abc" }));
        action.ReceivedSettings(TestPayloads.Received(new { itemname = "gi", loadtoken = "1700000000-abc" }));

        cli.Received(1).Run("list", "items");
    }

    [Fact]
    public void Pressing_load_again_lists_the_vault_again()
    {
        var (action, cli, _) = ForSettings();

        action.ReceivedSettings(TestPayloads.Received(new { loadtoken = "1700000000-abc" }));
        action.ReceivedSettings(TestPayloads.Received(new { loadtoken = "1700000009-xyz" }));

        cli.Received(2).Run("list", "items");
    }

    [Fact]
    public void The_list_loaded_once_still_resolves_a_selection_typed_later()
    {
        // Filtering moved into the property inspector, so the list fetched by Load has to
        // stay usable for every later keystroke without another CLI call.
        var (action, cli, _) = ForSettings();

        action.ReceivedSettings(TestPayloads.Received(new { loadtoken = "1700000000-abc" }));
        cli.ClearReceivedCalls();

        action.ReceivedSettings(TestPayloads.Received(new { itemname = "GitHub (octocat)" }));

        Assert.Equal("8f1b3c2e-4d5a-4b6c-9e7f-1a2b3c4d5e6f", action.ResolveItemQuery());
        cli.DidNotReceive().Run(Arg.Any<string[]>());
    }

    [Fact]
    public void An_action_dropped_with_no_settings_persists_its_defaults()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();

        _ = new Get(connection, TestPayloads.Empty(), Substitute.For<IBwCli>(), Substitute.For<IKeyboardTyper>());

        connection.Received(1).SetSettingsAsync(Arg.Any<Newtonsoft.Json.Linq.JObject>());
    }
}
