using System.Diagnostics;
using System.Text;
using EasyPDF.Core;

namespace EasyPDF.Converters;

public sealed class LibreOfficeDocumentConverter(
    string? executablePath = null,
    TimeSpan? timeout = null,
    InputLimits? limits = null) : IDocumentConverter
{
    private readonly FileValidator _validator = new(limits);
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMinutes(2);

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
        var soffice = executablePath is null ? LibreOfficeLocator.Locate() : Path.GetFullPath(executablePath);
        if (!File.Exists(soffice) || !string.Equals(Path.GetFileName(soffice), "soffice.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("The configured LibreOffice executable is invalid.", soffice);
        }

        return await AtomicFile.WriteAsync(outputPath, async (temporaryOutput, token) =>
        {
            var workRoot = Path.Combine(Path.GetTempPath(), "EasyPDF", Guid.NewGuid().ToString("N"));
            var outputDirectory = Path.Combine(workRoot, "output");
            var profileDirectory = Path.Combine(workRoot, "profile");
            Directory.CreateDirectory(outputDirectory);
            Directory.CreateDirectory(profileDirectory);

            try
            {
                var profileUri = new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri.TrimEnd('/');
                var startInfo = new ProcessStartInfo
                {
                    FileName = soffice,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = outputDirectory
                };
                startInfo.ArgumentList.Add("--headless");
                startInfo.ArgumentList.Add("--nologo");
                startInfo.ArgumentList.Add("--nodefault");
                startInfo.ArgumentList.Add("--nofirststartwizard");
                startInfo.ArgumentList.Add("--nolockcheck");
                startInfo.ArgumentList.Add("--invisible");
                startInfo.ArgumentList.Add($"-env:UserInstallation={profileUri}");
                startInfo.ArgumentList.Add("--convert-to");
                startInfo.ArgumentList.Add("pdf");
                startInfo.ArgumentList.Add("--outdir");
                startInfo.ArgumentList.Add(outputDirectory);
                startInfo.ArgumentList.Add(input);

                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                {
                    throw new InvalidOperationException("LibreOffice could not be started.");
                }

                var stdoutTask = ReadLimitedAsync(process.StandardOutput, token);
                var stderrTask = ReadLimitedAsync(process.StandardError, token);
                using var timeoutSource = new CancellationTokenSource(_timeout);
                using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token);
                try
                {
                    await process.WaitForExitAsync(linkedSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    KillProcess(process);
                    throw new TimeoutException($"Document conversion exceeded the {_timeout.TotalSeconds:0}-second limit.");
                }
                catch
                {
                    KillProcess(process);
                    throw;
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"LibreOffice conversion failed with exit code {process.ExitCode}. {SanitizeDiagnostic(stderr.Length > 0 ? stderr : stdout)}");
                }

                var produced = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(input) + ".pdf");
                if (!File.Exists(produced) || new FileInfo(produced).Length < 5)
                {
                    throw new InvalidDataException("LibreOffice did not produce a valid PDF output.");
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

    private static async Task<string> ReadLimitedAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[2048];
        var builder = new StringBuilder();
        while (builder.Length < 8192)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, 8192 - builder.Length)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
    }

    private static string SanitizeDiagnostic(string value) =>
        string.Join(' ', value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)).Trim();

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
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
