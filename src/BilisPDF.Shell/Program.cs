using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using BilisPDF.Converters;
using BilisPDF.Core;

if (args.Length < 2)
{
    return 2;
}

var allowedActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "convert-to-pdf", "merge", "split", "extract-pages", "rotate", "images-to-pdf", "open"
};
var action = args[0];
if (!allowedActions.Contains(action))
{
    return 2;
}

var paths = args[1..]
    .Take(100)
    .Select(Path.GetFullPath)
    .Where(File.Exists)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (paths.Length == 0)
{
    return 2;
}

if (action.Equals("convert-to-pdf", StringComparison.OrdinalIgnoreCase))
{
    return await ConvertDirectlyAsync(paths);
}

return LaunchDesktop(action, paths);

static async Task<int> ConvertDirectlyAsync(IReadOnlyList<string> paths)
{
    try
    {
        var converter = new LocalDocumentConverter();
        var outputs = new List<string>(paths.Count);
        foreach (var path in paths)
        {
            var kind = SupportedFiles.GetKind(path);
            if (kind is not (EasyFileKind.OfficeDocument or EasyFileKind.TextDocument))
            {
                throw new NotSupportedException($"{Path.GetFileName(path)} is not a supported document format.");
            }

            var output = OutputPathResolver.BesideSource(path, string.Empty);
            outputs.Add(await converter.ConvertToPdfAsync(path, output));
        }

        var message = outputs.Count == 1
            ? $"Created {Path.GetFileName(outputs[0])}"
            : $"Created {outputs.Count} PDF files";
        ShowNotification("BilisPDF conversion complete", message, ToolTipIcon.Info);
        return 0;
    }
    catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or NotSupportedException or TimeoutException or System.Runtime.InteropServices.COMException)
    {
        MessageBox.Show(
            exception.Message,
            "BilisPDF could not convert the file",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
        return 3;
    }
}

static int LaunchDesktop(string action, IReadOnlyList<string> paths)
{
    var executableDirectory = AppContext.BaseDirectory;
    var desktopExecutable = Path.Combine(executableDirectory, "BilisPDF.Desktop.exe");
    if (!File.Exists(desktopExecutable))
    {
        return 3;
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = desktopExecutable,
        UseShellExecute = false,
        WorkingDirectory = executableDirectory
    };
    startInfo.ArgumentList.Add("--action");
    startInfo.ArgumentList.Add(action);
    foreach (var path in paths)
    {
        startInfo.ArgumentList.Add(path);
    }

    Process.Start(startInfo);
    return 0;
}

static void ShowNotification(string title, string message, ToolTipIcon icon)
{
    var iconPath = Path.Combine(AppContext.BaseDirectory, "BilisPDF.ico");
    using var appIcon = File.Exists(iconPath) ? new Icon(iconPath) : (Icon)SystemIcons.Application.Clone();
    using var notification = new NotifyIcon
    {
        Icon = appIcon,
        Text = "BilisPDF",
        Visible = true
    };
    notification.ShowBalloonTip(3500, title, message, icon);
    var until = DateTime.UtcNow.AddSeconds(4);
    while (DateTime.UtcNow < until)
    {
        Application.DoEvents();
        Thread.Sleep(50);
    }
}
