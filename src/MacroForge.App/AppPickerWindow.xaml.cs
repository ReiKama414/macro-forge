using System.Windows;
using System.Windows.Input;
using MacroForge.Core.Model;
using MacroForge.Core.Scope;
using Microsoft.Win32;

namespace MacroForge.App;

public partial class AppPickerWindow : Window
{
    public string? SelectedExe { get; private set; }

    public AppPickerWindow()
    {
        InitializeComponent();
        AppList.ItemsSource = ForegroundProcess.ListRunningApps();
    }

    private void AppList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => AcceptSelected();

    private void UseForeground_Click(object sender, RoutedEventArgs e)
    {
        var app = ForegroundProcess.GetForegroundProcess();
        if (app is null)
            return;
        SelectedExe = app.ExeFileName;
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
            SelectedExe = string.IsNullOrWhiteSpace(app.ExecutablePath) ? app.ProcessName : app.ExeFileName;
            DialogResult = true;
        }
    }
}
