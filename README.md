# Motor City Online Custom Server Installer

This repo builds a Windows `.exe` that helps users set up a retail Motor City Online install for a custom server.

The packaged installer can:

- apply a bundled community update archive or folder over the game install
- copy a bundled `pub.key` into the game folder
- install a bundled server certificate into the Local Machine trusted root store
- patch the 32-bit Motor City registry entries for the custom server
- silently install bundled Radmin VPN if it is not already installed
- launch `MCity.exe` when setup is complete

Users must install the original game from their ISO before running this tool. Do not package or redistribute copyrighted game files unless you have permission to do so.

## Payload files

Put redistributable server assets in `payload/` before publishing:

| File | Required | Purpose |
| --- | --- | --- |
| `server.json` | Yes | Server IP, URLs, install defaults, and VPN details. |
| `update.zip` or `update/` | Recommended | Community patch/update copied into the game install root. |
| `server.crt` | Recommended | Certificate installed into `LocalMachine\Root`. |
| `pub.key` | Recommended | Copied to the game folder as `pub.key`. |
| `radmin-vpn.exe` | Optional | Radmin VPN installer, installed silently if selected. |

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
```

## User flow

1. Run `McoInstaller.exe`.
2. Keep the auto-detected install folder, or choose the folder that contains `MCity.exe`.
3. Click **Install**.
4. Join the Radmin VPN network in the Radmin app if it is not already joined:
   - Network: `Motor City Online`
   - Password: `123456`

## Registry changes

The installer writes to the 32-bit registry view under:

```text
HKLM\SOFTWARE\Electronic Arts\Motor City
```

It sets the server IP, patch server port, account creation URL, auth login server, shard URLs, and ticker URL. It also clears the `Path` value in the `GamePatch`, `UpdateInfoPatch`, and `NPSPatch` keys so the original patcher path is skipped.
