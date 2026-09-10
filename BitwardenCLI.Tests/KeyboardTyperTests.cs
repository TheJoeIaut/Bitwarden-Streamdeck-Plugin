namespace BitwardenStreamdeckPlugin.Tests;

/// <summary>
/// The parts of the keyboard layer that can be checked without actually driving a
/// keyboard. The real typing is covered by the Linux integration test.
/// </summary>
public class KeyboardTyperTests
{
    [Fact]
    public void An_x11_session_uses_xdotool()
    {
        using var _ = new EnvironmentVariable("WAYLAND_DISPLAY", null);

        Assert.Equal("xdotool", KeyboardTyper.LinuxTypingTool());
    }

    [Fact]
    public void A_wayland_session_uses_ydotool()
    {
        using var _ = new EnvironmentVariable("WAYLAND_DISPLAY", "wayland-0");

        Assert.Equal("ydotool", KeyboardTyper.LinuxTypingTool());
    }

    [Fact]
    public void An_empty_wayland_display_is_treated_as_x11()
    {
        using var _ = new EnvironmentVariable("WAYLAND_DISPLAY", "");

        Assert.Equal("xdotool", KeyboardTyper.LinuxTypingTool());
    }

    [Theory]
    [InlineData("hunter2", "\"hunter2\"")]
    [InlineData("with \"quotes\"", "\"with \\\"quotes\\\"\"")]
    [InlineData("back\\slash", "\"back\\\\slash\"")]
    [InlineData("", "\"\"")]
    public void AppleScript_string_literals_are_escaped(string input, string expected)
    {
        Assert.Equal(expected, KeyboardTyper.EscapeAppleScript(input));
    }

    [Fact]
    public void A_backslash_before_a_quote_survives_escaping_in_the_right_order()
    {
        // Escaping quotes first would double-escape the backslash and corrupt the password.
        Assert.Equal("\"a\\\\\\\"b\"", KeyboardTyper.EscapeAppleScript("a\\\"b"));
    }

    /// <summary>
    /// Sets an environment variable for the duration of a test and restores it afterwards.
    /// </summary>
    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string name;
        private readonly string? original;

        internal EnvironmentVariable(string name, string? value)
        {
            this.name = name;
            original = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(name, original);
    }
}
