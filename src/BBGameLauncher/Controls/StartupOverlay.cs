using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BBGameLauncher.Controls;

/// <summary>Original loading sequence: converging light trails, a dotted orbit, then a blue-white bloom.</summary>
public sealed class StartupOverlay : Grid
{
    private readonly StartupVisual _visual = new();
    private readonly TextBlock _title = new()
    {
        Text = "B B   G A M E   L A U N C H E R",
        FontFamily = new FontFamily("Segoe UI"),
        FontWeight = FontWeights.SemiBold,
        FontSize = 15,
        Foreground = new SolidColorBrush(Color.FromRgb(202, 235, 255)),
        Opacity = 0,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 42, 0, 0),
        Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(108, 211, 255), BlurRadius = 16, ShadowDepth = 0, Opacity = .85
        }
    };

    public StartupOverlay()
    {
        Background = Brushes.Black;
        IsHitTestVisible = true;
        Children.Add(_visual);
        Children.Add(_title);
        Loaded += (_, _) => BeginSequence();
    }

    private void BeginSequence()
    {
        _visual.Begin();
        _title.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(650))
        {
            BeginTime = TimeSpan.FromMilliseconds(2900), FillBehavior = FillBehavior.HoldEnd
        });

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(950))
        {
            BeginTime = TimeSpan.FromMilliseconds(5500), FillBehavior = FillBehavior.Stop
        };
        fadeOut.Completed += (_, _) => Visibility = Visibility.Collapsed;
        BeginAnimation(OpacityProperty, fadeOut);
    }
}

internal sealed class StartupVisual : FrameworkElement
{
    private TimeSpan _lastFrame;
    private double _elapsed;
    private bool _running;

    public StartupVisual()
    {
        Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
    }

    public void Begin()
    {
        _elapsed = 0;
        _running = true;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
        if (!_running || RenderSize.Width <= 0 || RenderSize.Height <= 0)
            return;

        var center = new Point(RenderSize.Width * .5, RenderSize.Height * .5);
        var colours = new[]
        {
            Color.FromRgb(109, 238, 255), Color.FromRgb(159, 214, 255), Color.FromRgb(138, 168, 255),
            Color.FromRgb(222, 169, 255), Color.FromRgb(149, 245, 205)
        };
        for (var i = 0; i < colours.Length; i++)
            DrawTrail(dc, center, i, colours[i]);

        var orbitOpacity = Math.Clamp((_elapsed - 2.45) / .75, 0, 1)
            * (1 - Math.Clamp((_elapsed - 3.35) / .7, 0, 1));
        if (orbitOpacity > 0)
            DrawOrbit(dc, center, orbitOpacity);

        var bloom = Math.Clamp((_elapsed - 4.65) / .75, 0, 1);
        if (bloom > 0)
        {
            var brush = new RadialGradientBrush();
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(bloom * 230), 191, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(bloom * 105), 88, 207, 255), .3));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 88, 207, 255), 1));
            dc.DrawEllipse(brush, null, center, RenderSize.Width * .68, RenderSize.Height * .68);
        }
    }

    private void DrawTrail(DrawingContext dc, Point center, int trail, Color colour)
    {
        var head = Math.Clamp((_elapsed - trail * .14) / 2.6, 0, 1);
        if (head <= 0) return;

        var start = Math.Max(0, head - .24);
        var points = new List<Point>();
        for (var i = 0; i <= 32; i++)
            points.Add(TrailPoint(center, trail, start + (head - start) * i / 32));

        var trailOpacity = Math.Min(1, head * 4) * (1 - Math.Clamp((_elapsed - 3.05) / .72, 0, 1));
        for (var segment = 1; segment < points.Count; segment++)
        {
            var progressAlongTail = (double)segment / (points.Count - 1);
            var alpha = (byte)(trailOpacity * 78 * progressAlongTail * progressAlongTail);
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(alpha, colour.R, colour.G, colour.B)), 1.1),
                points[segment - 1], points[segment]);
        }

        for (var bead = 0; bead < 8; bead++)
        {
            var t = head - bead * .032;
            if (t < 0) break;
            var p = TrailPoint(center, trail, t);
            var alpha = (byte)(trailOpacity * (210 - bead * 21));
            var radius = bead == 0 ? 4.8 : 2.4;
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, colour.R, colour.G, colour.B)), null, p, radius, radius);
        }
    }

    private Point TrailPoint(Point center, int trail, double t)
    {
        var maxRadius = Math.Max(RenderSize.Width, RenderSize.Height) * .57;
        var radius = 18 + maxRadius * Math.Pow(1 - t, 1.12);
        var angle = 1.5 + trail * 1.25 + t * (4.8 + trail * .08);
        return new Point(center.X + Math.Cos(angle) * radius * 1.1, center.Y + Math.Sin(angle) * radius * .56);
    }

    private static void DrawOrbit(DrawingContext dc, Point center, double opacity)
    {
        const int dots = 22;
        for (var i = 0; i < dots; i++)
        {
            var angle = Math.PI * 2 * i / dots - Math.PI * .3;
            var p = new Point(center.X + Math.Cos(angle) * 71, center.Y + Math.Sin(angle) * 71);
            var alpha = (byte)(opacity * (125 + (i % 4) * 25));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(alpha, 153, 219, 255)), null, p, 1.9, 1.9);
        }
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs args)
        {
            if (_lastFrame != default && _running)
                _elapsed += Math.Min(.05, (args.RenderingTime - _lastFrame).TotalSeconds);
            _lastFrame = args.RenderingTime;
        }
        if (_running) InvalidateVisual();
    }
}
