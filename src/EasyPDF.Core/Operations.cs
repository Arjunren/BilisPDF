namespace EasyPDF.Core;

public interface IDocumentConverter
{
    Task<string> ConvertToPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default);
}

public interface IPdfService
{
    Task<string> MergeAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken cancellationToken = default);

    Task<string> ExtractPagesAsync(string inputPath, string pages, string outputPath, CancellationToken cancellationToken = default);

    Task<string> RotateAsync(string inputPath, int degrees, string pages, string outputPath, CancellationToken cancellationToken = default);

    Task<string> ImagesToPdfAsync(IReadOnlyList<string> inputPaths, string outputPath, CancellationToken cancellationToken = default);
}
