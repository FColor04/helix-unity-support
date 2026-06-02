# Helix Unity Support

Unity external editor support for [Helix](https://helix-editor.com/).

This package registers Helix as a Unity external script editor, opens files at the requested line and column, and regenerates Unity `.sln`/`.csproj` files so C# language servers can load the project correctly.

## Features

- Registers `Helix Code Editor` in Unity's external editor list.
- Opens Unity script requests as `path:line:column`.
- Launches Helix in an available terminal on Linux/macOS.
- Adds a Unity menu for setting Helix as the external editor.
- Regenerates Unity project files through Unity's Visual Studio project generator.
- Ensures the solution includes all generated root `.csproj` files.

## Install In Unity

Use a local file dependency while developing:

```json
"com.fcolor04.helix-unity-support": "file:/home/fcolor04/Projects/helix-unity-support"
```

After Unity resolves the package, use:

```text
Tools > Helix Code Editor > Set as Unity External Editor
```

You can also select `Helix Code Editor` from Unity preferences if Unity lists it there.

## Helix C# LSP

Install OmniSharp and configure Helix with a valid executable path.

Example `~/.config/helix/languages.toml`:

```toml
[language-server.omnisharp]
command = "/usr/bin/omnisharp"
args = [ "-lsp" ]
timeout = 10000

[[language]]
name = "c-sharp"
language-servers = [ "omnisharp" ]
roots = [ "*.sln", "*.csproj" ]
```

Verify with:

```sh
helix --health c-sharp
```

## Configuration

Set `HELIX_PATH` if Helix is not installed as `hx` or `helix` in a common path.

On Linux/macOS, configure the preferred terminal in:

```text
Edit > Preferences > Helix Unity
```

## Development

This is a Unity package. The editor code lives under `Editor/` and is compiled only in the Unity editor.
