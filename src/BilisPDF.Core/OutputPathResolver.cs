namespace BilisPDF.Core;

public static class OutputPathResolver
{
    public static string BesideSource(string sourcePath, string suffix, string extension = ".pdf")
    {
        var fullSource = Path.GetFullPath(sourcePath);
        var directory = Path.GetDirectoryName(fullSource)
            ?? throw new ArgumentException("The source path has no parent directory.", nameof(sourcePath));
        var baseName = Path.GetFileNameWithoutExtension(fullSource) + suffix;
        return FindAvailable(Path.Combine(directory, baseName + NormalizeExtension(extension)));
    }

    public static string FindAvailable(string requestedPath)
    {
        var fullPath = Path.GetFullPath(requestedPath);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            return fullPath;
        }

        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("The output path has no parent directory.", nameof(requestedPath));
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var extension = Path.GetExtension(fullPath);

        for (var index = 1; index <= 10_000; index++)
        {
            var candidate = Path.Combine(directory, $"{name} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("Could not allocate a unique output filename.");
    }

    private static string NormalizeExtension(string extension) =>
        extension.StartsWith('.') ? extension : "." + extension;
}
