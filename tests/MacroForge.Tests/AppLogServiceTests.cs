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
            log.Version("更新", "v1.0.0");

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

    [Fact]
    public void Trims_each_level_independently_and_clears()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MacroForge-log-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var log = new AppLogService(dir);
            for (var i = 0; i < AppLogService.MaxEntriesPerLevel + 25; i++)
            {
                log.Action("測試", $"action-{i}");
                log.Info("測試", $"info-{i}");
            }

            Assert.Equal(AppLogService.MaxEntriesPerLevel, log.CountByLevel(LogLevel.Action));
            Assert.Equal(AppLogService.MaxEntriesPerLevel, log.CountByLevel(LogLevel.Info));
            Assert.Equal(AppLogService.MaxEntriesPerLevel * 2, log.Count);
            Assert.Contains(log.Query(LogLevel.Action), e => e.Message == $"action-{AppLogService.MaxEntriesPerLevel + 24}");
            Assert.DoesNotContain(log.Query(LogLevel.Action), e => e.Message == "action-0");

            var removed = log.Clear(LogLevel.Action);
            Assert.Equal(AppLogService.MaxEntriesPerLevel, removed);
            Assert.Equal(0, log.CountByLevel(LogLevel.Action));
            Assert.Equal(AppLogService.MaxEntriesPerLevel, log.CountByLevel(LogLevel.Info));

            Assert.Equal(AppLogService.MaxEntriesPerLevel, log.Clear());
            Assert.Equal(0, log.Count);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
