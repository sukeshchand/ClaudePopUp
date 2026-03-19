using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace ClaudePopup;

static class Program
{
    private const string MutexName = "ClaudePopup_SingleInstance_Mutex";
    internal const string PipeName = "ClaudePopup_Pipe";

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // No arguments → show setup instructions (always new instance)
        if (args.Length == 0)
        {
            Application.Run(new SetupForm());
            return;
        }

        string title = "Claude Code";
        string message = "Task completed.";
        string type = NotificationType.Info;
        string? messageFile = null;
        string? saveQuestion = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--title" or "-t" when i + 1 < args.Length:
                    title = args[++i];
                    break;
                case "--message" or "-m" when i + 1 < args.Length:
                    message = args[++i];
                    break;
                case "--message-file" when i + 1 < args.Length:
                    messageFile = args[++i];
                    break;
                case "--save-question" when i + 1 < args.Length:
                    saveQuestion = args[++i];
                    break;
                case "--type" when i + 1 < args.Length:
                    type = args[++i].ToLowerInvariant();
                    break;
            }
        }

        // Save question to history and exit (UserPromptSubmit hook)
        if (saveQuestion != null)
        {
            // Read from file if it's a file path
            if (File.Exists(saveQuestion))
                saveQuestion = File.ReadAllText(saveQuestion, Encoding.UTF8);
            ResponseHistory.SaveQuestion(saveQuestion.Replace("\\n", "\n").Replace("\\t", "\t").Trim());
            return;
        }

        // Read message from file if specified (avoids command-line length limits)
        if (messageFile != null && File.Exists(messageFile))
            message = File.ReadAllText(messageFile, Encoding.UTF8);

        message = message.Replace("\\n", "\n").Replace("\\t", "\t");

        // Save response to history (completes pending question entry)
        ResponseHistory.SaveResponse(title, message, type);

        using var mutex = new Mutex(true, MutexName, out bool isNewInstance);

        if (!isNewInstance)
        {
            SendToPipe(title, message, type);
            return;
        }

        Application.Run(new PopupAppContext(title, message, type));
    }

    private static void SendToPipe(string title, string message, string type)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(3000);
            var payload = JsonSerializer.Serialize(new { title, message, type });
            var bytes = Encoding.UTF8.GetBytes(payload);
            client.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SendToPipe failed: {ex.Message}");
        }
    }
}
