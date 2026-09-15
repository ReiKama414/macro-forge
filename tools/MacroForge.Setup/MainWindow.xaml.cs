using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace MacroForge.Setup;

public partial class MainWindow : Window
{
    public const string GitHubUrl = "https://github.com/ReiKama414/macro-forge";

    private int _step;
    private string _installDir = "";
    private string? _installedExe;

    public MainWindow()
    {
        InitializeComponent();
        _installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "MacroForge");
        PathBox.Text = _installDir;
        ShowStep(0);
    }

    private void OnGitHubNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Agree_Changed(object sender, RoutedEventArgs e) => UpdateButtons();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇安裝資料夾",
            InitialDirectory = _installDir
        };
        if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            _installDir = Path.Combine(dialog.FolderName, "MacroForge");
            PathBox.Text = _installDir;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_step <= 0) return;
        ShowStep(_step - 1);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_step == 4)
        {
            if (LaunchAfterBox.IsChecked == true && !string.IsNullOrWhiteSpace(_installedExe) && File.Exists(_installedExe))
                Process.Start(new ProcessStartInfo(_installedExe) { UseShellExecute = true });
            Close();
            return;
        }

        if (_step == 2)
        {
            ShowStep(3);
            await InstallAsync();
            return;
        }

        ShowStep(_step + 1);
    }

    private void ShowStep(int step)
    {
        _step = step;
        PageWelcome.Visibility = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        PageDisclaimer.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        PagePath.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        PageProgress.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        PageDone.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        MarkStep(Step1Label, step == 0, step > 0);
        MarkStep(Step2Label, step == 1, step > 1);
        MarkStep(Step3Label, step == 2, step > 2);
        MarkStep(Step4Label, step == 3, step > 3);
        MarkStep(Step5Label, step == 4, false);

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        BackButton.IsEnabled = _step is > 0 and < 3;
        BackButton.Visibility = _step is >= 3 and < 4 ? Visibility.Collapsed : Visibility.Visible;

        NextButton.Content = _step switch
        {
            2 => "開始安裝",
            3 => "安裝中…",
            4 => LaunchAfterBox.IsChecked == true ? "啟動並關閉" : "完成",
            _ => "下一步"
        };
        NextButton.IsEnabled = _step switch
        {
            1 => AgreeBox.IsChecked == true,
            3 => false,
            _ => true
        };

        FooterHint.Text = _step switch
        {
            1 => "必須同意聲明才能繼續",
            3 => "請稍候，正在解壓並寫入檔案",
            4 => "安裝完成 · reiKama414",
            _ => "作者 reiKama414 · MIT"
        };
    }

    private static void MarkStep(System.Windows.Controls.TextBlock label, bool current, bool done)
    {
        if (current)
        {
            label.Foreground = (Brush)Application.Current.Resources["Accent"];
            label.FontWeight = FontWeights.SemiBold;
        }
        else if (done)
        {
            label.Foreground = (Brush)Application.Current.Resources["Text"];
            label.FontWeight = FontWeights.Normal;
        }
        else
        {
            label.Foreground = (Brush)Application.Current.Resources["Muted"];
            label.FontWeight = FontWeights.Normal;
        }
    }

    private async Task InstallAsync()
    {
        try
        {
            SetProgress(5, "讀取安裝包…");
            await using var payload = OpenPayload();
            if (payload is null)
                throw new InvalidOperationException("找不到內嵌安裝包。請使用 scripts/publish-release.ps1 產出的 Setup。");

            SetProgress(15, "建立資料夾…");
            Directory.CreateDirectory(_installDir);

            SetProgress(30, "解壓縮檔案…");
            var tempZip = Path.Combine(Path.GetTempPath(), $"macroforge-payload-{Guid.NewGuid():N}.zip");
            await using (var file = File.Create(tempZip))
                await payload.CopyToAsync(file);

            SetProgress(55, "寫入程式檔…");
            if (Directory.Exists(_installDir))
            {
                foreach (var file in Directory.EnumerateFiles(_installDir, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { /* ignore */ }
                }
            }
            ZipFile.ExtractToDirectory(tempZip, _installDir, overwriteFiles: true);
            try { File.Delete(tempZip); } catch { /* ignore */ }

            _installedExe = Path.Combine(_installDir, "MacroForge.exe");
            if (!File.Exists(_installedExe))
                throw new InvalidOperationException("安裝後找不到 MacroForge.exe。");

            SetProgress(80, "建立捷徑…");
            if (DesktopShortcutBox.IsChecked == true)
                CreateShortcut(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "MacroForge.lnk"), _installedExe);
            if (StartMenuBox.IsChecked == true)
            {
                var startMenu = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                    "Programs");
                Directory.CreateDirectory(startMenu);
                CreateShortcut(Path.Combine(startMenu, "MacroForge.lnk"), _installedExe);
            }

            SetProgress(100, "完成");
            ShowStep(4);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "安裝失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            ShowStep(2);
        }
    }

    private static Stream? OpenPayload()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("app.zip", StringComparison.OrdinalIgnoreCase));
        return name is null ? null : asm.GetManifestResourceStream(name);
    }

    private void SetProgress(double value, string text)
    {
        ProgressBar.Value = value;
        ProgressText.Text = text;
    }

    private static void CreateShortcut(string shortcutPath, string targetPath)
    {
        // Windows Script Host COM — available on desktop Windows without extra packages.
        var shellType = Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null)
            return;
        dynamic shell = Activator.CreateInstance(shellType)!;
        var shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath);
        shortcut.Description = "MacroForge by reiKama414";
        shortcut.IconLocation = targetPath + ",0";
        shortcut.Save();
    }
}
