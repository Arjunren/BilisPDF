using BilisPDF.Converters;
using BilisPDF.Core;
using BilisPDF.Pdf;

namespace BilisPDF.Cli;

public static class CliApplication
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || args[0] is "help" or "--help" or "-h")
        {
            WriteHelp();
            return 0;
        }

        try
        {
            var command = args[0].ToLowerInvariant();
            var parsed = Arguments.Parse(args[1..]);
            var pdf = new PdfService();
            string result;
            switch (command)
            {
                case "convert-to-pdf":
                    {
                        var input = parsed.RequireSingleInput();
                        var output = parsed.Output ?? OutputPathResolver.BesideSource(input, string.Empty);
                        result = await new LocalDocumentConverter().ConvertToPdfAsync(input, output, cancellationToken);
                        break;
                    }
                case "merge":
                    {
                        parsed.RequireInputCount(2);
                        var output = parsed.Output ?? OutputPathResolver.FindAvailable(
                            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(parsed.Inputs[0]))!, "Merged.pdf"));
                        result = await pdf.MergeAsync(parsed.Inputs, output, cancellationToken);
                        break;
                    }
                case "extract-pages":
                case "split":
                    {
                        var input = parsed.RequireSingleInput();
                        var pageRange = parsed.Pages ?? throw new CliUsageException("Specify --pages, for example --pages \"1-3,8\".");
                        var suffix = command == "split" ? "-split" : "-extracted";
                        var output = parsed.Output ?? OutputPathResolver.BesideSource(input, suffix);
                        result = await pdf.ExtractPagesAsync(input, pageRange, output, cancellationToken);
                        break;
                    }
                case "rotate":
                    {
                        var input = parsed.RequireSingleInput();
                        var degrees = parsed.Degrees ?? throw new CliUsageException("Specify --degrees 90, -90, 180, or 270.");
                        var output = parsed.Output ?? OutputPathResolver.BesideSource(input, "-rotated");
                        result = await pdf.RotateAsync(input, degrees, parsed.Pages ?? "all", output, cancellationToken);
                        break;
                    }
                case "images-to-pdf":
                    {
                        parsed.RequireInputCount(1);
                        var output = parsed.Output ?? OutputPathResolver.FindAvailable(
                            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(parsed.Inputs[0]))!, "Images.pdf"));
                        result = await pdf.ImagesToPdfAsync(parsed.Inputs, output, cancellationToken);
                        break;
                    }
                default:
                    throw new CliUsageException($"Unknown command '{command}'.");
            }

            Console.WriteLine(result);
            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Operation cancelled.");
            return 130;
        }
        catch (CliUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Run 'bilispdf help' for usage.");
            return 2;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or NotSupportedException or TimeoutException)
        {
            Console.Error.WriteLine(ToUserMessage(exception));
            return 3;
        }
    }

    private static string ToUserMessage(Exception exception) => exception switch
    {
        FileNotFoundException => exception.Message,
        InvalidDataException => "BilisPDF could not process the file because it appears damaged, unsafe, encrypted, or unsupported.",
        TimeoutException => exception.Message,
        _ => exception.Message
    };

    private static void WriteHelp()
    {
        Console.WriteLine("""
            BilisPDF CLI — free, offline PDF tools by Arjunren Von

            Commands:
              bilispdf convert-to-pdf <input> [--output <pdf>]
              bilispdf merge <pdf> <pdf> [...] [--output <pdf>]
              bilispdf extract-pages <pdf> --pages <range> [--output <pdf>]
              bilispdf split <pdf> --pages <range> [--output <pdf>]
              bilispdf rotate <pdf> --degrees <90|-90|180|270> [--pages <range|all>] [--output <pdf>]
              bilispdf images-to-pdf <image> [...] [--output <pdf>]

            Existing output files are never overwritten. Page ranges use forms such as 1-5,8,12-15.
            """);
    }

    private sealed class Arguments
    {
        public List<string> Inputs { get; } = [];

        public string? Output { get; private set; }

        public string? Pages { get; private set; }

        public int? Degrees { get; private set; }

        public static Arguments Parse(string[] args)
        {
            var parsed = new Arguments();
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                switch (argument)
                {
                    case "--output":
                        parsed.Output = RequireOptionValue(args, ref index, argument);
                        break;
                    case "--pages":
                        parsed.Pages = RequireOptionValue(args, ref index, argument);
                        break;
                    case "--degrees":
                        var value = RequireOptionValue(args, ref index, argument);
                        if (!int.TryParse(value, out var degrees))
                        {
                            throw new CliUsageException("--degrees must be a number.");
                        }

                        parsed.Degrees = degrees;
                        break;
                    default:
                        if (argument.StartsWith("-", StringComparison.Ordinal) && !Path.Exists(argument))
                        {
                            throw new CliUsageException($"Unknown option '{argument}'.");
                        }

                        parsed.Inputs.Add(argument);
                        break;
                }
            }

            return parsed;
        }

        public string RequireSingleInput()
        {
            if (Inputs.Count != 1)
            {
                throw new CliUsageException("This command requires exactly one input file.");
            }

            return Inputs[0];
        }

        public void RequireInputCount(int minimum)
        {
            if (Inputs.Count < minimum)
            {
                throw new CliUsageException($"This command requires at least {minimum} input file(s).");
            }
        }

        private static string RequireOptionValue(string[] args, ref int index, string option)
        {
            if (++index >= args.Length)
            {
                throw new CliUsageException($"{option} requires a value.");
            }

            return args[index];
        }
    }

    private sealed class CliUsageException(string message) : Exception(message);
}
