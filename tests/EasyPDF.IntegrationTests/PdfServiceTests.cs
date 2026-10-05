using EasyPDF.Pdf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace EasyPDF.IntegrationTests;

[TestClass]
public sealed class PdfServiceTests
{
    [TestMethod]
    public async Task MergeAndExtract_ProduceExpectedPageCounts()
    {
        var directory = Path.Combine(Path.GetTempPath(), "EasyPDF.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var first = CreatePdf(directory, "first.pdf", 2);
            var second = CreatePdf(directory, "second.pdf", 1);
            var merged = Path.Combine(directory, "merged.pdf");
            var extracted = Path.Combine(directory, "extracted.pdf");
            var service = new PdfService();

            await service.MergeAsync([first, second], merged);
            await service.ExtractPagesAsync(merged, "2-3", extracted);

            using var mergedDocument = PdfReader.Open(merged, PdfDocumentOpenMode.Import);
            using var extractedDocument = PdfReader.Open(extracted, PdfDocumentOpenMode.Import);
            Assert.AreEqual(3, mergedDocument.PageCount);
            Assert.AreEqual(2, extractedDocument.PageCount);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreatePdf(string directory, string name, int pages)
    {
        var path = Path.Combine(directory, name);
        using var document = new PdfDocument();
        for (var index = 0; index < pages; index++)
        {
            document.AddPage();
        }

        document.Save(path);
        return path;
    }
}
