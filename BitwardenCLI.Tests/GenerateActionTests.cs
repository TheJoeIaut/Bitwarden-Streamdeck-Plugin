using BarRaider.SdTools;
using NSubstitute;

namespace BitwardenStreamdeckPlugin.Tests;

/// <summary>
/// The generator's whole job is turning settings into 'bw generate' arguments, so that
/// translation carries most of the risk: a dropped flag silently produces a weaker
/// password than the one the user configured.
/// </summary>
public class GenerateActionTests
{
    private static Generate.PluginSettings Defaults() => Generate.PluginSettings.CreateDefaultSettings();

    private static string Args(Generate.PluginSettings settings) =>
        string.Join(" ", Generate.BuildGenerateArguments(settings));

    [Fact]
    public void The_defaults_match_the_bitwarden_generator()
    {
        Assert.Equal("generate --uppercase --lowercase --number --length 14 --minNumber 1 --minSpecial 0",
            Args(Defaults()));
    }

    [Fact]
    public void Special_characters_are_requested_when_enabled()
    {
        Generate.PluginSettings settings = Defaults();
        settings.Special = true;

        Assert.Contains("--special", Args(settings));
        Assert.Contains("--minSpecial 1", Args(settings));
    }

    [Fact]
    public void A_disabled_character_class_gets_an_explicit_zero_minimum()
    {
        // Omitting the minimum lets the CLI inherit the logged in account's saved
        // generator options, whose minimum of one forces the class in anyway.
        Generate.PluginSettings settings = Defaults();
        settings.Special = false;
        settings.MinSpecial = 3;

        Assert.Contains("--minSpecial 0", Args(settings));
    }

    [Fact]
    public void A_configured_minimum_of_zero_is_still_stated()
    {
        Generate.PluginSettings settings = Defaults();
        settings.MinNumber = 0;

        Assert.Contains("--minNumber 0", Args(settings));
    }

