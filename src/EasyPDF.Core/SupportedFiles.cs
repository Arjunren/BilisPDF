using System.Collections.Frozen;

namespace EasyPDF.Core;

public enum EasyFileKind
{
    Pdf,
    OfficeDocument,
    TextDocument,
    Image,
    Unknown
}

public static class SupportedFiles
{
    private static readonly FrozenDictionary<string, EasyFileKind> Kinds =
        new Dictionary<string, EasyFileKind>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = EasyFileKind.Pdf,
            [".docx"] = EasyFileKind.OfficeDocument,
            [".xlsx"] = EasyFileKind.OfficeDocument,
            [".pptx"] = EasyFileKind.OfficeDocument,
            [".txt"] = EasyFileKind.TextDocument,
            [".rtf"] = EasyFileKind.TextDocument,
            [".html"] = EasyFileKind.TextDocument,
            [".htm"] = EasyFileKind.TextDocument,
            [".png"] = EasyFileKind.Image,
            [".jpg"] = EasyFileKind.Image,
            [".jpeg"] = EasyFileKind.Image,
            [".bmp"] = EasyFileKind.Image,
            [".tif"] = EasyFileKind.Image,
            [".tiff"] = EasyFileKind.Image,
            [".webp"] = EasyFileKind.Image
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static EasyFileKind GetKind(string path) =>
        Kinds.TryGetValue(Path.GetExtension(path), out var kind) ? kind : EasyFileKind.Unknown;

    public static bool IsSupported(string path) => GetKind(path) != EasyFileKind.Unknown;

    public static IReadOnlyList<string> GetValidActions(IEnumerable<string> paths)
    {
        var kinds = paths.Select(GetKind).ToArray();
        if (kinds.Length == 0 || kinds.Contains(EasyFileKind.Unknown))
        {
            return [];
        }

        if (kinds.All(kind => kind == EasyFileKind.Pdf))
        {
            return kinds.Length > 1
                ? ["merge", "rotate"]
                : ["split", "extract-pages", "rotate"];
        }

        if (kinds.All(kind => kind == EasyFileKind.Image))
        {
            return ["images-to-pdf"];
        }

        if (kinds.Length == 1 && kinds[0] is EasyFileKind.OfficeDocument or EasyFileKind.TextDocument)
        {
            return ["convert-to-pdf"];
        }

        return [];
    }
}
