# Bitwarden-Streamdeck-Plugin
This unofficial Plugin allows interaction with the Bitwarden CLI. Actions allow the Lock/Unlock of the vault and the extraction of Username, Password or TOTP. The data is pasted at the current cursor position. This plugin requires the Bitwarden CLI to be already installed on your machine. All products that are compatible with the CLI (Bitwarden, Vaultwarden,...) are supported. Find the complete Bitwarden CLI documentation here: https://bitwarden.com/help/cli/

## Actions

| Action | What it does | Needs an unlocked vault |
| --- | --- | --- |
| Unlock | Unlocks the vault using a master password, environment variable or password file | - |
| Lock | Locks the vault | - |
| Get Item Information | Types a stored username, password or TOTP at the cursor | yes |
| Generate Password | Generates a password or passphrase and types it at the cursor | no |

## What has changed

### 2.0

The first release since 2023, and a large one. Everything below is new since 1.0.

**New**

- **Generate Password action.** Passwords or passphrases with the same options as the
  Bitwarden generator, typed at the cursor, copied to the clipboard, or both. Works on a
  locked vault.
- **Searchable item picker.** Load your vault once, then type to search it. Entries are
  labelled with their username so several logins for the same site can be told apart, and
  a clear button resets the selection.
- **Username+Password in one press.** Types the username, Tab, then the password.
- **macOS and Linux code paths.** Typing goes through `osascript` on macOS and
  `xdotool`/`ydotool` on Linux. The released package is Windows only - build from source
  for the others, and see [Platform support](#platform-support).
- **No .NET install needed.** The released build carries its own runtime.

**Fixed**

- **TOTP typed the stored seed, not a code.** The TOTP option typed the raw base32 secret
  from the vault entry, which no login form accepts. It now asks the CLI for the current
  six digit code on every press.
- **A Bitwarden CLI process per keystroke.** Typing in the item field reloaded the whole
  vault on every character, which also rewrote the field mid-typing and made characters
  jump and disappear. The list is fetched once, when you press Load.
- **Hangs on a locked vault.** A `bw` command that wanted to prompt - which is what a
  locked vault produces - waited forever on input that could never arrive. Prompts are now
  turned off, so it fails and reports instead.

**Under the hood**

- .NET 10 and BarRaider's StreamDeck-Tools 7.
- A unit test suite, integration suites covering real Linux typing and an end to end run
  against a throwaway Vaultwarden server, a dev container and CI.
- Credentials are handed to the typing helpers over stdin rather than as command line
  arguments, so they cannot be read out of the process list.

## Requirements

| You need | Why |
| --- | --- |
| [Bitwarden CLI](https://bitwarden.com/help/cli/#download-and-install) (`bw` on `PATH`) | Every action shells out to it; the plugin never talks to Bitwarden directly |
| Stream Deck 6.4 or newer | Minimum host version in the plugin manifest |
| [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | Only for framework-dependent builds from source. The released plugin is self-contained and needs no .NET install |

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

**From the Elgato Marketplace.** Search for **Bitwarden** in the Stream Deck store and
install it. Stream Deck handles updates from then on. This is the recommended route.

Other ways in, if you need them:

- **From a GitHub release.** Download the package from the
  [releases page](https://github.com/TheJoeIaut/Bitwarden-Streamdeck-Plugin/releases) and
  double-click the `com.thejoeiaut.bitwarden.streamDeckPlugin` file inside.
- **From source.** See [BUILDING.md](BUILDING.md). On Windows,
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

The released 2.0 package is **Windows only**, and its manifest declares nothing else.
That is a packaging decision, not a code one: the plugin builds and runs for macOS and
Linux too, and [BUILDING.md](BUILDING.md) covers publishing for them.

macOS is not shipped because a Mac build has to be signed and notarized before Gatekeeper
will run it, and it has never been tested on real hardware.

Linux is not a Stream Deck platform at all - Elgato's own software runs on Windows and
macOS only, and the manifest has no Linux platform value. To use this plugin on Linux you
need a third party host such as [OpenDeck](https://github.com/nekename/OpenDeck), which
runs plugins built for the original Stream Deck SDK. OpenDeck can also run the Windows
build under Wine, so a native Linux build is not strictly required.

## Development

- [BUILDING.md](BUILDING.md) - building, self-contained publishing, installing a local build
- [TESTING.md](TESTING.md) - unit tests, the integration suites and the dev container
