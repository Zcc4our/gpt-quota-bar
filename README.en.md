# GPT Quota Bar

[简体中文](README.md)

GPT Quota Bar is a standalone Windows utility that displays Codex subscription quota beside the top menu of the ChatGPT / Codex desktop app. Current version: **1.3.0**. This is not an official OpenAI product.

## Features

![Synthetic dark and light examples](assets/preview.png)

```text
1h58min/5h [progress] 49% | 4d3h/7d [progress] 48% · 已更新 10:40
```

- Shows returned quota windows, reset countdowns, remaining percentages and the last successful update time.
- Displays one or two windows according to the response. A Pro account may return only a weekly window; credit-only accounts show a credit state.
- Optionally displays a tray notification when a window reaches 10% or less. Each window duration and reset time is notified once per process run.
- Moves, minimizes and restores with the desktop app; clicks and drags pass through the bar. Dark and light themes are supported.
- Supports Windows logon startup and tray refresh. Polling is approximately every 30 seconds in the foreground and every 5 minutes in the background; the countdown is updated locally, and reset triggers a refresh.
- Clears stale values on read failure and shows the status. Narrow windows may omit the status or hide the bar.

Plus has been verified on the author's current computer. Pro single-window and credit-only responses have been tested with synthetic data, **not a real Pro account**. Go adapts to returned data but **has not been verified with a real Go account**. These are Codex subscription limits, not every ChatGPT model's message limits or API billing.

## How the code works

The application uses C#, WinForms and Windows APIs; most logic is in `src/GptQuotaBar.cs`.

1. **Startup and detection:** `StartupTask` registers the current-user logon task; `BarContext` manages the tray, host detection, display and reset refresh.
2. **Quota access:** `QuotaClient` starts official `codex.exe app-server` and exchanges requests over standard input/output. The official CLI manages authentication; the utility does not directly read `auth.json`.
3. **Parsing and drawing:** `Snapshot` selects only the Codex bucket and sorts windows by returned duration. `Renderer` draws remaining quota, credit states, times and errors. Failed reads clear old values.
4. **Windows and processes:** `BarForm` creates a non-activating native child window with mouse pass-through. Windows moves it with the host. A Job Object attempts to close the query subprocess when the watcher exits.

The utility searches the explicitly configured executable, desktop bundle and official local installation before `PATH`. A `PATH` candidate must pass Windows trust validation and have OpenAI in the signer subject. Users are responsible for verifying explicitly configured paths.

## Usage

### Requirements

- Windows 10 / 11 x64 and .NET Framework 4.8.
- The official ChatGPT / Codex desktop app and official `codex.exe` supporting `app-server`; sign in with your own ChatGPT account.
- An account and interface response containing Codex quota windows or credit information. Browser, macOS and Linux are not supported by this version.

### Install, update and uninstall

1. Download `GptQuotaBar-v1.3.0-windows-x64.zip` from **Releases** and extract it into a permanent folder writable only by the current Windows user.
2. Run `Install.cmd` to register the current-user logon task and start. Open the desktop app to display the bar.
3. Right-click the green tray icon to refresh, switch themes, toggle low-quota notifications or exit. The interface is currently Chinese.

| File | Action |
| --- | --- |
| `Install.cmd` | Register or update logon startup and start now |
| `Start.cmd` / `GptQuotaBar.exe` | Start once |
| `Stop.cmd` | Stop now, keeping the logon task |
| `Uninstall.cmd` | Remove the logon task and stop |

To update or move folders, run `Uninstall.cmd` in the old folder, then extract the new package and run its `Install.cmd`. After uninstalling, the runtime folder can be deleted.

If the bar is missing, check the tray icon, host visibility, available menu space and official CLI login. If necessary, set `GPT_QUOTA_CODEX_PATH` to the full path of the official `codex.exe` in a PowerShell session, then start the utility in that session.

### Build and data

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package.ps1
```

The build uses the Windows .NET Framework C# compiler without NuGet dependencies. The runtime package and checksum are written to `artifacts/releases/`.

Theme, notification preferences and diagnostics are stored under `%LOCALAPPDATA%\GptQuotaBar`, falling back to the executable folder if creation fails. Installation records omit executable paths. Diagnostics and quota snapshots may contain actual usage data and should not be published.
