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
        SetFiles([]);
        Loaded += (_, _) => HandleStartupArguments();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _operationCancellation is null && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_operationCancellation is null && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            try { SetFiles(_files.Concat(ExpandPaths(paths)).ToArray()); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                StatusHeading.Text = "Could not add these files";
                StatusText.Text = exception.Message;
            }
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
            SetFiles(_files.Concat(dialog.FileNames).ToArray());
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => SetFiles([]);

    private async void Run_Click(object sender, RoutedEventArgs e) => await RunSelectedActionAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e) => _operationCancellation?.Cancel();

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_operationCancellation is not null)
        {
            e.Cancel = true;
            _operationCancellation.Cancel();
            StatusText.Text = "Cancelling safely. Close the window once the operation has stopped.";
        }
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (FilesList.SelectedIndex is var index && index >= 0)
        {
            SetFiles(_files.Where((_, position) => position != index).ToArray());
        }
    }

    private void MoveFile_Click(object sender, RoutedEventArgs e)
    {
        var index = FilesList.SelectedIndex;
        var destination = index + int.Parse((string)((Button)sender).Tag, System.Globalization.CultureInfo.InvariantCulture);
        if (index < 0 || destination < 0 || destination >= _files.Count) { return; }
        (_files[index], _files[destination]) = (_files[destination], _files[index]);
        RefreshFileList();
        FilesList.SelectedIndex = destination;
    }

    private void FilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MoveUpButton is null) { return; }
        var index = FilesList.SelectedIndex;
        MoveUpButton.IsEnabled = index > 0;
        MoveDownButton.IsEnabled = index >= 0 && index < _files.Count - 1;
        RemoveButton.IsEnabled = index >= 0;
    }

    private void ActionBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateActionControls();

    private void UpdateActionControls()
    {
        if (PagesPanel is null) { return; }
        var action = ActionBox.SelectedValue as string;
        PagesPanel.Visibility = action is "split" or "extract-pages" or "rotate" ? Visibility.Visible : Visibility.Collapsed;
        RotationPanel.Visibility = action == "rotate" ? Visibility.Visible : Visibility.Collapsed;
        ActionBox.IsEnabled = ActionBox.Items.Count > 0;
        RunButton.IsEnabled = action is not null && _operationCancellation is null;
        RunButton.Content = action is null ? "Create PDF" : LabelFor(action);
        ActionHint.Text = action switch
        {
            "convert-to-pdf" => "Create a PDF from your document. Office files need LibreOffice or Microsoft Office.",
            "merge" => "Combine all selected PDFs into one, in the order shown.",
            "split" or "extract-pages" => "Save the selected pages as a new PDF. Your original stays unchanged.",
            "rotate" => "Turn the selected pages and save a new copy.",
            "images-to-pdf" => "Create one PDF, with an image on each page in the order shown.",
            _ => "Add a document, PDFs, or images to see available operations."
        };
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutput is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = false,
                ArgumentList = { "/select,", _lastOutput }
            });
        }
        catch (System.ComponentModel.Win32Exception exception) { StatusText.Text = exception.Message; }
    }

    private void SetFiles(IEnumerable<string> paths)
    {
        var selectedAction = ActionBox.SelectedValue;
        var candidates = paths.Select(Path.GetFullPath).Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(101).ToArray();
        _files.Clear();
        _files.AddRange(candidates.Take(100));
        RefreshFileList();
        _lastOutput = null;
        OpenFolderButton.Visibility = Visibility.Collapsed;
        EmptyFilesHint.Visibility = _files.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FileCountText.Text = $"{_files.Count} {(_files.Count == 1 ? "file" : "files")}";
        ClearButton.IsEnabled = _files.Count > 0;

        var actions = SupportedFiles.GetValidActions(_files)
            .Select(id => new ActionItem(id, LabelFor(id)))
            .ToArray();
        ActionBox.ItemsSource = actions;
        ActionBox.SelectedIndex = actions.Length > 0 ? 0 : -1;
        if (actions.Any(item => item.Id == selectedAction as string)) { ActionBox.SelectedValue = selectedAction; }
        UpdateActionControls();
        StatusHeading.Text = _files.Count == 0 ? "Ready when you are" : actions.Length > 0 ? "Ready to create" : "Check your selection";
        StatusText.Text = actions.Length > 0
            ? $"{_files.Count} file(s) ready. Choose an operation on the right."
            : _files.Count == 0 ? "Add a document, a PDF, or a few images to begin." : "Use one document, one or more PDFs, or images together. Remove other file types to continue.";
        if (candidates.Length > 100) { StatusText.Text += " Only the first 100 files were added."; }
    }

    private void RefreshFileList()
    {
        FilesList.ItemsSource = _files.Select(path => new FileItem(Path.GetFileName(path), Path.GetDirectoryName(path)!, Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), path)).ToArray();
        FilesList_SelectionChanged(FilesList, null!);
    }

    private void HandleStartupArguments()
    {
        var arguments = App.StartupArguments;
        if (arguments.Length >= 3 && arguments[0] == "--action")
        {
            var action = arguments[1];
            SetFiles(arguments[2..]);
            if (action != "open") { ActionBox.SelectedValue = action; }
        }
    }

    private async Task RunSelectedActionAsync()
    {
        if (ActionBox.SelectedValue is not string action || _files.Count == 0)
        {
            MessageBox.Show(this, "Choose supported files and an operation first.", "EasyPDF", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_operationCancellation is not null) { return; }
        _operationCancellation = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        ChooseFilesButton.IsEnabled = OptionsPanel.IsEnabled = FilesList.IsEnabled = FileToolbar.IsEnabled = false;
        CancelButton.Visibility = OperationProgress.Visibility = Visibility.Visible;
        OpenFolderButton.Visibility = Visibility.Collapsed;
        StatusHeading.Text = "Working on your files…";
        StatusText.Text = "Processing locally. Your originals will stay unchanged.";

        try
        {
            var pdf = new PdfService();
            var token = _operationCancellation.Token;
            _lastOutput = action switch
            {
                "convert-to-pdf" => await new LocalDocumentConverter().ConvertToPdfAsync(
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

            StatusHeading.Text = "Your PDF is ready";
            StatusText.Text = $"Saved to {_lastOutput}";
            OpenFolderButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException)
        {
            StatusHeading.Text = "Operation cancelled";
            StatusText.Text = "Operation cancelled. Temporary files were cleaned up.";
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or NotSupportedException or TimeoutException or FormatException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            StatusHeading.Text = "Could not create the PDF";
            StatusText.Text = exception.Message;
        }
        finally
        {
            _operationCancellation.Dispose();
            _operationCancellation = null;
            ChooseFilesButton.IsEnabled = OptionsPanel.IsEnabled = FilesList.IsEnabled = FileToolbar.IsEnabled = true;
            CancelButton.Visibility = OperationProgress.Visibility = Visibility.Collapsed;
            UpdateActionControls();
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
                foreach (var file in Directory.EnumerateFiles(path).Where(SupportedFiles.IsSupported).Take(101))
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
    private sealed record FileItem(string Name, string Folder, string Extension, string FullPath);
}
