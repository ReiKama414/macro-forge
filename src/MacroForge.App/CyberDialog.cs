using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace MacroForge.App;

/// <summary>Borderless cyberpunk dialog used instead of native Windows chrome popups.</summary>
internal static class CyberDialog
{
    public static Window Create(Window owner, string title, double width = 520, double height = 440)
    {
        var dialog = new Window
        {
            Owner = owner,
            Title = title,
            Width = width,
            Height = height,
            MinWidth = Math.Min(400, width),
            MinHeight = Math.Min(280, height),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            Background = (Brush)owner.FindResource("Bg"),
            AllowsTransparency = false
        };

        WindowChrome.SetWindowChrome(dialog, new WindowChrome
        {
            CaptionHeight = 44,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false
        });

        return dialog;
    }

    public static void Show(
        Window owner,
        string title,
        string description,
        Action<Panel, Window>? buildBody = null,
        double width = 520,
        double height = 440)
    {
        var dialog = Create(owner, title, width, height);
        dialog.DataContext = owner.DataContext;

        var root = new Border
        {
            Background = (Brush)owner.FindResource("Bg"),
            BorderBrush = (Brush)owner.FindResource("Accent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        };

        var layout = new DockPanel();

        // Header — title once only (no duplicate in body)
        var header = new Border
        {
            Height = 48,
            Background = (Brush)owner.FindResource("ChromeSurface"),
            BorderBrush = (Brush)owner.FindResource("Stroke"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(9, 9, 0, 0)
        };
        var headerDock = new DockPanel { LastChildFill = true };
        var close = new Button
        {
            Style = (Style)owner.FindResource("ChromeButton"),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "關閉",
            Content = new TextBlock
            {
                Text = "✕",
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            }
        };
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(close, Dock.Right);
        headerDock.Children.Add(close);
        headerDock.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(20, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)owner.FindResource("Text")
        });
        header.Child = headerDock;
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        // Footer — credit left, compact close right, clear gap
        var footer = new Border
        {
            Background = (Brush)owner.FindResource("ChromeSurface"),
            BorderBrush = (Brush)owner.FindResource("Stroke"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(22, 16, 22, 16),
            CornerRadius = new CornerRadius(0, 0, 9, 9)
        };
        var footerGrid = new Grid();
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var credit = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 12,
            Margin = new Thickness(0, 0, 16, 0)
        };
        var link = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("reiKama414"))
        {
            NavigateUri = new Uri(MainWindow.GitHubUrl),
            Foreground = (Brush)owner.FindResource("Accent"),
            TextDecorations = null,
            Cursor = Cursors.Hand,
            ToolTip = MainWindow.GitHubUrl
        };
        link.RequestNavigate += (_, e) =>
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        };
        credit.Inlines.Add(link);
        credit.Inlines.Add(new System.Windows.Documents.Run(" · MIT")
        {
            Foreground = (Brush)owner.FindResource("Muted")
        });
        Grid.SetColumn(credit, 0);
        footerGrid.Children.Add(credit);

        var done = new Button
        {
            MinWidth = 96,
            Padding = new Thickness(20, 0, 20, 0),
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Right,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Style = (Style)owner.FindResource("PrimaryButton"),
            Content = new TextBlock
            {
                Text = "關閉",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            }
        };
        done.Click += (_, _) => dialog.Close();
        Grid.SetColumn(done, 1);
        footerGrid.Children.Add(done);
        footer.Child = footerGrid;
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        var body = new StackPanel { Margin = new Thickness(28, 22, 28, 22) };
        body.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 23,
            Foreground = (Brush)owner.FindResource("Muted"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        var actions = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
        buildBody?.Invoke(actions, dialog);
        if (actions.Children.Count > 0)
            body.Children.Add(actions);

        layout.Children.Add(new ScrollViewer
        {
            Content = body,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0)
        });

