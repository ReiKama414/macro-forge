using System.Windows;
using System.Windows.Input;
using MacroForge.Core.Model;
using MacroForge.Core.Scope;
using Microsoft.Win32;

namespace MacroForge.App;

public partial class AppPickerWindow : Window
{
    private readonly CancellationTokenSource _captureCancellation = new();
    private bool _capturing;
    public string? SelectedExe { get; private set; }

    public AppPickerWindow()
    {
        InitializeComponent();
        Closed += (_, _) => _captureCancellation.Cancel();
        SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            WindowChromeHelper.ApplyRoundedCyberChrome(handle,
                (System.Windows.Media.Color)FindResource("AccentColor"));
        };
        AppList.ItemsSource = ForegroundProcess.ListRunningApps();
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AcceptSelected();

    private async void UseForeground_Click(object sender, RoutedEventArgs e)
    {
        if (_capturing) return;
        _capturing = true;
        ForegroundApp? app;
        var button = (System.Windows.Controls.Button)sender;
        try
        {
            app = await ForegroundProcess.CaptureAfterSwitchAsync(message => button.Content = message, _captureCancellation.Token);
        }
        catch (OperationCanceledException) { return; }
        finally { _capturing = false; }
        if (_captureCancellation.IsCancellationRequested) return;
        if (app is null)
            return;
        SelectedExe = string.IsNullOrWhiteSpace(app.ExecutablePath)
            ? app.ExeFileName
            : app.ExecutablePath;
        DialogResult = true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "應用程式 (*.exe)|*.exe|所有檔案 (*.*)|*.*",
            Title = "選擇應用程式"
        };
        if (dialog.ShowDialog(this) == true)
        {
            SelectedExe = dialog.FileName;
            DialogResult = true;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private void AcceptSelected()
    {
        if (AppList.SelectedItem is ForegroundApp app)
        {
            SelectedExe = string.IsNullOrWhiteSpace(app.ExecutablePath)
                ? app.ExeFileName
                : app.ExecutablePath;
            DialogResult = true;
        }
    }
}
