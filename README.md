# TYW Minecraft Launcher

A customized build of **Plain Craft Launcher 2 (PCL2)** for the TYW Minecraft community, with a self-hosted server-side management layer: per-channel announcements, in-app updates, a server-gated beta channel, and device banning.

> **This is NOT the original PCL2.** All original PCL2 code belongs to LTCat and the PCL community — see [Original Project](#original-project) below.

## Original Project

- **PCL2 official repo (issues & releases):** https://github.com/Hex-Dragon/PCL2
- **PCL2 open-source code (this repo's base):** https://github.com/Meloong-Git/PCL
- Author: **LTCat** (@Meloong-Git). Thanks for the amazing launcher!
- [Download the original PCL2](https://meloong.com/pcl)

This repository is a fork-style redistribution of the open-source PCL2 code with additional modifications. The server-side code is **not** included and is **not** open source.

## What's Different from Upstream PCL2

- Rebranded to **TYW Minecraft Launcher** (new icon, About/Credits pages, crash logs, etc.), with a "Based on PCL" attribution kept in the About page
- **Three release channels**: Release / Snapshot / Beta (the Beta channel is gated by server-issued beta codes bound to a device ID)
- **Device banning**: the launcher checks a server endpoint on startup; banned devices are locked out with the reason shown
- **Server announcements**: per-channel popup announcements pushed from the server (shown once per content, remembered in the registry)
- **In-app updates**: per-channel version codes on the server drive update prompts and one-click download-and-restart
- **Built-in changelog** for each version, independent of server announcements
- **Anonymous error reporting** to the self-hosted server (can be disabled by the user, forced on for the beta channel)

## Server Features & Self-Hosting

The client is fully open source, but the management server is **private**. To use the server-dependent features, you must implement your own backend and point the launcher to it.

Set the server URL in [`Plain Craft Launcher 2/Modules/ModSecret.vb`](Plain%20Craft%20Launcher%202/Modules/ModSecret.vb):

```vb
Public Const ServerAddress As String = ""  ' e.g. "http://your-server.com:8080/"
```

If `ServerAddress` is empty, **all server features are disabled** and the launcher works standalone (no announcements, no update checks, no beta/ban checks, no error reporting — everything degrades gracefully).

The client expects these HTTP endpoints:

| Endpoint | Method | Response |
|---|---|---|
| `Notice.cfg` | GET | Plain text, `\|`-separated: `Title\|SnapshotCode\|ReleaseCode\|ReleaseNotice\|SnapshotNotice\|BetaCode\|BetaNotice\|ReleaseChangelog\|SnapshotChangelog\|BetaChangelog` (use `\n` for newlines inside text; trailing changelog segments are optional) |
| `beta?ident={deviceId}&code={betaCode}` | GET | JSON `{"ok": bool, "msg": "...", "version_code": int}` |
| `ban?ident={deviceId}` | GET | JSON `{"banned": bool, "reason": "..."}` |
| `report` | POST | Accepts plain-text error reports (body) |
| `config.json` | GET | JSON `{"settings": {...}}` cloud configuration (optional) |
| `TYW Minecraft Launcher.exe` / `... Snapshot.exe` / `... Beta.exe` | GET | Per-channel launcher binaries for updates |

The device ID is a machine-derived fingerprint shown in the ban dialog, so users can send it to you for (un)banning.

## Building

Requirements: **Windows**, **Visual Studio 2022** (with the .NET desktop development workload) or the **.NET Framework 4.8 Developer Pack**.

```bash
msbuild "Plain Craft Launcher 2.sln" /restore /p:Configuration=Release /m
```

- `Debug` → Beta-channel build
- `Snapshot` → Snapshot channel
- `Release` → Stable channel

Or just push a tag (`v*`) — **GitHub Actions builds automatically and attaches the exe to a GitHub Release**.

## Repository Layout

```
Plain Craft Launcher 2/   Main launcher project (VB.NET WPF, .NET Framework 4.8)
PCLCS/                    PCL core support code (from the upstream open-source release)
MeloongCore/              Vendored MeloongCore library (Apache-2.0, from Meloong-Git/MeloongCore)
```

## License & Credits

- The original PCL2 code is © LTCat / Meloong-Git, released under its own license — see [`LICENCE`](LICENCE) (the upstream license file, kept unmodified).
- [`MeloongCore`](MeloongCore/LICENSE.txt) is licensed under **Apache-2.0** by the Meloong/PCL community.
- All modifications in this repository are by **TYW-MC**.
- The server-side implementation is proprietary and not part of this repository.

## Disclaimer

This launcher is a community modification and is not affiliated with Mojang or Microsoft. Minecraft is a trademark of Mojang Synergies AB.
