using BilisPDF.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BilisPDF.Core.Tests;

[TestClass]
public sealed class OutputPathResolverTests
{
    [TestMethod]
    public void FindAvailable_WhenFileExists_AddsCounter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "BilisPDF.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var requested = Path.Combine(directory, "Merged.pdf");
            File.WriteAllText(requested, "existing");
            Assert.AreEqual(Path.Combine(directory, "Merged (1).pdf"), OutputPathResolver.FindAvailable(requested));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void GetValidActions_MixedKinds_ReturnsNone()
    {
        Assert.IsEmpty(SupportedFiles.GetValidActions(["one.pdf", "two.jpg"]));
    }
}
