# Bitwarden-Streamdeck-Plugin
This unofficial Plugin allows interaction with the Bitwarden CLI. Actions allow the Lock/Unlock of the vault and the extraction of Username, Password or TOTP. The data is pasted at the current cursor position. This plugin requires the Bitwarden CLI to be already installed on your machine. All products that are compatible with the CLI (Bitwarden, Vaultwarden,...) are supported. Find the complete Bitwarden CLI documentation here: https://bitwarden.com/help/cli/

## Actions

| Action | What it does | Needs an unlocked vault |
| --- | --- | --- |
| Unlock | Unlocks the vault using a master password, environment variable or password file | - |
| Lock | Locks the vault | - |
| Get Item Information | Types a stored username, password or TOTP at the cursor | yes |
| Generate Password | Generates a password or passphrase and types it at the cursor | no |

## Requirements

| You need | Why |
| --- | --- |
| [Bitwarden CLI](https://bitwarden.com/help/cli/#download-and-install) (`bw` on `PATH`) | Every action shells out to it; the plugin never talks to Bitwarden directly |
| [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | The plugin runs on **.NET 10** |
| Stream Deck 4.9 or newer | Minimum host version in the plugin manifest |

A self-contained build carries its own runtime and needs no .NET install - see
[BUILDING.md](BUILDING.md).

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

## Installation

### 1. Set up the Bitwarden CLI

The plugin drives an already-configured `bw`, so do this first and confirm it works from
a terminal before touching Stream Deck.

1. [Download and install the CLI](https://bitwarden.com/help/cli/#download-and-install)
   and make sure `bw` is on your `PATH`.
2. (optional) [Point it at your server](https://bitwarden.com/help/cli/#config) if you
   use Vaultwarden or a self-hosted Bitwarden: `bw config server https://vault.example.com`
3. [Log in](https://bitwarden.com/help/cli/#log-in): `bw login`

Logging in is a one-time step and survives reboots. Unlocking is separate, and is what
the Unlock action does for you.

### 2. Install the plugin

**From a release.** Download `com.thejoeiaut.bitwarden.zip` from the
[releases page](https://github.com/TheJoeIaut/Bitwarden-Streamdeck-Plugin/releases),
unzip it, and double-click the `com.thejoeiaut.bitwarden.streamDeckPlugin` file inside.
Stream Deck installs it and the actions appear under a **Bitwarden** category in the
action list.

> The published 1.0.0 release dates from 2023 and does not include the newer work -
> the Generate Password action, the searchable item picker, .NET 10 or the Linux and
> macOS support. For those, build from source.

**From source.** See [BUILDING.md](BUILDING.md). On Windows,
`tools/install-local.ps1` publishes and installs in one step.

### 3. Add the actions

Drag **Unlock** and **Get Item Information** onto keys and configure them as described
below. Both are under the Bitwarden category.

## Usage

### Unlock

Configure one credential source in the action's settings. If more than one is filled in,
the first of these wins:

1. **Master Password** - typed straight into the settings.
2. **Environment Variable** - the *name* of a variable holding the password
   (`bw unlock --passwordenv`). The variable has to be visible to the Stream Deck
   process, so set it system-wide and restart Stream Deck.
3. **Password File** - a file whose contents are the password
   (`bw unlock --passwordfile`).

> Prefer the environment variable or password file. The master password option is passed
> to `bw` as a command line argument, which is visible in the process list while the
> command runs, and Stream Deck stores action settings in plain text on disk either way.

Pressing the key unlocks the vault and keeps the session key in memory for the other
actions. The key shows a checkmark on success and a warning triangle if the CLI refused
- a wrong password, or not being logged in.

The session lives in the plugin process, so it is gone when Stream Deck restarts. Press
Unlock again after a restart; it does not need to be re-configured.

### Get Item Information

1. Unlock the vault first - the item list cannot be read from a locked vault.
2. Press **Load** in the action's settings. That fetches your vault entries once. Each
   entry is labelled with its username in parentheses so several logins for the same site
   can be told apart.
3. Type in **Selected Item** to search the loaded entries, and pick one. The X button
   clears the selection.
4. Choose what the key types under **Selected Info**:

| Selected Info | What gets typed |
| --- | --- |
| Username | The entry's username |
| Password | The entry's password |
| TOTP | The current six digit code, computed fresh on every press |
| Username+Password | Username, then Tab, then password - fills a whole login form |

The list is loaded only when you press Load, not while you type, so pressing Load again
is how you pick up entries added to your vault since.

TOTP asks the CLI for the current code rather than reading the stored seed, so what
arrives at the cursor is a code a login form accepts.

### Generate Password

Needs no unlocked vault - generation happens entirely in the CLI. Exposes the same
options as the Bitwarden generator, backed by `bw generate`:

- **Password**: length, which character types to include (`A-Z`, `a-z`, `0-9`, symbols),
  minimum counts for numbers and symbols, and avoiding ambiguous characters.
- **Passphrase**: word count, separator (a single character, or the words `space` or
  `empty`), title casing and including a number.

**Output** decides where the result goes: typed at the cursor, copied to the clipboard,
or both. Anything put on the clipboard stays there until something else replaces it.

Unchecking every character type is refused rather than quietly falling back to a default,
so you can never end up with a weaker password than the one you configured.

### Lock

No settings. Pressing the key locks the vault; the next Get press will fail until you
unlock again.

## Platform support

Elgato's own Stream Deck software runs on **Windows and macOS only**, and the plugin
manifest has no Linux platform value. To use this plugin on Linux you need a third party
host such as [OpenDeck](https://github.com/nekename/OpenDeck), which runs plugins built
for the original Stream Deck SDK. OpenDeck can also run the Windows build under Wine, so
a native Linux build is not strictly required.

macOS and Linux are built and published successfully but have not been tested on real
hardware.

## Development

- [BUILDING.md](BUILDING.md) - building, self-contained publishing, installing a local build
- [TESTING.md](TESTING.md) - unit tests, the integration suites and the dev container
