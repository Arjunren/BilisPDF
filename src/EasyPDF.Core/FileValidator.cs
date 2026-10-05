using System.IO.Compression;

namespace EasyPDF.Core;

public sealed class FileValidator(InputLimits? limits = null)
{
    private readonly InputLimits _limits = limits ?? new InputLimits();

    public async Task<string> ValidateAsync(string path, EasyFileKind expectedKind, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("The input file does not exist.", fullPath);
        }

        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Reparse-point inputs are not accepted.");
        }

        if (info.Length == 0 || info.Length > _limits.MaximumFileBytes)
        {
            throw new IOException($"The input must be between 1 byte and {_limits.MaximumFileBytes} bytes.");
        }

        var actualKind = SupportedFiles.GetKind(fullPath);
        if (actualKind != expectedKind)
        {
            throw new NotSupportedException($"'{Path.GetExtension(fullPath)}' is not valid for this operation.");
        }

        await ValidateSignatureAsync(fullPath, actualKind, cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    public async Task<IReadOnlyList<string>> ValidateManyAsync(
        IEnumerable<string> paths,
        EasyFileKind expectedKind,
        CancellationToken cancellationToken = default)
    {
        var items = paths.ToArray();
        if (items.Length == 0 || items.Length > _limits.MaximumFiles)
        {
            throw new ArgumentOutOfRangeException(nameof(paths), $"Select between 1 and {_limits.MaximumFiles} files.");
        }

        var validated = new List<string>(items.Length);
        foreach (var path in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            validated.Add(await ValidateAsync(path, expectedKind, cancellationToken).ConfigureAwait(false));
        }

        return validated;
    }

    private static async Task ValidateSignatureAsync(string path, EasyFileKind kind, CancellationToken cancellationToken)
    {
        if (kind == EasyFileKind.TextDocument)
        {
            return;
        }

        var header = new byte[12];
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var read = await stream.ReadAsync(header, cancellationToken).ConfigureAwait(false);
            if (read < 4)
            {
                throw new InvalidDataException("The file is too short to identify.");
            }
        }

        var extension = Path.GetExtension(path).ToLowerInvariant();
        var valid = extension switch
        {
            ".pdf" => header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".docx" or ".xlsx" or ".pptx" => header[0] == (byte)'P' && header[1] == (byte)'K',
            ".png" => header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".bmp" => header[0] == (byte)'B' && header[1] == (byte)'M',
            ".tif" or ".tiff" => (header[0] == (byte)'I' && header[1] == (byte)'I') || (header[0] == (byte)'M' && header[1] == (byte)'M'),
            ".webp" => header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => true
        };

        if (!valid)
        {
            throw new InvalidDataException("The file signature does not match its extension.");
        }

        if (kind == EasyFileKind.OfficeDocument)
        {
            ValidateOfficeContainer(path, extension);
        }
    }

    private static void ValidateOfficeContainer(string path, string extension)
    {
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > 100_000)
        {
            throw new InvalidDataException("The Office document contains too many parts.");
        }

        const long maximumExpandedBytes = 2L * 1024 * 1024 * 1024;
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > maximumExpandedBytes)
            {
                throw new InvalidDataException("The Office document expands beyond the safety limit.");
            }
        }

        var requiredPart = extension switch
        {
            ".docx" => "word/document.xml",
            ".xlsx" => "xl/workbook.xml",
            ".pptx" => "ppt/presentation.xml",
            _ => throw new NotSupportedException()
        };

        if (archive.GetEntry("[Content_Types].xml") is null || archive.GetEntry(requiredPart) is null)
        {
            throw new InvalidDataException("The Office document container is missing required parts.");
        }
    }
}
