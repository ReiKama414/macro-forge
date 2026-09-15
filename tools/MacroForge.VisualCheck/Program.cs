using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MacroForge.App;
using MacroForge.Core;
using MacroForge.Core.Devices;
using MacroForge.Core.Model;
using MacroForge.Core.Storage;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var output = Path.GetFullPath("artifacts/visual-check");
        Directory.CreateDirectory(output);
        // Load the same resources without App.StartupUri creating a second, live window.
        var xaml = System.Xml.Linq.XDocument.Load("src/MacroForge.App/App.xaml");
        var resources = xaml.Root!.Elements().Single();
        var dictionary = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" + string.Join("", resources.Elements()) + "</ResourceDictionary>";
        var app = new Application { Resources = (ResourceDictionary)System.Windows.Markup.XamlReader.Parse(dictionary) };
        var service = new UniversalMouseService(new DeviceStore(Path.Combine(output, "fixture-store")));
        var window = new MainWindow(service, false);
        app.MainWindow = window;
        var vm = (MainViewModel)window.DataContext;
        // Explicitly synthetic fixtures; no physical input, macros, or user settings are changed.
        foreach (var (name, count) in new[] { ("USB GAMING MOUSE", 8), ("Wireless Mouse", 5), ("Generic HID Mouse", 5) })
        {
            service.CreateDeviceModel(new PhysicalMouseDevice
            {
                DisplayName = name, ProductName = name,
                Fingerprint = DeviceFingerprint.Create(0x1532, 0x0084, name, null, null, 1, 2),
                Capabilities = new DeviceCapabilities { ButtonCount = count, HasWheel = true }
            });
        }
        vm.Reload();
        vm.Selected = vm.Mice[0];
        vm.SelectedButton = vm.Buttons.First(b => b.RawButtonIndex == 4);
        vm.BindingName = "自動技能 1";
        vm.MacroSteps.Clear();
        foreach (var value in new[] { "D1", "D2", "D3" })
        {
            vm.MacroSteps.Add(new MacroStep { Kind = "按鍵盤", Value = value });
            vm.MacroSteps.Add(new MacroStep { Kind = "等待", Value = "500" });
        }
        vm.ScopeGlobal = false;
        vm.Applications.Add("game.exe");
        vm.Status = "視覺測試資料 · 未啟動巨集";
        vm.SaveBindingCommand.Execute(null);
        Assert(vm.Page == "mouse", "Saving must keep the mouse editor visible");
        Assert(vm.Selected.Snapshot.Bindings.Bindings.Count == 1, "Binding saved to isolated fixture store");
        vm.IntervalText = "1 s";
        vm.IntervalUnit = "毫秒 (ms)";
        Assert(vm.IntervalValue == "1000", "Unit switching must preserve duration");
        vm.IntervalUnit = "秒 (s)";
        vm.ChosenMacro = vm.AvailableMacros.First();

        // Bindings remain live while theme resources are replaced, including selected radio tiles.
        foreach (var (name, file, width, height) in new[]
        {
            ("綠", "green", 1536, 1024), ("紫", "purple", 1536, 1024), ("青", "cyan", 1536, 1024),
            ("藍", "blue-small", 1280, 800), ("橘", "orange", 1536, 1024)
        })
        {
            vm.SelectedAccent = name;
            Render(window, output, file, width, height);
            var radios = Descendants((DependencyObject)window.Content).OfType<RadioButton>().Where(r => r.IsChecked == true).ToList();
            Assert(radios.Count >= 4, "Navigation, function, trigger and scope selections are present");
            Assert(!vm.ScopeGlobal, "Rendering must preserve application scope");
            Assert(radios.All(r => ((SolidColorBrush)r.Foreground).Color == ThemePalette.AccentFor(name)), "Selected tiles use the current theme");
            var drawings = Descendants((DependencyObject)window.Content).OfType<MouseArtwork>().ToList();
            Assert(drawings.Count > 0 && drawings.All(a => ((SolidColorBrush)a.AccentBrush).Color == ThemePalette.AccentFor(name)), "All mouse artwork uses the current theme");
        }
        vm.Page = "auto";
        Render(window, output, "automation", 1536, 1024);
        vm.Page = "mouse";
        vm.IsSideView = true;
        Render(window, output, "side-view", 1536, 1024);
        Assert(Descendants((DependencyObject)window.Content).OfType<MouseArtwork>().Any(a => a.SideView), "Side view changes the mouse artwork");
        vm.IsFrontView = true;
        var nameBox = Descendants((DependencyObject)window.Content).OfType<TextBox>().Single(box => Equals(box.ToolTip, "編輯巨集名稱"));
        Assert(nameBox.ActualHeight >= 30 && nameBox.ActualWidth >= 180, "Macro name editor has usable bounds");
        // An empty draft must never run the previously saved macro through the Test command.
        vm.MacroSteps.Clear();
        vm.TestSelectedCommand.Execute(null);
        Assert(service.Macros.Scheduler.Snapshots.Count == 0, "Invalid test draft does not start the previous macro");
        vm.FunctionType = "滑鼠"; vm.MouseAction = "右鍵"; vm.SaveBindingCommand.Execute(null);
        Assert(vm.Selected.Snapshot.Bindings.Bindings.Single().Action.MouseButton == "right", "Mouse action is saved");
        vm.FunctionType = "停用"; vm.SaveBindingCommand.Execute(null);
        Assert(!vm.Selected.Snapshot.Bindings.Bindings.Single().Enabled, "Disable turns off the binding");
        vm.FunctionType = "預設"; vm.SaveBindingCommand.Execute(null);
        Assert(vm.Selected.Snapshot.Bindings.Bindings.Count == 0, "Default restores native behavior");
        vm.Selected = null;
        Assert(!vm.HasSelectedButton && vm.Buttons.Count == 0, "Clearing the device also clears the button inspector");
        Render(window, output, "empty-selection", 1280, 800);
        vm.Shutdown();
        Console.WriteLine("Visual and interaction checks passed. " + output);
    }

    private static void Render(Window window, string output, string name, int width, int height)
    {
        var root = (FrameworkElement)window.Content;
        root.Width = width; root.Height = height;
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png")); png.Save(stream);
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + message);
    }
}
