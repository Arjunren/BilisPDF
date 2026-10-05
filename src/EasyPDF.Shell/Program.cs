using System.Diagnostics;

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

var executableDirectory = AppContext.BaseDirectory;
var desktopExecutable = Path.Combine(executableDirectory, "EasyPDF.Desktop.exe");
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
foreach (var path in args[1..].Take(100))
{
    startInfo.ArgumentList.Add(Path.GetFullPath(path));
}

Process.Start(startInfo);
return 0;
