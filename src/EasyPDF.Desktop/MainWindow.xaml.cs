using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using EasyPDF.Converters;
using EasyPDF.Core;
using EasyPDF.Pdf;
using Microsoft.Win32;

namespace EasyPDF.Desktop;

public partial class MainWindow : Window
{
    private readonly List<string> _files = [];
    private CancellationTokenSource? _operationCancellation;
    private string? _lastOutput;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await HandleStartupArgumentsAsync();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            SetFiles(ExpandPaths(paths));
        }
    }

    private void ChooseFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            CheckFileExists = true,
            Filter = "Supported files|*.pdf;*.docx;*.xlsx;*.pptx;*.txt;*.rtf;*.html;*.htm;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.webp|All files|*.*"
        };
        if (dialog.ShowDialog(this) == true)
        {
            SetFiles(dialog.FileNames);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => SetFiles([]);

    private async void Run_Click(object sender, RoutedEventArgs e) => await RunSelectedActionAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => _operationCancellation?.Cancel();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutput is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = false,
            ArgumentList = { "/select,", _lastOutput }
        });
    }

    private void SetFiles(IEnumerable<string> paths)
    {
        _files.Clear();
        _files.AddRange(paths.Select(Path.GetFullPath).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(100));
        FilesList.ItemsSource = null;
        FilesList.ItemsSource = _files.Select(path => Path.GetFileName(path)).ToArray();

        var actions = SupportedFiles.GetValidActions(_files)
            .Select(id => new ActionItem(id, LabelFor(id)))
            .ToArray();
        ActionBox.ItemsSource = actions;
        ActionBox.SelectedIndex = actions.Length > 0 ? 0 : -1;
        StatusText.Text = actions.Length > 0
            ? $"{_files.Count} file(s) selected."
            : _files.Count == 0 ? "Ready. Operation history is disabled by default." : "This combination of files has no supported action.";
    }

    private async Task HandleStartupArgumentsAsync()
    {
        var arguments = App.StartupArguments;
        if (arguments.Length >= 3 && arguments[0] == "--action")
        {
            var action = arguments[1];
            SetFiles(arguments[2..]);
            ActionBox.SelectedValue = action;
            if (ActionBox.SelectedItem is not null && action is not "open")
            {
                await RunSelectedActionAsync();
            }
        }
    }

    private async Task RunSelectedActionAsync()
    {
        if (ActionBox.SelectedValue is not string action || _files.Count == 0)
        {
            MessageBox.Show(this, "Choose supported files and an operation first.", "EasyPDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _operationCancellation?.Dispose();
        _operationCancellation = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        OpenFolderButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Working…";

        try
        {
            var pdf = new PdfService();
            var token = _operationCancellation.Token;
            _lastOutput = action switch
            {
                "convert-to-pdf" => await new LibreOfficeDocumentConverter().ConvertToPdfAsync(
                    _files[0], OutputPathResolver.BesideSource(_files[0], string.Empty), token),
                "merge" => await pdf.MergeAsync(
                    _files,
                    OutputPathResolver.FindAvailable(Path.Combine(Path.GetDirectoryName(_files[0])!, "Merged.pdf")), token),
                "split" => await pdf.ExtractPagesAsync(
                    _files[0], PagesBox.Text, OutputPathResolver.BesideSource(_files[0], "-split"), token),
                "extract-pages" => await pdf.ExtractPagesAsync(
                    _files[0], PagesBox.Text, OutputPathResolver.BesideSource(_files[0], "-extracted"), token),
                "rotate" => await pdf.RotateAsync(
                    _files[0], SelectedDegrees(), PagesBox.Text, OutputPathResolver.BesideSource(_files[0], "-rotated"), token),
                "images-to-pdf" => await pdf.ImagesToPdfAsync(
                    _files,
                    OutputPathResolver.FindAvailable(Path.Combine(Path.GetDirectoryName(_files[0])!, "Images.pdf")), token),
                _ => throw new NotSupportedException("The selected operation is not supported.")
            };

            StatusText.Text = $"Completed successfully: {Path.GetFileName(_lastOutput)}";
            OpenFolderButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Operation cancelled. Temporary files were cleaned up.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or NotSupportedException or TimeoutException)
        {
            StatusText.Text = "The operation could not be completed.";
            MessageBox.Show(this, exception.Message, "EasyPDF", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    private int SelectedDegrees() =>
        int.Parse(((ComboBoxItem)DegreesBox.SelectedItem).Tag!.ToString()!, System.Globalization.CultureInfo.InvariantCulture);

    private static IEnumerable<string> ExpandPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                yield return path;
            }
            else if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
            {
                foreach (var file in Directory.EnumerateFiles(path).Where(SupportedFiles.IsSupported).Take(100))
                {
                    yield return file;
                }
            }
        }
    }

    private static string LabelFor(string id) => id switch
    {
        "convert-to-pdf" => "Convert to PDF",
        "merge" => "Merge PDFs",
        "split" => "Split / select pages",
        "extract-pages" => "Extract pages",
        "rotate" => "Rotate pages",
        "images-to-pdf" => "Combine images into PDF",
        _ => id
    };

    private sealed record ActionItem(string Id, string Label);
}
