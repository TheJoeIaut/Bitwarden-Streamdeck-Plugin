# Bitwarden-Streamdeck-Plugin
This unofficial Plugin allows interaction with the Bitwarden CLI. Actions allow the Lock/Unlock of the vault and the extraction of Username, Password or TOTP. The data is pasted at the current cursor position. This plugin requires the Bitwarden CLI to be already installed on your machine. All products that are compatible with the CLI (Bitwarden, Vaultwarden,...) are supported. Find the complete Bitwarden CLI documentation here: https://bitwarden.com/help/cli/

## Instructions
1. Download the Bitwarden CLI (https://bitwarden.com/help/cli/#download-and-install)
2. (optional) Configure your CLI (https://bitwarden.com/help/cli/#config)
3. Login (https://bitwarden.com/help/cli/#log-in)
4. Unlock the vault via CLI or configure Unlock Action in plugin
5. Configure Get Information Task
6. (optional) Lock Vault via CLI or configure Lock Action in plugin

## Requirements

The plugin runs on **.NET 10** and requires the
[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) to be
installed. If you would rather ship a build that does not need the runtime, see the
self-contained publish commands below.

Typing a credential into the focused window needs a platform helper:

| Platform | Mechanism | You need |
| --- | --- | --- |
| Windows | Win32 `SendInput` | nothing extra |
| Linux (X11) | `xdotool` | `xdotool` on `PATH` |
| Linux (Wayland) | `ydotool` | `ydotool` on `PATH`, its daemon running, and access to `/dev/uinput` |
| macOS | `osascript` | Accessibility permission granted to the Stream Deck host app |

On Linux the helper is picked at runtime: `ydotool` when `WAYLAND_DISPLAY` is set,
otherwise `xdotool`. Credentials are handed to these tools over stdin, never as command
line arguments, so they cannot be read out of the process list.

## Platform support

Elgato's own Stream Deck software runs on **Windows and macOS only**, and the plugin
manifest has no Linux platform value. To use this plugin on Linux you need a third party
host such as [OpenDeck](https://github.com/nekename/OpenDeck), which runs plugins built
for the original Stream Deck SDK. OpenDeck can also run the Windows build under Wine, so
a native Linux build is not strictly required.

macOS and Linux are built and published successfully but have not been tested on real
hardware.

## Building

The project targets `net10.0` and uses BarRaider's
[StreamDeck-Tools](https://github.com/BarRaider/streamdeck-tools) v7.

```bash
dotnet build BitwardenCLI/BitwardenStreamdeckPlugin.csproj -c Release
```

The build drops a ready-to-use plugin folder at
`BitwardenCLI/bin/Release/com.thejoeiaut.bitwarden.sdPlugin/`. Copy it into your host's
plugin directory (on Windows, `%APPDATA%\Elgato\StreamDeck\Plugins\`, with Stream Deck
closed) to test it locally.

To produce a build that carries its own runtime, publish self-contained into a
`.sdPlugin` folder and zip that folder:

```bash
dotnet publish BitwardenCLI/BitwardenStreamdeckPlugin.csproj -c Release -r win-x64 --self-contained true -o out/com.thejoeiaut.bitwarden.sdPlugin
```

Swap `-r win-x64` for `linux-x64` or `osx-arm64` to build for the other platforms.
Always pass `-o` when publishing so the RID-specific output does not overwrite the
plain `dotnet build` output folder.
