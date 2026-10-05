namespace EasyPDF.Core;

public static class AtomicFile
{
    public static async Task<string> WriteAsync(
        string outputPath,
        Func<string, CancellationToken, Task> writer,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var fullOutput = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutput)
            ?? throw new ArgumentException("The output path has no parent directory.", nameof(outputPath));
        Directory.CreateDirectory(directory);

        if (File.Exists(fullOutput) || Directory.Exists(fullOutput))
        {
            throw new IOException($"Output already exists: {fullOutput}");
        }

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullOutput)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await writer(temporaryPath, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullOutput, overwrite: false);
            return fullOutput;
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; the rotating diagnostics layer records the operation result.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort cleanup.
            }
        }
    }
}
