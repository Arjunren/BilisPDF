using BilisPDF.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BilisPDF.Core.Tests;

[TestClass]
public sealed class SupportedFilesTests
{
    [TestMethod]
    public void MultiplePdfs_OnlyOfferMerge_SingleFileActionsMustNotIgnoreOtherFiles()
    {
        CollectionAssert.AreEqual(new[] { "merge" }, SupportedFiles.GetValidActions(["first.pdf", "second.PDF"]).ToArray());
    }

    [TestMethod]
    public void SinglePdf_OffersPageOperations()
    {
        CollectionAssert.AreEqual(new[] { "split", "extract-pages", "rotate" }, SupportedFiles.GetValidActions(["first.pdf"]).ToArray());
    }
}
