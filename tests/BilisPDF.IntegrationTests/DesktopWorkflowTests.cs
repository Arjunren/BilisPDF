using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using BilisPDF.Desktop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BilisPDF.IntegrationTests;

[TestClass]
public sealed class DesktopWorkflowTests
{
    [TestMethod]
    public void Selection_UpdatesOperations_ReordersFiles_AndClearsStaleResults()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App();
                app.InitializeComponent();
                var window = new MainWindow();
                T Control<T>(string name) => (T)window.FindName(name);
                void Select(params string[] paths) => typeof(MainWindow).GetMethod("SetFiles", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [paths]);
                Assert.IsFalse(Control<Button>("RunButton").IsEnabled);
                Assert.IsFalse(Control<Button>("RemoveButton").IsEnabled);
                var files = new[] { Path.Combine(Path.GetTempPath(), $"BilisPDF-{Guid.NewGuid():N}.pdf"), Path.Combine(Path.GetTempPath(), $"BilisPDF-{Guid.NewGuid():N}.pdf") };
                // Only the selection is tested here; PDF content is covered by PdfServiceTests.
                try
                {
                    foreach (var path in files) { File.WriteAllText(path, "%PDF-test"); }
                    Select(files);
                    var actions = Control<ComboBox>("ActionBox");
                    Assert.AreEqual("merge", actions.SelectedValue);
                    Assert.AreEqual(Visibility.Collapsed, Control<StackPanel>("PagesPanel").Visibility);
                    var list = Control<ListBox>("FilesList");
                    list.SelectedIndex = 1;
                    Control<Button>("MoveUpButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var first = list.Items[0];
                    Assert.AreEqual(files[1], first.GetType().GetProperty("FullPath")!.GetValue(first));
                    Control<Button>("RemoveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.AreEqual(1, list.Items.Count);
                    actions.SelectedValue = "rotate";
                    Assert.AreEqual(Visibility.Visible, Control<StackPanel>("RotationPanel").Visibility);
                    Control<TextBlock>("StatusText").Text = "Old result";
                    Control<Button>("OpenFolderButton").Visibility = Visibility.Visible;
                    Select([]);
                    Assert.IsFalse(Control<Button>("RunButton").IsEnabled);
                    Assert.AreEqual(Visibility.Collapsed, Control<Button>("OpenFolderButton").Visibility);
                    Assert.AreEqual(Visibility.Visible, Control<StackPanel>("EmptyFilesHint").Visibility);
                }
                finally
                {
                    foreach (var path in files) { File.Delete(path); }
                    window.Close();
                    app.Shutdown();
                }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "Desktop checks timed out.");
        if (failure is not null) { throw new AssertFailedException("Desktop workflow failed.", failure); }
    }
}
