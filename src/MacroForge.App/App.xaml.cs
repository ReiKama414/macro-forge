using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace MacroForge.App;

public partial class App : Application
{
    private const string MutexName = "Local\\MacroForge.reiKama414.SingleInstance";
    private const string ActivateEventName = "Local\\MacroForge.reiKama414.Activate";

    private Mutex? _mutex;
    private EventWaitHandle? _activateEvent;
    private CancellationTokenSource? _activateWatch;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            WriteCrash("domain", args.ExceptionObject as Exception);

        _mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            try
            {
                using var activate = EventWaitHandle.OpenExisting(ActivateEventName);
                activate.Set();
            }
            catch
            {
                // Existing instance may be shutting down.
            }

            Shutdown();
            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _activateWatch = new CancellationTokenSource();
        var token = _activateWatch.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_activateEvent.WaitOne(500))
                        Current?.Dispatcher.BeginInvoke(BringMainWindowToFront, DispatcherPriority.Normal);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }, token);

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("dispatcher", e.Exception);
        e.Handled = true;
    }

    private static void WriteCrash(string source, Exception? error)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MacroForge");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "ui-crash.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {error}{Environment.NewLine}");
        }
        catch
        {
            // Crash logging is best-effort.
        }
    }

    private static void BringMainWindowToFront()
    {
        if (Current.MainWindow is not Window window)
            return;
        if (window is MainWindow main)
        {
            main.RestoreFromTray();
            return;
        }
        if (!window.IsVisible)
            window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activateWatch?.Cancel();
        _activateEvent?.Dispose();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch { /* ignore */ }
            _mutex.Dispose();
        }
        base.OnExit(e);
    }
}
