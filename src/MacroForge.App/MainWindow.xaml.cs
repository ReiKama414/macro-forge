using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using MacroForge.Core;
using MacroForge.Core.Input;

namespace MacroForge.App;

public partial class MainWindow : Window
{
    public const string GitHubUrl = "https://github.com/ReiKama414/macro-forge";

    private readonly MainViewModel _vm;

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
        }
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    private void OnGitHubNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
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

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm.Start();
        LayoutCanvas.Rebuild();
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
