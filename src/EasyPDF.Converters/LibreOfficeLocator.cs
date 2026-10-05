namespace EasyPDF.Converters;

public static class LibreOfficeLocator
{
    public static string Locate()
    {
        var configured = Environment.GetEnvironmentVariable("EASYPDF_LIBREOFFICE_PATH");
        foreach (var candidate in GetCandidates(configured))
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        throw new FileNotFoundException(
            "LibreOffice was not found. Install LibreOffice locally or set EASYPDF_LIBREOFFICE_PATH to soffice.exe.");
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
