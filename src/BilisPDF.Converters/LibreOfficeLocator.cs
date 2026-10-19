namespace BilisPDF.Converters;

public static class LibreOfficeLocator
{
    public static string Locate()
    {
        if (TryLocate(out var executable))
        {
            return executable;
        }

        throw new FileNotFoundException(
            "LibreOffice was not found. Install LibreOffice locally or set BILISPDF_LIBREOFFICE_PATH to soffice.exe.");
    }

    public static bool TryLocate(out string executable)
    {
        var configured = Environment.GetEnvironmentVariable("BILISPDF_LIBREOFFICE_PATH");
        foreach (var candidate in GetCandidates(configured))
        {
            if (File.Exists(candidate))
            {
                executable = Path.GetFullPath(candidate);
                return true;
            }
        }

        executable = string.Empty;
        return false;
    }

    private static IEnumerable<string> GetCandidates(string? configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            yield return Directory.Exists(configured) ? Path.Combine(configured, "soffice.exe") : configured;
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        yield return Path.Combine(programFiles, "LibreOffice", "program", "soffice.exe");
        yield return Path.Combine(programFilesX86, "LibreOffice", "program", "soffice.exe");
    }
}
