using System.Text.Json;

namespace EasyPDF.Infrastructure;

public sealed record AppSettings(
    string OutputBehavior = "same-folder",
    string ConflictBehavior = "rename",
    bool Notifications = true,
    bool KeepHistory = false,
    string Theme = "system",
    int MaximumParallelConversions = 2,
    bool DiagnosticLogging = false);

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyPDF",
            "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.MaximumParallelConversions is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(settings), "Parallel conversions must be between 1 and 8.");
        }

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, _path, overwrite: true);
    }
}
