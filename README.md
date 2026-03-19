# ClaudePopup

A Windows desktop notification popup for [Claude Code](https://claude.ai/code). Get rich popup notifications when Claude finishes a task, needs permission, or goes idle — so you can work in other windows without constantly checking the terminal.

## Features

- **Rich notifications** — Markdown-rendered responses with syntax highlighting, tables, code blocks, and styled conversation view (user question + Claude response)
- **Hook integration** — Automatically triggers on Claude Code's `Stop`, `Notification`, and `UserPromptSubmit` events
- **8 themes** — Ocean Blue, Deep Purple, Emerald, Amber, Rose, Cyberpunk, Mono (pure black), and Lite (light mode)
- **Response history** — Browse previous notifications with prev/next navigation
- **Snooze** — Suppress popups for 30 minutes when you need focus time
- **System tray** — Runs in the background with a tray icon; double-click to show the last notification
- **Single instance** — Multiple invocations communicate via named pipes to the running instance
- **Auto-update** — Configure a network path and the app checks hourly for new versions with one-click update
- **One-click install** — Setup wizard copies the exe, creates the hook script, and configures Claude Code settings automatically

## Requirements

- Windows 10/11
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (not included — framework-dependent)
- [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (pre-installed on Windows 10/11)

## Installation

### Option 1: Download and run

1. Download `ClaudePopup.exe` from the [Releases](../../releases) page (or from the `release/` folder)
2. Run it — the Setup wizard opens automatically
3. Click **Install Automatically** — this will:
   - Copy the exe to `~/.claude/tools/ClaudePopup/`
   - Create the PowerShell hook script at `~/.claude/hooks/Show-ClaudePopup.ps1`
   - Configure hooks in `~/.claude/settings.json`
4. Restart any running Claude Code instances

### Option 2: Build from source

```bash
git clone https://github.com/sukeshchand/ClaudePopUp.git
cd ClaudePopUp
dotnet publish -c Release src/ClaudePopup.Win
```

The published exe will be in `release/ClaudePopup.exe`. Run it to start the setup wizard.

## Usage

Once installed, ClaudePopup runs automatically via Claude Code hooks. You can also launch it manually:

```powershell
# Show a notification
ClaudePopup.exe --title "Claude Code" --message "Task completed!" --type success

# Show with message from file (avoids command-line length limits)
ClaudePopup.exe --title "Done" --message-file "C:\temp\response.txt" --type info
```

### Command-line arguments

| Argument | Description | Default |
|---|---|---|
| `--title`, `-t` | Window title | `"Claude Code"` |
| `--message`, `-m` | Body content (supports Markdown) | `"Task completed."` |
| `--message-file` | Read message from a file path | — |
| `--type` | Notification style: `info`, `success`, `error` | `info` |
| `--save-question` | Save a user question to history (used by hooks) | — |

### No arguments

- **From install directory** (`~/.claude/tools/ClaudePopup/`): Opens the popup with the last notification
- **From any other location**: Opens the Setup wizard

## Settings

Right-click the tray icon and select **Settings** to configure:

- **Theme** — Pick from 8 color themes
- **Response history** — Toggle saving daily response logs
- **Snooze** — Suppress popups for 30 minutes (shows remaining time)
- **Update location** — Set a network path for auto-updates (e.g. `\\server\share\ClaudePopup`)

## Auto-Update

ClaudePopup supports self-updating from a shared network location:

1. In Settings, enter the network path containing `ClaudePopup.exe` and `metadata.json`
2. The app checks hourly for new versions
3. When an update is available, a banner appears in the popup footer and in Settings
4. Click **Install Update** to apply — the app restarts automatically

### Publishing updates

Use the included publish script to build, version, and deploy:

```powershell
# Build and deploy to network share
.\publish.ps1 -DeployPath "\\server\share\ClaudePopup" -ReleaseNotes "Bug fixes"

# Local build only (auto-increments version)
.\publish.ps1

# Skip version bump
.\publish.ps1 -SkipVersionBump
```

This creates `ClaudePopup.exe` and `metadata.json` in the `release/` folder and optionally copies them to the deploy path.

## Hook Events

| Event | Behavior |
|---|---|
| **Stop** | Shows Claude's last response when it finishes |
| **Notification** | Shows permission prompts, idle prompts, and elicitation dialogs |
| **UserPromptSubmit** | Saves the user's question to history (displayed above Claude's response) |

## Project Structure

```
src/ClaudePopup.Win/
  Program.cs              Entry point, CLI parsing, single-instance mutex
  Core/                   Shared infrastructure (settings, version, update system)
  Popup/                  Main notification window and tray icon
  Settings/               Settings dialog
  Setup/                  Installation wizard
  Rendering/              Markdown-to-HTML converter and quotes
  Theme/                  8 built-in color themes
  Controls/               Custom UI controls
  Data/                   Response history persistence
```

## License

[MIT](LICENSE)
