using System.Windows;

namespace EasyPDF.Desktop;

public partial class App : Application
{
    public static string[] StartupArguments { get; } = Environment.GetCommandLineArgs()[1..];
}
