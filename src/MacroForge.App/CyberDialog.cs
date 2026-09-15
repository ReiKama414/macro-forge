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
            Content = "✕",
            Style = (Style)owner.FindResource("ChromeButton"),
            Margin = new Thickness(0, 0, 8, 0),
            ToolTip = "關閉"
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
            Content = "關閉",
            MinWidth = 96,
            Padding = new Thickness(20, 8, 20, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            Style = (Style)owner.FindResource("PrimaryButton")
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
