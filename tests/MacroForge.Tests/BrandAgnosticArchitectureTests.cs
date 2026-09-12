using MacroForge.Core.Learning;
using MacroForge.Core.Model;

namespace MacroForge.Tests;

public class BrandAgnosticArchitectureTests
{
    private static readonly string[] ForbiddenTokens =
    {
        "G502", "G HUB", "DeathAdder", "iCUE", "Synapse", "Armoury"
    };

    [Fact]
    public void Core_has_no_brand_special_cases()
    {
        var coreDir = FindCore();
        var files = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var token in ForbiddenTokens)
                Assert.DoesNotContain(token, text);
            Assert.DoesNotContain("if (brand", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("switch (vendor", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Button_tables_are_not_vendor_specific()
    {
        var five = ButtonLearningService.SeedFromCapabilities(new DeviceCapabilities { ButtonCount = 5 });
        var twelve = ButtonLearningService.SeedFromCapabilities(new DeviceCapabilities { ButtonCount = 12 });
        Assert.Equal(5, five.Count);
        Assert.Equal(12, twelve.Count);
        Assert.All(twelve, b => Assert.Equal(InputSource.Mouse, b.Source));
    }

    private static string FindCore()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "MacroForge.Core");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("MacroForge.Core");
    }
}
