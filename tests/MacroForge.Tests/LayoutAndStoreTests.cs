using MacroForge.Core.Layout;
using MacroForge.Core.Learning;
using MacroForge.Core.Model;
using MacroForge.Core.Storage;

namespace MacroForge.Tests;

public class LayoutAndStoreTests
{
    [Fact]
    public void Default_layout_creates_one_node_per_button()
    {
        var buttons = ButtonLearningService.SeedFromCapabilities(new DeviceCapabilities { ButtonCount = 12 });
        var layout = LayoutFactory.CreateDefault(buttons);
        Assert.Equal(12, layout.Buttons.Count);
        Assert.Contains(layout.Buttons, n => n.ButtonId == "btn_1" && n.X < 0.4);
        Assert.Contains(layout.Buttons, n => n.ButtonId == "btn_2" && n.X > 0.6);
    }

    [Fact]
    public void Store_keeps_devices_in_separate_folders()
    {
        var root = Path.Combine(Path.GetTempPath(), "macroforge-tests", Guid.NewGuid().ToString("N"));
        var store = new DeviceStore(root);
        var a = store.Create(new DeviceModel { DeviceId = "mouse_aaaa_bbbb_11111", ProductName = "Mouse A", Buttons = { } }, new LayoutModel());
        var b = store.Create(new DeviceModel { DeviceId = "mouse_cccc_dddd_22222", ProductName = "Mouse B", Buttons = { } }, new LayoutModel());
        store.Save(new DeviceSnapshot
        {
            Device = a.Device,
            Layout = new LayoutModel { Buttons = { new LayoutNode { ButtonId = "btn_1", X = 0.2, Y = 0.3 } } },
            Bindings = new BindingsModel()
        });

        Assert.True(Directory.Exists(Path.Combine(root, "mouse_aaaa_bbbb_11111")));
        Assert.True(File.Exists(Path.Combine(root, "mouse_aaaa_bbbb_11111", "device.json")));
        Assert.True(File.Exists(Path.Combine(root, "mouse_aaaa_bbbb_11111", "layout.json")));
        Assert.True(File.Exists(Path.Combine(root, "mouse_aaaa_bbbb_11111", "bindings.json")));
        Assert.True(Directory.Exists(Path.Combine(root, "mouse_cccc_dddd_22222")));
        var loaded = store.Load("mouse_aaaa_bbbb_11111");
        Assert.Equal(0.2, loaded.Layout.Buttons[0].X);
        Assert.NotEqual(loaded.Device.DeviceId, b.Device.DeviceId);
    }
}