        root.Child = layout;
        dialog.Content = root;
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                dialog.Close();
                e.Handled = true;
            }
        };

        dialog.SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(dialog).Handle;
            WindowChromeHelper.ApplyRoundedCyberChrome(handle,
                (Color)owner.FindResource("AccentColor"));
        };

        dialog.ShowDialog();
    }

    public static bool? Confirm(
        Window owner,
        string title,
        string description,
        string yesLabel = "是",
        string noLabel = "否",
        string? checkboxLabel = null,
        Action<bool>? onCheckbox = null,
        double width = 480,
        double height = 320)
    {
        bool? result = null;
        var dialog = Create(owner, title, width, height);
        dialog.DataContext = owner.DataContext;

        var root = new Border
        {
            Background = (Brush)owner.FindResource("Bg"),
            BorderBrush = (Brush)owner.FindResource("Accent"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        };

        var layout = new DockPanel();
        var header = new Border
        {
            Height = 48,
            Background = (Brush)owner.FindResource("ChromeSurface"),
            BorderBrush = (Brush)owner.FindResource("Stroke"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(9, 9, 0, 0)
        };
        var headerDock = new DockPanel { LastChildFill = true };
        var close = new Button
        {
            Style = (Style)owner.FindResource("ChromeButton"),
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "關閉",
            Content = new TextBlock
            {
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                Text = "\uE8BB",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(close, Dock.Right);
        headerDock.Children.Add(close);
        headerDock.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(20, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)owner.FindResource("Text")
        });
        header.Child = headerDock;
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);

        var footer = new Border
        {
            Background = (Brush)owner.FindResource("ChromeSurface"),
            BorderBrush = (Brush)owner.FindResource("Stroke"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(22, 16, 22, 16),
            CornerRadius = new CornerRadius(0, 0, 9, 9)
        };
        var footerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        void Finish(bool? value)
        {
            result = value;
            dialog.Close();
        }

        var no = new Button
        {
            MinWidth = 96,
            Height = 36,
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(20, 0, 20, 0),
            Style = (Style)owner.FindResource("GhostButton"),
            Content = noLabel
        };
        no.Click += (_, _) => Finish(false);
        var yes = new Button
        {
            MinWidth = 96,
            Height = 36,
            Padding = new Thickness(20, 0, 20, 0),
            Style = (Style)owner.FindResource("PrimaryButton"),
            Content = yesLabel
        };
        yes.Click += (_, _) => Finish(true);
        footerPanel.Children.Add(no);
        footerPanel.Children.Add(yes);
        footer.Child = footerPanel;
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        var body = new StackPanel { Margin = new Thickness(28, 22, 28, 22) };
        body.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 23,
            Foreground = (Brush)owner.FindResource("Muted"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        if (!string.IsNullOrWhiteSpace(checkboxLabel))
        {
            var check = new CheckBox
            {
                Content = checkboxLabel,
                Foreground = (Brush)owner.FindResource("Text"),
                Margin = new Thickness(0, 4, 0, 0)
            };
            check.Checked += (_, _) => onCheckbox?.Invoke(true);
            check.Unchecked += (_, _) => onCheckbox?.Invoke(false);
            body.Children.Add(check);
        }

        layout.Children.Add(body);
        root.Child = layout;
        dialog.Content = root;
        dialog.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Finish(null);
                e.Handled = true;
            }
        };
        dialog.SourceInitialized += (_, _) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(dialog).Handle;
            WindowChromeHelper.ApplyRoundedCyberChrome(handle,
                (Color)owner.FindResource("AccentColor"));
        };
        dialog.ShowDialog();
        return result;
    }

    public static Button ActionButton(Window owner, string label, Action onClick)
    {
        var button = new Button
        {
            Content = label,
            Margin = new Thickness(0, 0, 10, 10),
            MinWidth = 120,
            Padding = new Thickness(16, 9, 16, 9),
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = (Style)owner.FindResource("GhostButton")
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
