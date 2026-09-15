using System.Diagnostics;
using System.Text;
using MacroForge.Core.Model;
using MacroForge.Core.Native;

namespace MacroForge.Core.Scope;

public static class ForegroundProcess
{
    public static bool IsOwnApplication(ForegroundApp? app) => app is not null &&
        (app.ProcessId == (uint)Environment.ProcessId ||
         string.Equals(app.ExeFileName, "MacroForge.exe", StringComparison.OrdinalIgnoreCase));

    public static async Task<ForegroundApp?> CaptureAfterSwitchAsync(Action<string> report, CancellationToken ct)
    {
        for (var seconds = 3; seconds > 0; seconds--)
        {
            report($"{seconds} 秒後擷取程式，請切換到目標視窗…");
            await Task.Delay(1000, ct);
        }
        report("等待切換到目標視窗…");
        for (var attempt = 0; attempt < 120; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var app = GetForegroundProcess();
            if (app is not null && !IsOwnApplication(app)) return app;
            await Task.Delay(250, ct);
        }
        report("未切換到其他程式，已取消擷取。");
        return null;
    }

    public static ForegroundApp? GetForegroundProcess()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
            return null;
        return FromPid(pid);
    }

    public static ForegroundApp? FromPid(uint pid)
    {
        var handle = NativeMethods.OpenProcess(Win32.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle == IntPtr.Zero)
        {
            try
            {
                using var process = Process.GetProcessById((int)pid);
                return new ForegroundApp
                {
                    ProcessId = pid,
                    ProcessName = process.ProcessName + ".exe",
                    ExecutablePath = SafePath(process)
                };
            }
            catch
            {
                return null;
            }
        }

        try
        {
            var size = 1024;
            var sb = new StringBuilder(size);
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, sb, ref size))
                return null;
            var path = sb.ToString();
            return new ForegroundApp
            {
                ProcessId = pid,
                ProcessName = Path.GetFileName(path),
                ExecutablePath = path
            };
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    public static IReadOnlyList<ForegroundApp> ListRunningApps()
    {
        var result = new List<ForegroundApp>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.Id == Environment.ProcessId || process.MainWindowHandle == IntPtr.Zero)
                    continue;
                var path = SafePath(process);
                var name = string.IsNullOrWhiteSpace(path) ? process.ProcessName + ".exe" : Path.GetFileName(path);
                if (!seen.Add(name))
                    continue;
                result.Add(new ForegroundApp
                {
                    ProcessId = (uint)process.Id,
                    ProcessName = name,
                    ExecutablePath = path
                });
            }
            catch
            {
                // access denied on some system processes
            }
            finally
            {
                process.Dispose();
            }
        }

        return result.OrderBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string SafePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? "";
        }
        catch
        {
            return "";
        }
    }
}