    [Fact]
    public void The_configured_length_is_passed_through()
    {
        Generate.PluginSettings settings = Defaults();
        settings.Length = 64;

        Assert.Contains("--length 64", Args(settings));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void A_length_below_the_cli_minimum_is_raised_to_it(int configured)
    {
        // 'bw generate' rejects anything under 5 outright, so an unset or too small
        // value would otherwise fail at runtime instead of producing a password.
        Generate.PluginSettings settings = Defaults();
        settings.Length = configured;

        Assert.Contains($"--length {Generate.MinimumLength}", Args(settings));
    }

    [Fact]
    public void Avoiding_ambiguous_characters_is_requested_when_enabled()
    {
        Generate.PluginSettings settings = Defaults();
        settings.AvoidAmbiguous = true;

        Assert.Contains("--ambiguous", Args(settings));
    }

    [Fact]
    public void Excluding_every_character_type_is_refused_rather_than_silently_weakened()
    {
        Generate.PluginSettings settings = Defaults();
        settings.Uppercase = settings.Lowercase = settings.Number = settings.Special = false;

        InvalidOperationException error =
            Assert.Throws<InvalidOperationException>(() => Generate.BuildGenerateArguments(settings));

        Assert.Contains("at least one character type", error.Message);
    }

    [Fact]
    public void A_single_character_type_is_enough()
    {
        Generate.PluginSettings settings = Defaults();
        settings.Uppercase = settings.Number = false;

        Assert.Equal("generate --lowercase --length 14 --minNumber 0 --minSpecial 0", Args(settings));
    }

    [Fact]
    public void Passphrase_mode_uses_the_passphrase_flags()
    {
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "passphrase";
        settings.Words = 5;
        settings.Separator = "_";

        Assert.Equal("generate --passphrase --words 5 --separator _", Args(settings));
    }

    [Fact]
    public void Passphrase_mode_ignores_the_password_options()
    {
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "passphrase";
        settings.Special = true;
        settings.Length = 40;

        string arguments = Args(settings);

        Assert.DoesNotContain("--length", arguments);
        Assert.DoesNotContain("--special", arguments);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void A_word_count_below_the_cli_minimum_is_raised_to_it(int configured)
    {
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "passphrase";
        settings.Words = configured;

        Assert.Contains($"--words {Generate.MinimumWords}", Args(settings));
    }

    [Fact]
    public void Passphrase_extras_are_requested_when_enabled()
    {
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "passphrase";
        settings.Capitalize = true;
        settings.IncludeNumber = true;

        Assert.Contains("--capitalize", Args(settings));
        Assert.Contains("--includeNumber", Args(settings));
    }

    [Theory]
    [InlineData("space")]
    [InlineData("empty")]
    [InlineData("-")]
    public void The_separator_is_passed_through_verbatim(string separator)
    {
        // 'space' and 'empty' are CLI keywords; everything else is literal.
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "passphrase";
        settings.Separator = separator;

        Assert.Contains($"--separator {separator}", Args(settings));
    }

    [Fact]
    public void An_empty_separator_falls_back_to_the_cli_default()
    {
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "passphrase";
        settings.Separator = "";

        Assert.DoesNotContain("--separator", Args(settings));
    }

    [Fact]
    public void The_generator_type_is_matched_case_insensitively()
    {
        Generate.PluginSettings settings = Defaults();
        settings.GeneratorType = "PassPhrase";

        Assert.Contains("--passphrase", Args(settings));
    }

    // --- action behaviour ---

    /// <summary>
    /// Builds an action from a complete settings object. Passing a partial one would
    /// deserialize every unmentioned bool as false, which is a different scenario.
    /// </summary>
    private static (Generate action, IBwCli cli, IKeyboardTyper typer, ISDConnection connection) Build(
        Action<Generate.PluginSettings>? configure = null, string generated = "gener4ted-p4ssword")
    {
        var built = BuildWithClipboard(configure, generated);
        return (built.action, built.cli, built.typer, built.connection);
    }

    private static (Generate action, IBwCli cli, IKeyboardTyper typer, IClipboardWriter clipboard,
        ISDConnection connection) BuildWithClipboard(
        Action<Generate.PluginSettings>? configure = null, string generated = "gener4ted-p4ssword")
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        IKeyboardTyper typer = Substitute.For<IKeyboardTyper>();
        IClipboardWriter clipboard = Substitute.For<IClipboardWriter>();

        cli.Run(Arg.Any<string[]>()).Returns(generated);

        Generate.PluginSettings settings = Defaults();
        configure?.Invoke(settings);

        var action = new Generate(connection, TestPayloads.Initial(settings), cli, typer, clipboard);
        return (action, cli, typer, clipboard, connection);
    }

    [Fact]
    public async Task The_generated_value_is_typed_at_the_cursor()
    {
        var (action, _, typer, connection) = Build(s => s.Length = 20);

        await action.TypeGeneratedSecret();

        await typer.Received(1).TypeText("gener4ted-p4ssword");
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task Trailing_newline_from_the_cli_is_not_typed()
    {
        var (action, _, typer, _) = Build(generated: "abc123\n");

        await action.TypeGeneratedSecret();

        await typer.Received(1).TypeText("abc123");
    }

    [Fact]
    public async Task A_failing_cli_alerts_and_types_nothing()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();
        IBwCli cli = Substitute.For<IBwCli>();
        IKeyboardTyper typer = Substitute.For<IKeyboardTyper>();
        cli.Run(Arg.Any<string[]>()).Returns<string>(_ => throw new BwCliException("boom", 1));

        var action = new Generate(connection, TestPayloads.Initial(Defaults()), cli, typer,
            Substitute.For<IClipboardWriter>());

        await action.TypeGeneratedSecret();

        await connection.Received(1).ShowAlert();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
    }

    [Fact]
    public async Task An_empty_result_is_treated_as_a_failure()
    {
        var (action, _, typer, connection) = Build(generated: "   \n");

        await action.TypeGeneratedSecret();

        await connection.Received(1).ShowAlert();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
    }

    [Fact]
    public async Task An_impossible_configuration_alerts_without_calling_the_cli()
    {
        var (action, cli, typer, connection) = Build(s =>
        {
            s.Uppercase = false;
            s.Lowercase = false;
            s.Number = false;
            s.Special = false;
        });

        await action.TypeGeneratedSecret();

        await cli.DidNotReceive().Run(Arg.Any<string[]>());
        await connection.Received(1).ShowAlert();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
    }

    [Fact]
    public void An_action_dropped_with_no_settings_persists_its_defaults()
    {
        ISDConnection connection = Substitute.For<ISDConnection>();

        _ = new Generate(connection, TestPayloads.Empty(), Substitute.For<IBwCli>(),
            Substitute.For<IKeyboardTyper>(), Substitute.For<IClipboardWriter>());

        connection.Received(1).SetSettingsAsync(Arg.Any<Newtonsoft.Json.Linq.JObject>());
    }

    // --- where the generated value goes ---

    [Fact]
    public async Task By_default_the_value_is_typed_and_not_copied()
    {
        var (action, _, typer, clipboard, _) = BuildWithClipboard();

        await action.TypeGeneratedSecret();

        await typer.Received(1).TypeText("gener4ted-p4ssword");
        await clipboard.DidNotReceive().SetText(Arg.Any<string>());
    }

    [Fact]
    public async Task Copy_to_clipboard_copies_without_typing()
    {
        // Typing a password into whichever window happens to be focused is exactly what
        // this mode exists to avoid.
        var (action, _, typer, clipboard, connection) = BuildWithClipboard(s => s.Output = "clipboard");

        await action.TypeGeneratedSecret();

        await clipboard.Received(1).SetText("gener4ted-p4ssword");
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
        await connection.Received(1).ShowOk();
    }

    [Fact]
    public async Task Type_and_copy_does_both()
    {
        var (action, _, typer, clipboard, _) = BuildWithClipboard(s => s.Output = "both");

        await action.TypeGeneratedSecret();

        await clipboard.Received(1).SetText("gener4ted-p4ssword");
        await typer.Received(1).TypeText("gener4ted-p4ssword");
    }

    [Fact]
    public async Task The_value_reaches_the_clipboard_before_it_is_typed()
    {
        // Typing is the half that can fail on a missing helper; having the value on the
        // clipboard first is what makes that recoverable.
        var (action, _, typer, clipboard, _) = BuildWithClipboard(s => s.Output = "both");

        await action.TypeGeneratedSecret();

        Received.InOrder(() =>
        {
            clipboard.SetText("gener4ted-p4ssword");
            typer.TypeText("gener4ted-p4ssword");
        });
    }

    [Fact]
    public async Task A_key_configured_before_this_option_existed_still_types()
    {
        // Those settings carry no output field at all.
        var (action, _, typer, clipboard, _) = BuildWithClipboard(s => s.Output = null!);

        await action.TypeGeneratedSecret();

        await typer.Received(1).TypeText("gener4ted-p4ssword");
        await clipboard.DidNotReceive().SetText(Arg.Any<string>());
    }

    [Fact]
    public async Task A_failing_clipboard_alerts_and_does_not_type_the_value_anyway()
    {
        var (action, _, typer, clipboard, connection) = BuildWithClipboard(s => s.Output = "both");
        clipboard.SetText(Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("'clip.exe' failed."));

        await action.TypeGeneratedSecret();

        await connection.Received(1).ShowAlert();
        await connection.DidNotReceive().ShowOk();
        await typer.DidNotReceive().TypeText(Arg.Any<string>());
    }

    [Theory]
    [InlineData("ClipBoard", false, true)]
    [InlineData("BOTH", true, true)]
    [InlineData("Type", true, false)]
    public void The_output_mode_is_matched_case_insensitively(string output, bool types, bool copies)
    {
        Generate.PluginSettings settings = Defaults();
        settings.Output = output;

        Assert.Equal(types, settings.TypesResult);
        Assert.Equal(copies, settings.CopiesResult);
    }
}
