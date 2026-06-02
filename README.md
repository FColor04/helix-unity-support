# Helix Unity Support

Unity external editor support for [Helix](https://helix-editor.com/).

This package registers Helix as a Unity external script editor, opens files at the requested line and column, and regenerates Unity `.sln`/`.csproj` files so C# language servers can load the project correctly.

## Features

- Registers `Helix Code Editor` in Unity's external editor list.
- Opens Unity script requests as `path:line:column`.
- Launches Helix in an available terminal on Linux/macOS.
- Adds a Unity menu for setting Helix as the external editor.
- Regenerates Unity project files through Unity's Visual Studio project generator.
- Enables Unity project generation for embedded, local, git, unknown, and local tarball package sources.
- Writes an isolated `.helix/lsp-root/BallGame.sln` plus a `csharp-ls` wrapper so Helix does not load nested Unity package solutions.
- Refuses to generate project files while `.meta` files contain merge conflict markers.

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

Install `csharp-ls` or OmniSharp and configure Helix with a valid executable path.

Example `~/.config/helix/languages.toml`:

```toml
[language-server.csharp-ls-unity]
command = "/usr/bin/bash"
args = ["/path/to/project/.helix/csharp-ls-unity"]
timeout = 180

[[language]]
name = "c-sharp"
language-servers = [ "csharp-ls-unity" ]
roots = [ "Assets", "Packages", "ProjectSettings", ".helix/lsp-root/BallGame.sln" ]
```

Verify with:

```sh
helix --health c-sharp
```

If Helix reports that the language server exited, regenerate project files from Unity first and check Unity's console for `HelixUnity` errors. Invalid `.meta` files or stale generated project files can prevent C# language servers from loading the workspace.

## Configuration

Set `HELIX_PATH` if Helix is not installed as `hx` or `helix` in a common path.

On Linux/macOS, configure the preferred terminal in:

```text
Edit > Preferences > Helix Unity
```

## Development

This is a Unity package. The editor code lives under `Editor/` and is compiled only in the Unity editor.
