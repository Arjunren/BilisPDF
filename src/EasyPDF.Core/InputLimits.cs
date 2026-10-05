namespace EasyPDF.Core;

public sealed record InputLimits(
    long MaximumFileBytes = 512L * 1024 * 1024,
    int MaximumFiles = 100,
    int MaximumPdfPages = 10_000,
    long MaximumImagePixels = 100_000_000);
