# Motor City Online Custom Server Installer

This repo builds a Windows `.exe` that helps users set up a retail Motor City Online install for a custom server.

The packaged installer can:

- apply a bundled community update archive or folder over the game install
- copy a bundled `pub.key` into the game folder
- install a bundled server certificate into the Local Machine trusted root store
- patch the 32-bit Motor City registry entries for the custom server
- launch `MCity.exe` when setup is complete

Users must install the original game from their ISO before running this tool. Do not package or redistribute copyrighted game files unless you have permission to do so.

## Payload files

Put redistributable server assets in `payload/` before publishing:

| File | Required | Purpose |
| --- | --- | --- |
| `server.json` | Yes | Server IP, URLs, and install defaults. |
| `update.zip` or `update/` | Recommended | Community patch/update copied into the game install root. |
| `server.crt` | Recommended | Certificate installed into `LocalMachine\Root`. |
| `pub.key` | Recommended | Copied to the game folder as `pub.key`. |

If `update.zip` contains a top-level `update/` folder, the installer unwraps that folder and copies its contents directly into the Motor City Online install folder. For example, `update.zip\update\MCity.exe` becomes `...\Motor City Online\MCity.exe`.

## Build

From PowerShell:

```powershell
.\scripts\build.ps1
```

For a release package that fails if the recommended payload files are missing:

```powershell
.\scripts\build.ps1 -StrictPayload
```

The single-file executable is published to:

```text
dist\win-x64\McoInstaller.exe
dist\win-x64\McoInstaller-vX.Y.Z.exe
```

## Versioning

The installer uses semantic versions. The current version is set with `<VersionPrefix>` in `src\McoInstaller\McoInstaller.csproj`.

- Increment the patch version for packaging fixes or small non-behavioral changes.
- Increment the minor version for user-facing installer, payload, or server config changes.
- Increment the major version for breaking changes to the install flow.

The version is shown in the installer window title, Windows file properties, and the versioned release filename.

## User flow

1. Run the latest `McoInstaller-vX.Y.Z.exe`.
2. Keep the auto-detected install folder, or choose the folder that contains `MCity.exe`.
3. Click **Install**.
4. Start `MCity.exe`, or select the launch option before installing.

## Registry changes

The installer writes to the 32-bit registry view under:

```text
HKLM\SOFTWARE\Electronic Arts\Motor City
```

It sets the server IP, patch server port, account creation URL, auth login server, shard URLs, and ticker URL. It also clears the `Path` value in the `GamePatch`, `UpdateInfoPatch`, and `NPSPatch` keys so the original patcher path is skipped.
