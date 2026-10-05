using System.Runtime.InteropServices;
using EasyPDF.Core;

namespace EasyPDF.Converters;

/// <summary>
/// Optional local fallback for machines that already have Microsoft Office.
/// Office is automated invisibly with macros disabled and the source opened read-only.
/// </summary>
public sealed class MicrosoftOfficeDocumentConverter(InputLimits? limits = null) : IDocumentConverter
{
    private readonly FileValidator _validator = new(limits);

    public async Task<string> ConvertToPdfAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var kind = SupportedFiles.GetKind(inputPath);
        if (kind is not (EasyFileKind.OfficeDocument or EasyFileKind.TextDocument))
        {
            throw new NotSupportedException("This input format cannot be converted to PDF.");
        }

        var input = await _validator.ValidateAsync(inputPath, kind, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return await AtomicFile.WriteAsync(outputPath, async (temporaryOutput, token) =>
        {
            var workRoot = Path.Combine(Path.GetTempPath(), "EasyPDF", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workRoot);
            var produced = Path.Combine(workRoot, "converted.pdf");

            try
            {
                await RunStaAsync(() => ConvertWithOffice(input, produced), token).ConfigureAwait(false);
                if (!File.Exists(produced) || new FileInfo(produced).Length < 5)
                {
                    throw new InvalidDataException("Microsoft Office did not produce a valid PDF output.");
                }

                await using (var stream = File.OpenRead(produced))
                {
                    var signature = new byte[5];
                    if (await stream.ReadAsync(signature, token).ConfigureAwait(false) != signature.Length ||
                        !signature.AsSpan().SequenceEqual("%PDF-"u8))
                    {
                        throw new InvalidDataException("The conversion output is not a PDF file.");
                    }
                }

                File.Copy(produced, temporaryOutput, overwrite: false);
            }
            finally
            {
                TryDeleteDirectory(workRoot);
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private static void ConvertWithOffice(string inputPath, string outputPath)
    {
        var extension = Path.GetExtension(inputPath);
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ConvertWithExcel(inputPath, outputPath);
            return;
        }

        if (extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
        {
            ConvertWithPowerPoint(inputPath, outputPath);
            return;
        }

        ConvertWithWord(inputPath, outputPath);
    }

    private static void ConvertWithWord(string inputPath, string outputPath)
    {
        dynamic? application = null;
        dynamic? document = null;
        try
        {
            application = CreateApplication("Word.Application", "Microsoft Word");
            application.Visible = false;
            application.DisplayAlerts = 0;
            application.AutomationSecurity = 3;
            document = application.Documents.Open(
                FileName: inputPath,
                ConfirmConversions: false,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: false,
                OpenAndRepair: true,
                NoEncodingDialog: true);
            document.ExportAsFixedFormat(
                OutputFileName: outputPath,
                ExportFormat: 17,
                OpenAfterExport: false,
                OptimizeFor: 0,
                Range: 0,
                Item: 0,
                IncludeDocProps: true,
                KeepIRM: true,
                CreateBookmarks: 1,
                DocStructureTags: true,
                BitmapMissingFonts: true,
                UseISO19005_1: false);
        }
        finally
        {
            TryInvoke(() => document?.Close(false));
            ReleaseComObject(document);
            TryInvoke(() => application?.Quit(false));
            ReleaseComObject(application);
        }
    }

    private static void ConvertWithExcel(string inputPath, string outputPath)
    {
        dynamic? application = null;
        dynamic? workbook = null;
        try
        {
            application = CreateApplication("Excel.Application", "Microsoft Excel");
            application.Visible = false;
            application.DisplayAlerts = false;
            application.AutomationSecurity = 3;
            workbook = application.Workbooks.Open(
                Filename: inputPath,
                UpdateLinks: 0,
                ReadOnly: true,
                AddToMru: false,
                IgnoreReadOnlyRecommended: true);
            workbook.ExportAsFixedFormat(
                Type: 0,
                Filename: outputPath,
                Quality: 0,
                IncludeDocProperties: true,
                IgnorePrintAreas: false,
                OpenAfterPublish: false);
        }
        finally
        {
            TryInvoke(() => workbook?.Close(false));
            ReleaseComObject(workbook);
            TryInvoke(() => application?.Quit());
            ReleaseComObject(application);
        }
    }

    private static void ConvertWithPowerPoint(string inputPath, string outputPath)
    {
        dynamic? application = null;
        dynamic? presentation = null;
        try
        {
            application = CreateApplication("PowerPoint.Application", "Microsoft PowerPoint");
            application.AutomationSecurity = 3;
            presentation = application.Presentations.Open(
                FileName: inputPath,
                ReadOnly: -1,
                Untitled: 0,
                WithWindow: 0);
            presentation.SaveAs(outputPath, 32, -1);
        }
        finally
        {
            TryInvoke(() => presentation?.Close());
            ReleaseComObject(presentation);
            TryInvoke(() => application?.Quit());
            ReleaseComObject(application);
        }
    }

    private static dynamic CreateApplication(string programmaticId, string productName)
    {
        var type = Type.GetTypeFromProgID(programmaticId, throwOnError: false);
        if (type is null)
        {
            throw new FileNotFoundException(
                $"Neither LibreOffice nor {productName} is installed. Install one locally to convert this file type.");
        }

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"{productName} could not be started for background conversion.");
    }

    private static Task RunStaAsync(Action action, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                action();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        })
        {
            IsBackground = true,
            Name = "EasyPDF Office conversion"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task.WaitAsync(cancellationToken);
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static void TryInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is COMException or InvalidComObjectException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
