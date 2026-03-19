# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

ClaudePopUp is a Windows notification popup application for Claude Code. It displays rich notifications (task completions, errors, pending questions) with themed UI, markdown rendering, and response history. It integrates with Claude Code via hooks and named-pipe IPC.

## Build & Run Commands

```bash
# Build
dotnet build src/ClaudePopup.sln

# Build Release
dotnet build -c Release src/ClaudePopup.sln

# Run setup form (no arguments)
dotnet run --project src/ClaudePopup.Win

# Run popup with notification
dotnet run --project src/ClaudePopup.Win -- --title "Title" --message "Message" --type "info"

# Publish single-file executable
dotnet publish -c Release src/ClaudePopup.Win
```

There are no tests or linting configured.

## Architecture

**Single solution** (`src/ClaudePopup.sln`) with one WinForms project (`src/ClaudePopup.Win/`) targeting .NET 8.0 (net8.0-windows). Only external dependency is `Microsoft.Web.WebView2` for rendering HTML content. All classes share the `ClaudePopup` namespace.

### Folder Structure

```
src/ClaudePopup.Win/
  Program.cs                 Entry point, CLI arg parsing, single-instance mutex, pipe client
  Core/
    NativeMethods.cs         Shared P/Invoke (SetForegroundWindow, ShowWindow)
    NotificationType.cs      String constants: Info, Success, Error, Pending
    AppSettings.cs           Immutable record + JSON persistence (_data/settings.json)
  Popup/
    PopupAppContext.cs        Tray icon, context menu, pipe server loop
    PopupForm.cs             Main notification window (WebView2, animation, history nav)
    Sparkle.cs               Particle animation data class
  Settings/
    SettingsForm.cs          Settings dialog (theme picker, history toggle, snooze)
  Setup/
    SetupForm.cs             Installation wizard (copies exe, writes hook script, merges settings)
    SetupForm.resx           Designer resources
  Rendering/
    MarkdownRenderer.cs      Markdown-to-HTML converter with theme-aware CSS
    FunnyQuotes.cs           Programmer humor quotes for animated header
  Theme/
    PopupTheme.cs            PopupTheme record + Themes static class (7 built-in themes)
  Controls/
    RoundedButton.cs         Custom Button with rounded corners via GDI+
  Data/
    ResponseHistory.cs       Daily JSON history files in _data/history/, cached index
```

### Execution Flow

1. **First instance with no args** → opens `SetupForm` (installation wizard)
2. **First instance with args** (`--title`, `--message`, `--type`, `--message-file`, `--save-question`) → creates `PopupAppContext` with tray icon, starts pipe server, shows `PopupForm`
3. **Subsequent instances** → detect mutex, send args via named pipe to the running instance, then exit

### Conventions

- Notification type strings are centralized in `NotificationType` constants — use them instead of raw string literals.
- P/Invoke declarations live in `NativeMethods` — do not duplicate in individual forms.
- `AppSettingsData` is an immutable record — use `with` expressions to create modified copies.
- HTML encoding uses `System.Net.WebUtility.HtmlEncode` — do not create custom implementations.
- Empty `catch` blocks should include `Debug.WriteLine` for diagnostics.
- Files are organized by feature folder, not by type. New files go in the folder matching their feature area.
