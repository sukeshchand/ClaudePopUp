using System.Diagnostics;
using System.Text;

namespace ClaudePopup;

static class Updater
{
    public record UpdateResult(bool Success, string Message);

    public static UpdateResult Apply()
    {
        try
        {
            var settings = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.UpdateLocation))
                return new UpdateResult(false, "No update location configured.");

            string sourceExe = Path.Combine(settings.UpdateLocation, "ClaudePopup.exe");
            if (!File.Exists(sourceExe))
                return new UpdateResult(false, $"Update file not found at {sourceExe}");

            string installDir = Path.GetDirectoryName(Application.ExecutablePath)!;
            string currentExe = Application.ExecutablePath;
            string updateExe = Path.Combine(installDir, "ClaudePopup.update.exe");
            string batchPath = Path.Combine(installDir, "_update.bat");

            // Step 1: Copy new exe from network to local staging file
            File.Copy(sourceExe, updateExe, overwrite: true);

            // Step 2: Regenerate the hook script (in case it changed)
            RegenerateHookScript(currentExe);

            // Step 3: Write the updater batch script
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("timeout /t 2 /nobreak >nul");
            sb.AppendLine($"if exist \"{currentExe}\" del /f /q \"{currentExe}\"");
            sb.AppendLine($"if exist \"{updateExe}\" rename \"{updateExe}\" \"{Path.GetFileName(currentExe)}\"");
            sb.AppendLine($"start \"\" \"{currentExe}\"");
            sb.AppendLine($"del /f /q \"{batchPath}\" & exit");

            File.WriteAllText(batchPath, sb.ToString(), Encoding.ASCII);

            // Step 4: Launch the batch script and exit
            Process.Start(new ProcessStartInfo
            {
                FileName = batchPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = installDir,
            });

            return new UpdateResult(true, "Update started. Application will restart.");
        }
        catch (Exception ex)
        {
            return new UpdateResult(false, $"Update failed: {ex.Message}");
        }
    }

    private static void RegenerateHookScript(string exePath)
    {
        try
        {
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string hooksDir = Path.Combine(userProfile, ".claude", "hooks");
            string ps1Path = Path.Combine(hooksDir, "Show-ClaudePopup.ps1");

            Directory.CreateDirectory(hooksDir);

            string ps1Content = $@"[Console]::InputEncoding = [System.Text.Encoding]::UTF8
$inputJson = [Console]::In.ReadToEnd()

$title    = ""Claude Code""
$message  = ""Claude finished.""
$type     = ""success""

$exePath = ""{exePath}""

if ($inputJson) {{
    try {{
        $payload = $inputJson | ConvertFrom-Json

        if ($payload.hook_event_name -eq ""UserPromptSubmit"") {{
            # Save question to history via ClaudePopup and exit
            if ($payload.prompt) {{
                $qFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), ""claudepopup_question.txt"")
                [System.IO.File]::WriteAllText($qFile, $payload.prompt, [System.Text.Encoding]::UTF8)
                Start-Process -FilePath $exePath -ArgumentList ""--save-question"", ""`""$qFile`"""" -WindowStyle Hidden
            }}
            exit 0
        }}
        elseif ($payload.hook_event_name -eq ""Notification"") {{
            if ($payload.title)   {{ $title = $payload.title }}
            if ($payload.message) {{ $message = $payload.message }}
            $type = ""info""
        }}
        elseif ($payload.hook_event_name -eq ""Stop"") {{
            $title = ""Claude Code - Done""
            if ($payload.last_assistant_message) {{
                $message = $payload.last_assistant_message
            }} else {{
                $message = ""Claude finished its response.""
            }}
            $type = ""success""
        }}
    }}
    catch {{ }}
}}

$msgFile = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), ""claudepopup_msg.txt"")
[System.IO.File]::WriteAllText($msgFile, $message, [System.Text.Encoding]::UTF8)

$argList = @(""--title"", ""`""$title`"""", ""--message-file"", ""`""$msgFile`"""", ""--type"", $type)
Start-Process -FilePath $exePath -ArgumentList $argList -WindowStyle Hidden";

            File.WriteAllText(ps1Path, ps1Content, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to regenerate hook script: {ex.Message}");
        }
    }
}
