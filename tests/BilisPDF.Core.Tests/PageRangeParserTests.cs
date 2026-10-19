using BilisPDF.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BilisPDF.Core.Tests;

[TestClass]
public sealed class PageRangeParserTests
{
    [TestMethod]
    public void Parse_MixedRanges_ReturnsUniqueSortedPages()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 5, 8, 9 }, PageRangeParser.Parse("1-3,5,8-9,3", 10).ToArray());
    }

    [TestMethod]
    public void Parse_All_ReturnsEveryPage()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, PageRangeParser.Parse("all", 3).ToArray());
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("0")]
    [DataRow("3-1")]
    [DataRow("1--2")]
    [DataRow("x")]
    public void Parse_InvalidInput_Throws(string expression)
    {
        Assert.ThrowsExactly<FormatException>(() => PageRangeParser.Parse(expression, 10));
    }

    [TestMethod]
    public void Parse_PageBeyondDocument_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => PageRangeParser.Parse("11", 10));
    }
}
