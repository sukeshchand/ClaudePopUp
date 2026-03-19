using System.Diagnostics;
using System.Text.Json;

namespace ClaudePopup;

static class UpdateChecker
{
    private static System.Threading.Timer? _timer;
    private static UpdateMetadata? _latestMetadata;

    public static event Action<UpdateMetadata>? UpdateAvailable;

    public static UpdateMetadata? LatestMetadata => _latestMetadata;

    public static void Start()
    {
        // Check immediately, then every hour
        _timer = new System.Threading.Timer(_ => CheckForUpdate(), null, TimeSpan.Zero, TimeSpan.FromHours(1));
    }

    public static void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public static void CheckNow() => CheckForUpdate();

    private static void CheckForUpdate()
    {
        try
        {
            var settings = AppSettings.Load();
            if (string.IsNullOrWhiteSpace(settings.UpdateLocation))
                return;

            string metadataPath = Path.Combine(settings.UpdateLocation, "metadata.json");
            if (!File.Exists(metadataPath))
                return;

            string json = File.ReadAllText(metadataPath);
            var metadata = JsonSerializer.Deserialize<UpdateMetadata>(json);
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.Version))
                return;

            if (IsNewerVersion(metadata.Version, AppVersion.Current))
            {
                _latestMetadata = metadata;
                UpdateAvailable?.Invoke(metadata);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    internal static bool IsNewerVersion(string remote, string local)
    {
        if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(local, out var localVer))
            return remoteVer > localVer;
        return false;
    }
}
