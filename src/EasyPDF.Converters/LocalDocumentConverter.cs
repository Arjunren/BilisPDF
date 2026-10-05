using EasyPDF.Core;

namespace EasyPDF.Converters;

/// <summary>
/// Uses LibreOffice when it is installed, then falls back to a locally installed
/// Microsoft Office application. Neither path uploads the document.
/// </summary>
public sealed class LocalDocumentConverter : IDocumentConverter
{
    public Task<string> ConvertToPdfAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (LibreOfficeLocator.TryLocate(out var libreOffice))
        {
            return new LibreOfficeDocumentConverter(libreOffice)
                .ConvertToPdfAsync(inputPath, outputPath, cancellationToken);
        }

        return new MicrosoftOfficeDocumentConverter()
            .ConvertToPdfAsync(inputPath, outputPath, cancellationToken);
    }
}
