# Bitwarden-Streamdeck-Plugin
This unofficial Plugin allows interaction with the Bitwarden CLI. Actions allow the Lock/Unlock of the vault and the extraction of Username, Password or TOTP. The data is pasted at the current cursor position. This plugin requires the Bitwarden CLI to be already installed on your machine. All products that are compatible with the CLI (Bitwarden, Vaultwarden,...) are supported. Find the complete Bitwarden CLI documentation here: https://bitwarden.com/help/cli/

## Actions

| Action | What it does | Needs an unlocked vault |
| --- | --- | --- |
| Unlock | Unlocks the vault using a master password, environment variable or password file | - |
| Lock | Locks the vault | - |
| Get Item Information | Types a stored username, password or TOTP at the cursor | yes |
| Generate Password | Generates a password or passphrase and types it at the cursor | no |

### Generate Password

Exposes the same options as the Bitwarden generator, backed by `bw generate`:

- **Password**: length, which character types to include (`A-Z`, `a-z`, `0-9`, symbols),
  minimum counts for numbers and symbols, and avoiding ambiguous characters.
- **Passphrase**: word count, separator, title casing and including a number.

Because generation happens entirely in the CLI, this action works on a locked vault - no
Unlock needed first.

Unchecking every character type is refused rather than quietly falling back to a default,
so you can never end up with a weaker password than the one you configured.

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

> Prefer `dotnet publish -r <rid>` over a plain `dotnet build` when you want something to
> ship or install. Since the plugin gained Linux and macOS targets, a plain build carries
> every platform's SkiaSharp natives and comes to roughly 180 MB; publishing for a single
> runtime keeps it near 16 MB.

### Installing your build locally (Windows)

```powershell
pwsh -File tools/install-local.ps1
```

That publishes, stops Stream Deck, replaces
`%APPDATA%\Elgato\StreamDeck\Plugins\com.thejoeiaut.bitwarden.sdPlugin`, starts Stream Deck
again and prints the plugin's log so you can see it registered.

Stream Deck holds the plugin's executable open, so it has to be stopped for the files to be
replaced - there is no way around the restart. If it is running elevated the script cannot
stop it and will ask you to quit it from its tray icon first. Pass `-SkipBuild` to install
the last publish again, or `-NoRelaunch` to leave Stream Deck closed.

## Tests

Unit tests need nothing beyond the SDK and run everywhere:

```bash
dotnet test BitwardenCLI.Tests/BitwardenCLI.Tests.csproj --filter "Category!=Integration"
```

They cover the parsing of Bitwarden CLI output, the unlock argument selection, the
keyboard layer's platform choices, and each action's success and failure paths against a
stubbed CLI and keyboard.

### Integration tests

Two suites are marked `Category=Integration` and are excluded from the command above.

**Linux typing** (`LinuxKeyboardTests`) checks that `KeyboardTyper` really drives
`xdotool` and that the characters arrive in the focused window. It needs Linux, an X
display and `xdotool`, and skips anywhere else. The dev container and the CI Linux job
both provide those.

**Vaultwarden end to end** (`VaultwardenE2ETests`) starts a throwaway Vaultwarden server
with Testcontainers, registers an account, seeds a login entry and reads it back through
the real `bw` CLI. It needs a container runtime (Docker or Podman), `bw` on `PATH`, and:

```bash
BW_E2E=1 dotnet test BitwardenCLI.Tests/BitwardenCLI.Tests.csproj --filter "Category=Integration"
```

> The `BW_E2E` opt-in exists because these tests run the real `bw` binary, which on a
> developer machine is usually signed in to a real vault. Every invocation is given its
> own throwaway `BITWARDENCLI_APPDATA_DIR`, so your login, server configuration and
> session are never read or modified - but the tests stay opt-in regardless.

There is no `bw register` command, so the fixture derives the account keys itself the way
the official clients do (PBKDF2, HKDF, AES-CBC plus HMAC). If that were wrong the
subsequent `bw login` would simply fail, so the end to end run validates it.

### Dev container

`.devcontainer/` builds an image with the .NET 10 SDK, the Bitwarden CLI, `xdotool`,
`xterm` and Xvfb, and starts a headless display on `:99`. Opening the repository in it
lets the full suite - including both integration suites - run on Linux:

```bash
dotnet test BitwardenCLI.Tests/BitwardenCLI.Tests.csproj
```
