# Building

The project targets `net10.0` and uses BarRaider's
[StreamDeck-Tools](https://github.com/BarRaider/streamdeck-tools) v7. You need the
[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) - the Desktop Runtime
that users install is not enough to compile.

```bash
dotnet build BitwardenCLI/BitwardenStreamdeckPlugin.csproj -c Release
```

The build drops a ready-to-use plugin folder at
`BitwardenCLI/bin/Release/com.thejoeiaut.bitwarden.sdPlugin/`. Copy it into your host's
plugin directory (on Windows, `%APPDATA%\Elgato\StreamDeck\Plugins\`, with Stream Deck
closed) to test it locally.

## Self-contained builds

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

## Installing your build locally (Windows)

```powershell
powershell -ExecutionPolicy Bypass -File tools/install-local.ps1
```

That publishes, stops Stream Deck, replaces
`%APPDATA%\Elgato\StreamDeck\Plugins\com.thejoeiaut.bitwarden.sdPlugin`, starts Stream Deck
again and prints the plugin's log so you can see it registered.

Stream Deck holds the plugin's executable open, so it has to be stopped for the files to be
replaced - there is no way around the restart. Stream Deck usually runs at a higher
integrity level than an ordinary shell, so stopping it outright needs an elevated prompt;
otherwise the script asks you to quit it from its tray icon and waits (`-WaitMinutes`,
10 by default). Pass `-SkipBuild` to install the last publish again, or `-NoRelaunch` to
leave Stream Deck closed.

## See also

- [TESTING.md](TESTING.md) - running the unit and integration suites
- [README.md](README.md) - installation and usage
