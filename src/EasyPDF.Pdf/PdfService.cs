using EasyPDF.Core;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace EasyPDF.Pdf;

public sealed class PdfService(InputLimits? limits = null) : IPdfService
{
    private readonly InputLimits _limits = limits ?? new InputLimits();
    private readonly FileValidator _validator = new(limits);

    public async Task<string> MergeAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (inputPaths.Count < 2)
        {
            throw new ArgumentException("Merge requires at least two PDF files.", nameof(inputPaths));
        }

        var inputs = await _validator.ValidateManyAsync(inputPaths, EasyFileKind.Pdf, cancellationToken).ConfigureAwait(false);
        return await AtomicFile.WriteAsync(outputPath, (temporaryPath, token) =>
        {
            using var output = new PdfDocument();
            var totalPages = 0;
            foreach (var input in inputs)
            {
                token.ThrowIfCancellationRequested();
                using var document = PdfReader.Open(input, PdfDocumentOpenMode.Import);
                totalPages = checked(totalPages + document.PageCount);
                if (totalPages > _limits.MaximumPdfPages)
                {
                    throw new InvalidDataException($"The merged document exceeds {_limits.MaximumPdfPages} pages.");
                }

                for (var index = 0; index < document.PageCount; index++)
                {
                    token.ThrowIfCancellationRequested();
                    output.AddPage(document.Pages[index]);
                }
            }

            output.Save(temporaryPath);
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ExtractPagesAsync(
        string inputPath,
        string pages,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var input = await _validator.ValidateAsync(inputPath, EasyFileKind.Pdf, cancellationToken).ConfigureAwait(false);
        return await AtomicFile.WriteAsync(outputPath, (temporaryPath, token) =>
        {
            using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            EnsurePageLimit(source.PageCount);
            var selectedPages = PageRangeParser.Parse(pages, source.PageCount);
            using var output = new PdfDocument();
            foreach (var pageNumber in selectedPages)
            {
                token.ThrowIfCancellationRequested();
                output.AddPage(source.Pages[pageNumber - 1]);
            }

            output.Save(temporaryPath);
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> RotateAsync(
        string inputPath,
        int degrees,
        string pages,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (degrees is not (90 or -90 or 180 or 270))
        {
            throw new ArgumentOutOfRangeException(nameof(degrees), "Rotation must be 90, -90, 180, or 270 degrees.");
        }

        var input = await _validator.ValidateAsync(inputPath, EasyFileKind.Pdf, cancellationToken).ConfigureAwait(false);
        return await AtomicFile.WriteAsync(outputPath, (temporaryPath, token) =>
        {
            using var source = PdfReader.Open(input, PdfDocumentOpenMode.Import);
            EnsurePageLimit(source.PageCount);
            var selectedPages = PageRangeParser.Parse(pages, source.PageCount).ToHashSet();
            using var output = new PdfDocument();
            for (var index = 0; index < source.PageCount; index++)
            {
                token.ThrowIfCancellationRequested();
                var outputPage = output.AddPage(source.Pages[index]);
                if (selectedPages.Contains(index + 1))
                {
                    outputPage.Rotate = NormalizeRotation(outputPage.Rotate + degrees);
                }
            }

            output.Save(temporaryPath);
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ImagesToPdfAsync(
        IReadOnlyList<string> inputPaths,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var inputs = await _validator.ValidateManyAsync(inputPaths, EasyFileKind.Image, cancellationToken).ConfigureAwait(false);
        return await AtomicFile.WriteAsync(outputPath, (temporaryPath, token) =>
        {
            using var output = new PdfDocument();
            foreach (var input in inputs)
            {
                token.ThrowIfCancellationRequested();
                using var image = XImage.FromFile(input);
                var pixels = checked((long)image.PixelWidth * image.PixelHeight);
                if (pixels > _limits.MaximumImagePixels)
                {
                    throw new InvalidDataException($"'{Path.GetFileName(input)}' exceeds the image dimension safety limit.");
                }

                var page = output.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                page.Orientation = image.PixelWidth > image.PixelHeight
                    ? PdfSharp.PageOrientation.Landscape
                    : PdfSharp.PageOrientation.Portrait;

                const double margin = 36;
                var availableWidth = page.Width.Point - (margin * 2);
                var availableHeight = page.Height.Point - (margin * 2);
                var scale = Math.Min(availableWidth / image.PointWidth, availableHeight / image.PointHeight);
                var width = image.PointWidth * scale;
                var height = image.PointHeight * scale;
                using var graphics = XGraphics.FromPdfPage(page);
                graphics.DrawImage(image, (page.Width.Point - width) / 2, (page.Height.Point - height) / 2, width, height);
            }

            output.Save(temporaryPath);
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
    }

    private void EnsurePageLimit(int pageCount)
    {
        if (pageCount < 1 || pageCount > _limits.MaximumPdfPages)
        {
            throw new InvalidDataException($"The PDF must contain between 1 and {_limits.MaximumPdfPages} pages.");
        }
    }

    private static int NormalizeRotation(int degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }
}
