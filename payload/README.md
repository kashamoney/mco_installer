# Payload

Drop the redistributable files for your custom server here before building the installer.

Expected names:

- `update.zip` or an `update/` folder containing the files to copy into the game install root
- `server.crt`
- `pub.key` or `pubori.key`
- `radmin-vpn.exe` if you want the installer to install Radmin VPN silently

Edit `server.json` if the server IP, account creation URL, shard list URLs, ticker URL, install path, or VPN details change.

If `update.zip` has a top-level `update/` folder, that folder is unwrapped so its contents land directly in the Motor City Online install folder.
