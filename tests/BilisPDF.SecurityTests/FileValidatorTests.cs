using System.IO.Compression;
using BilisPDF.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BilisPDF.SecurityTests;

[TestClass]
public sealed class FileValidatorTests
{
    [TestMethod]
    public async Task ValidateAsync_MismatchedPdfSignature_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(path, "not a pdf");
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => new FileValidator().ValidateAsync(path, EasyFileKind.Pdf));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ValidateAsync_OfficeContainerMissingParts_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.docx");
        await using (var stream = File.Create(path))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            archive.CreateEntry("unexpected.txt");
        }

        try
        {
            await Assert.ThrowsExactlyAsync<InvalidDataException>(
                () => new FileValidator().ValidateAsync(path, EasyFileKind.OfficeDocument));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task ValidateManyAsync_ExcessiveFileCount_IsRejected()
    {
        var paths = Enumerable.Repeat("missing.pdf", 101).ToArray();
        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(
            () => new FileValidator().ValidateManyAsync(paths, EasyFileKind.Pdf));
    }
}
