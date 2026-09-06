using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BBGameLauncher;

public partial class MainWindow : Window
{
    private readonly List<LauncherMenuItem> _rootItems =
    [
        new("Game Library", "Browse and launch your installed games."),
        new("Applications", "Open tools and utilities."),
        new("System Settings", "Configure the launcher and your library."),
        new("Help", "View controls and setup information."),
    ];

    private readonly List<LauncherMenuItem> _settingsItems =
    [
        new("Display", "Choose appearance, resolution, and full-screen behavior."),
        new("Library", "Set the folder used to discover your games."),
        new("Input", "Configure controllers and keyboard shortcuts."),
        new("Updates", "Check the installed launcher version."),
        new("About", "View BB Game Launcher details."),
    ];

    private List<LauncherMenuItem> _activeItems = [];
    private int _selectedIndex;
    private bool _inSettings;
    private bool _isFullscreen;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ShowRootMenu();
    }

    private void ShowRootMenu()
    {
        _inSettings = false;
        _activeItems = _rootItems;
        _selectedIndex = 0;
        HintText.Text = "MAIN MENU";
        DetailPanel.Child = null;
        SetDetailVisibility(false);
        RenderMenu();
    }

    private void ShowSettingsMenu()
    {
        _inSettings = true;
        _activeItems = _settingsItems;
        _selectedIndex = 0;
        HintText.Text = "SYSTEM SETTINGS";
        DetailPanel.Child = null;
        SetDetailVisibility(false);
        RenderMenu();
    }

    private void RenderMenu()
    {
        MenuPanel.Children.Clear();
        for (var i = 0; i < _activeItems.Count; i++)
        {
            var index = i;
            var item = _activeItems[i];
            var button = new Button
            {
                Content = item.Title,
                Tag = i,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(0, 5, 0, 5),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = i == _selectedIndex ? new SolidColorBrush(Color.FromRgb(230, 228, 69)) : new SolidColorBrush(Color.FromRgb(76, 194, 247)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = i == _selectedIndex ? Color.FromRgb(236, 232, 83) : Color.FromRgb(62, 178, 247),
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.55
                }
            };
            button.Click += (_, _) => { _selectedIndex = index; ActivateSelected(); };
            button.MouseEnter += (_, _) => { _selectedIndex = index; RenderMenu(); };
            MenuPanel.Children.Add(button);
        }

        var selected = _activeItems[_selectedIndex];
        if (DetailPanel.Child is null)
            HintText.Text = _inSettings ? "SYSTEM SETTINGS" : "MAIN MENU";
        ToolTip = selected.Description;
    }

    private void ActivateSelected()
    {
        var item = _activeItems[_selectedIndex];
        if (!_inSettings && item.Title == "System Settings")
        {
            ShowSettingsMenu();
            return;
        }

        ShowDetail(item);
    }

    private void ShowDetail(LauncherMenuItem item)
    {
        var label = new TextBlock
        {
            Text = item.Title.ToUpperInvariant(),
            FontSize = 28,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(231, 229, 69)),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(238, 233, 74), BlurRadius = 16, ShadowDepth = 0, Opacity = .6
            }
        };
        var description = new TextBlock
        {
            Text = item.Description,
            Margin = new Thickness(0, 16, 0, 0),
            Width = 320,
            FontSize = 15,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(177, 220, 244)),
            LineHeight = 23
        };
        var callout = new Border
        {
            Margin = new Thickness(0, 24, 0, 0),
            Padding = new Thickness(14, 10, 14, 10),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 100, 210, 255)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(38, 7, 40, 75)),
            Child = new TextBlock
            {
                Text = item.Title is "Game Library" or "Applications" ? "No items have been configured yet." : "Use this screen as the starting point for this section.",
                Foreground = new SolidColorBrush(Color.FromRgb(151, 205, 240)), FontSize = 13, TextWrapping = TextWrapping.Wrap
            }
        };

        var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(label);
        stack.Children.Add(description);
        stack.Children.Add(callout);
        DetailPanel.Child = stack;
        HintText.Text = item.Title.ToUpperInvariant();
        SetDetailVisibility(true);
    }

    private void SetDetailVisibility(bool show)
    {
        var fade = new DoubleAnimation(show ? 1 : 0, TimeSpan.FromMilliseconds(show ? 240 : 120));
        DetailPanel.BeginAnimation(OpacityProperty, fade);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                _selectedIndex = (_selectedIndex - 1 + _activeItems.Count) % _activeItems.Count;
                RenderMenu();
                break;
            case Key.Down:
                _selectedIndex = (_selectedIndex + 1) % _activeItems.Count;
                RenderMenu();
                break;
            case Key.Enter:
                ActivateSelected();
                break;
            case Key.Escape:
                if (_inSettings) ShowRootMenu(); else if (_isFullscreen) ToggleFullscreen();
                break;
            case Key.F11:
                ToggleFullscreen();
                break;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && !_isFullscreen) DragMove();
    }

    private void FullscreenButton_Click(object sender, RoutedEventArgs e) => ToggleFullscreen();
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void ToggleFullscreen()
    {
        _isFullscreen = !_isFullscreen;
        WindowState = _isFullscreen ? WindowState.Maximized : WindowState.Normal;
        ResizeMode = _isFullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
    }

    private sealed record LauncherMenuItem(string Title, string Description);
}
