using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MacroForge.Core.Macros;
using MacroForge.Core.Model;
using Microsoft.Win32;

namespace MacroForge.App;

public partial class MainWindow
{
    private void OnNavigate(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string tag }) return;
        if (tag == "count") { _vm.TriggerMode = "固定次數"; return; }

        var (title, description) = tag switch
        {
            "home" => ("歡迎使用 MacroForge", $"{_vm.ConnectedSummary}\n\n選取滑鼠與按鍵，在右側設定功能後儲存，再開啟底部巨集總開關。\n\nF12 可立即停止全部巨集。"),
            "device" => ("裝置資訊", $"{_vm.DeviceTitle}\n{_vm.DeviceSubtitle}\n{_vm.CapabilityText}\n\n{_vm.IdentityText}\n\n{_vm.Limitation}"),
            "test" => ("按鍵測試與偵測", "選擇偵測方式後，依序按下滑鼠上的實體按鍵。已辨識的按鍵會出現在中央模型。"),
            "profiles" => ("設定檔", $"{_vm.ProfileText}\n\n設定檔會依前景應用程式自動切換。可為目前前景程式新增設定檔。"),
            "transfer" => ("匯入 / 匯出", "備份或載入目前滑鼠的巨集與設定檔。匯入前會自動備份，並停止正在執行的巨集。"),
            "settings" => ("外觀設定", "選擇整個介面的主題色。面板、選取、焦點、燈光與視窗邊框會同步更新。"),
            _ => ("使用說明", "01  在「我的滑鼠」選裝置。\n\n02  點中央滑鼠按鍵，右側選擇鍵盤／滑鼠／巨集。\n\n03  新增動作並錄製按鍵。\n\n04  設定觸發與作用範圍後儲存。\n\n05  開啟巨集總開關。\n\nF12：全部停止　Esc：取消錄製")
        };

        CyberDialog.Show(this, title, description, (actions, dialog) =>
        {
            if (tag == "settings")
            {
                // Put theme picker in the body area above actions via a host panel.
                // actions is a WrapPanel — insert a full-width stack before buttons.
                if (actions.Parent is Panel body)
                {
                    var block = new StackPanel { Margin = new Thickness(0, 12, 0, 4) };
                    block.Children.Add(new TextBlock
                    {
                        Text = "主題色",
                        Foreground = (System.Windows.Media.Brush)FindResource("Muted"),
                        FontSize = 12,
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                    var colors = new ComboBox
                    {
                        ItemsSource = _vm.AccentOptions,
                        MinHeight = 40,
                        Width = 200,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    colors.SetBinding(ComboBox.SelectedItemProperty,
                        new Binding(nameof(MainViewModel.SelectedAccent)) { Mode = BindingMode.TwoWay });
                    block.Children.Add(colors);
                    var index = body.Children.IndexOf(actions);
                    body.Children.Insert(Math.Max(0, index), block);
                }
            }

            if (tag == "test")
            {
                actions.Children.Add(CyberDialog.ActionButton(this, "掃描全部按鍵", () =>
                {
                    dialog.Close();
                    _vm.Page = "mouse";
                    _vm.ScanAllCommand.Execute(null);
                }));
                actions.Children.Add(CyberDialog.ActionButton(this, "偵測單一按鍵", () =>
                {
                    dialog.Close();
                    _vm.Page = "mouse";
                    _vm.LearnOneCommand.Execute(null);
                }));
            }

            if (tag == "profiles")
            {
                actions.Children.Add(CyberDialog.ActionButton(this, "為目前程式建立設定檔", () =>
                {
                    _vm.AddWorkProfileCommand.Execute(null);
                    dialog.Close();
                }));
            }

            if (tag == "transfer")
            {
                actions.Children.Add(CyberDialog.ActionButton(this, "匯出目前設定", () => ExportBindings(dialog)));
                actions.Children.Add(CyberDialog.ActionButton(this, "匯入設定檔", () => ImportBindings(dialog)));
            }
        }, height: tag is "help" or "device" ? 480 : 360);

        _vm.Page = _vm.Page;
    }

    private static readonly JsonSerializerOptions TransferJson = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private void ExportBindings(Window owner)
    {
        if (_vm.Selected is null) return;
        var picker = new SaveFileDialog { FileName = "MacroForge-bindings.json", Filter = "MacroForge 設定 (*.json)|*.json" };
        if (picker.ShowDialog(owner) != true) return;
        try
        {
            File.WriteAllText(picker.FileName, JsonSerializer.Serialize(_vm.Selected.Snapshot.Bindings, TransferJson));
            _vm.Status = "已匯出巨集設定";
            owner.Close();
        }
        catch (Exception error)
        {
            CyberDialog.Show(this, "無法匯出", error.Message, height: 260);
        }
    }

    private void ImportBindings(Window owner)
    {
        if (_vm.Selected is null) return;
        var picker = new OpenFileDialog { Filter = "MacroForge 設定 (*.json)|*.json" };
        if (picker.ShowDialog(owner) != true) return;
        try
        {
            var model = JsonSerializer.Deserialize<BindingsModel>(File.ReadAllText(picker.FileName), TransferJson)
                ?? throw new InvalidDataException("設定檔是空的。");
            if (model.Bindings is null || model.Macros is null || model.Profiles is null ||
                model.Bindings.Any(b => b is null || b.Action is null || b.Scope is null || string.IsNullOrWhiteSpace(b.Id)) ||
                model.Bindings.Select(b => b.Id).Distinct().Count() != model.Bindings.Count)
                throw new InvalidDataException("設定檔結構不完整或巨集識別碼重複。");
            bool InvalidMacro(MacroDefinition macro) => macro.Actions is null || macro.Actions.Any(a =>
                a is null || a.Keys is null || a.Type is not ("key" or "mouse" or "delay"));
            if (model.Bindings.Any(b => b.Scope.Applications is null || b.Action.Keys is null || (b.Macro is not null && InvalidMacro(b.Macro))) ||
                model.Macros.Any(m => m is null || InvalidMacro(m)) ||
                model.Profiles.Any(p => p is null || p.Scope is null || p.Scope.Applications is null))
                throw new InvalidDataException("設定檔含有不完整或不支援的巨集動作。");
            if (model.Bindings.Any(b => !_vm.Buttons.Any(button => BindingResolver.ButtonMatches(b.TargetButton, button))))
                throw new InvalidDataException("設定檔含有目前滑鼠不存在的按鍵，請先完成按鍵偵測。");
            var backup = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MacroForge",
                $"bindings-backup-{DateTime.Now:yyyyMMdd-HHmmssfff}.json");
            File.WriteAllText(backup, JsonSerializer.Serialize(_vm.Selected.Snapshot.Bindings, TransferJson));
            _vm.Armed = false;
            _vm.Service.Macros.Scheduler.StopAll();
            _vm.Selected.Snapshot.Bindings = model;
            _vm.Service.SaveSelected();
            _vm.Reload();
            _vm.Status = "已匯入設定；原設定已備份，巨集總開關保持關閉";
            owner.Close();
        }
        catch (Exception error)
        {
            CyberDialog.Show(this, "無法匯入", error.Message, height: 260);
        }
    }
}
