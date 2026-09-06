using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace BBGameLauncher.Controls;

/// <summary>A small original startup flourish that gives the menu the same calm, space-like arrival.</summary>
public sealed class StartupOverlay : Grid
{
    public StartupOverlay()
    {
        Background = new SolidColorBrush(Color.FromRgb(0, 0, 2));
        IsHitTestVisible = true;
        Loaded += (_, _) => BeginSequence();
    }

    private void BeginSequence()
    {
        var center = new Grid { Width = 240, Height = 240, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        Children.Add(center);

        for (var i = 0; i < 3; i++)
        {
            var ring = new Ellipse
            {
                Width = 86 + i * 48,
                Height = 86 + i * 48,
                Stroke = new SolidColorBrush(Color.FromArgb((byte)(130 - i * 27), 90, 209, 255)),
                StrokeThickness = 1.2,
                Opacity = 0,
                RenderTransformOrigin = new Point(.5, .5),
                RenderTransform = new ScaleTransform(.35, .35)
            };
            center.Children.Add(ring);
            var rise = new DoubleAnimation(.35, 1.15, TimeSpan.FromMilliseconds(950)) { BeginTime = TimeSpan.FromMilliseconds(120 + i * 170), EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            ring.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, rise);
            ring.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, rise.Clone());
            ring.BeginAnimation(OpacityProperty, new DoubleAnimation(0, .85, TimeSpan.FromMilliseconds(540)) { BeginTime = TimeSpan.FromMilliseconds(100 + i * 150), AutoReverse = true });
        }

        var mark = new Border { Width = 46, Height = 46, BorderBrush = new SolidColorBrush(Color.FromRgb(117, 210, 255)), BorderThickness = new Thickness(1), Opacity = 0 };
        mark.Child = new Rectangle { Margin = new Thickness(12), Fill = new SolidColorBrush(Color.FromRgb(66, 150, 249)), RenderTransform = new RotateTransform(45) };
        center.Children.Add(mark);
        mark.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(500)) { BeginTime = TimeSpan.FromMilliseconds(620) });

        var name = new TextBlock { Text = "B B   G A M E   L A U N C H E R", FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.SemiBold, FontSize = 15, Foreground = new SolidColorBrush(Color.FromRgb(191, 227, 248)), Opacity = 0, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 188, 0, 0) };
        center.Children.Add(name);
        name.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(650)) { BeginTime = TimeSpan.FromMilliseconds(830) });

        BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(800)) { BeginTime = TimeSpan.FromMilliseconds(2450), FillBehavior = FillBehavior.Stop, Completed = (_, _) => Visibility = Visibility.Collapsed });
    }
}
