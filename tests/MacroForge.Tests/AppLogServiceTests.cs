using MacroForge.Core.Logging;

namespace MacroForge.Tests;

public class AppLogServiceTests
{
    [Fact]
    public void Records_multiple_levels_and_filters()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MacroForge-log-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var log = new AppLogService(dir);
            log.Info("系統", "啟動");
            log.Action("按鍵", "映射");
            log.Warning("應用程式", "切換");
            log.Error("輸入", "SendInput 失敗");
            log.Version("更新", "v0.1.0");

            Assert.Equal(5, log.Count);
            Assert.Equal(1, log.CountByLevel(LogLevel.Error));
            Assert.Single(log.Query(LogLevel.Action));
            Assert.Contains(log.Query(search: "SendInput"), e => e.Level == LogLevel.Error);
            Assert.Contains("錯誤", log.ExportText(log.Query(LogLevel.Error)));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
