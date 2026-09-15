using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using MacroForge.Core;
using MacroForge.Core.Input;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace MacroForge.App;

public partial class MainWindow : Window
{
    public const string GitHubUrl = "https://github.com/ReiKama414/macro-forge";

    private readonly MainViewModel _vm;
    private Forms.NotifyIcon? _tray;
    private bool _forceClose;
    private bool _inTray;

    public MainWindow() : this(new UniversalMouseService(), true) { }

    public MainWindow(UniversalMouseService service, bool startRuntime)
    {
        InitializeComponent();
        _vm = new MainViewModel(service, persistTheme: startRuntime);
        DataContext = _vm;
        LayoutCanvas.ButtonClicked += id =>
        {
            var button = _vm.Buttons.FirstOrDefault(b => b.Id == id);
            if (button is not null)
                _vm.SelectedButton = button;
        };
        LayoutCanvas.ButtonMoved += _vm.MoveButton;
        if (startRuntime)
        {
            Loaded += OnLoaded;
            SourceInitialized += OnSourceInitialized;
            StateChanged += OnWindowStateChanged;
        }
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    public void RestoreFromTray()
    {
        ShowFromTray();
    }

    private void OnGitHubNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_forceClose && _inTray)
        {
            e.Cancel = true;
            return;
        }

        DisposeTray();
        // Cancel the foreground poller and every running macro so the process
        // actually exits instead of lingering with background work.
        _vm.Shutdown();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_vm.IsRecordingKey)
            return;
        if (e.IsRepeat)
        {
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            _vm.CancelRecord();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F12)
            return;
        if (KeyCapture.TryCapture(e, out var canonical, out _))
        {
            _vm.CompleteRecord(canonical);
            e.Handled = true;
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        if (ShouldMinimizeToTray(promptIfNeeded: true))
            HideToTray();
        else
            WindowState = WindowState.Minimized;
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
            return;
        // Taskbar minimize path (e.g. Win+Down) — only auto-tray after preference is known.
        if (_vm.MinimizeToTrayPromptSeen && _vm.MinimizeToTray)
            HideToTray();
    }

    private bool ShouldMinimizeToTray(bool promptIfNeeded)
    {
        if (_vm.MinimizeToTrayPromptSeen)
            return _vm.MinimizeToTray;

        if (!promptIfNeeded)
            return false;

        var dontAsk = false;
        var answer = CyberDialog.Confirm(
            this,
            "縮小到系統匣",
            "要將 MacroForge 縮小到右下角系統匣嗎？之後仍可在設定中變更。",
            yesLabel: "縮小到系統匣",
            noLabel: "縮小到工作列",
            checkboxLabel: "以後不詢問",
            onCheckbox: value => dontAsk = value,
            height: 300);

        if (answer is null)
            return false;

        _vm.RememberMinimizeToTrayChoice(answer.Value, dontAsk);
        return answer.Value;
    }

    private void EnsureTray()
    {
        if (_tray is not null)
            return;

        var icon = LoadTrayIcon();
        _tray = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "MacroForge",
            Visible = false
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("顯示 MacroForge", null, (_, _) => ShowFromTray());
        menu.Items.Add("結束", null, (_, _) =>
        {
            _forceClose = true;
            ShowFromTray();
            Close();
        });
        _tray.ContextMenuStrip = menu;
    }

    private static DrawingIcon LoadTrayIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico");
            var stream = Application.GetResourceStream(uri)?.Stream;
            if (stream is not null)
                return new DrawingIcon(stream);
        }
        catch
        {
            // fall through
        }

        return System.Drawing.SystemIcons.Application;
    }

    private void HideToTray()
    {
        EnsureTray();
        _inTray = true;
        if (_tray is not null)
            _tray.Visible = true;
        ShowInTaskbar = false;
        Hide();
        WindowState = WindowState.Normal;
    }

    private void ShowFromTray()
    {
        _inTray = false;
        ShowInTaskbar = true;
        Show();
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
        if (_tray is not null)
            _tray.Visible = false;
    }

    private void DisposeTray()
    {
        if (_tray is null)
            return;
        _tray.Visible = false;
        _tray.Dispose();
        _tray = null;
    }

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e)
    {
        _forceClose = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.Start();
        LayoutCanvas.Rebuild();
        HighlightPageNav();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var source = PresentationSource.FromVisual(this) as HwndSource;
        if (source is null)
            return;
        WindowChromeHelper.ApplyRoundedCyberChrome(source.Handle, (System.Windows.Media.Color)FindResource("AccentColor"));
        RawInputRegistration.Register(source.Handle);
        source.AddHook(Hook);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_vm.Service.HandleWindowMessage(msg, wParam, lParam))
            handled = false;
        return IntPtr.Zero;
    }
}
