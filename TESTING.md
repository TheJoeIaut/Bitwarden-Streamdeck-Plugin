# Tests

Unit tests need nothing beyond the SDK and run everywhere:

```bash
dotnet test BitwardenCLI.Tests/BitwardenCLI.Tests.csproj --filter "Category!=Integration"
```

They cover the parsing of Bitwarden CLI output, the unlock argument selection, the
keyboard layer's platform choices, and each action's success and failure paths against a
stubbed CLI and keyboard.

## Integration tests

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

## Dev container

`.devcontainer/` builds an image with the .NET 10 SDK, the Bitwarden CLI, `xdotool`,
`xterm` and Xvfb, and starts a headless display on `:99`. Opening the repository in it
lets the full suite - including both integration suites - run on Linux:

```bash
dotnet test BitwardenCLI.Tests/BitwardenCLI.Tests.csproj
```

## See also

- [BUILDING.md](BUILDING.md) - building and publishing the plugin
- [README.md](README.md) - installation and usage
