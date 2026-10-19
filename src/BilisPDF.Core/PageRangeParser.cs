using System.Globalization;

namespace BilisPDF.Core;

public static class PageRangeParser
{
    public const int MaximumExpressionLength = 4096;

    public static IReadOnlyList<int> Parse(string expression, int pageCount)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            throw new FormatException("Specify at least one page.");
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        if (expression.Length > MaximumExpressionLength)
        {
            throw new FormatException("The page range is too long.");
        }

        if (expression.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return Enumerable.Range(1, pageCount).ToArray();
        }

        var pages = new SortedSet<int>();
        foreach (var untrimmedPart in expression.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var part = untrimmedPart.Trim();
            if (part.Length == 0)
            {
                throw new FormatException("The page range contains an empty item.");
            }

            var dash = part.IndexOf('-');
            if (dash < 0)
            {
                pages.Add(ParsePage(part, pageCount));
                continue;
            }

            if (dash == 0 || dash == part.Length - 1 || part.IndexOf('-', dash + 1) >= 0)
            {
                throw new FormatException($"'{part}' is not a valid page range.");
            }

            var start = ParsePage(part[..dash].Trim(), pageCount);
            var end = ParsePage(part[(dash + 1)..].Trim(), pageCount);
            if (start > end)
            {
                throw new FormatException($"Page range '{part}' is descending.");
            }

            for (var page = start; page <= end; page++)
            {
                pages.Add(page);
            }
        }

        if (pages.Count == 0)
        {
            throw new FormatException("Specify at least one page.");
        }

        return pages.ToArray();
    }

    private static int ParsePage(string value, int pageCount)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var page) || page < 1)
        {
            throw new FormatException($"'{value}' is not a positive page number.");
        }

        if (page > pageCount)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"Page {page} exceeds the document's {pageCount} pages.");
        }

        return page;
    }
}
