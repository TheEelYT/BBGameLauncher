using System.Windows;
using System.Windows.Media;

namespace BBGameLauncher.Controls;

/// <summary>Animated starfield, haze, and glass-like cubes drawn without external artwork.</summary>
public sealed class SpaceScene : FrameworkElement
{
    private readonly List<Star> _stars = [];
    private readonly List<Cube> _cubes = [];
    private readonly Random _random = new(4821);
    private TimeSpan _lastFrame;
    private double _elapsed;

    public SpaceScene()
    {
        Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(RenderSize);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(1, 4, 12), Color.FromRgb(2, 8, 19), 90), null, bounds);

        DrawNebula(dc, new Point(RenderSize.Width * .2, RenderSize.Height * .46), RenderSize.Width * .48, Color.FromArgb(113, 67, 69, 195));
        DrawNebula(dc, new Point(RenderSize.Width * .79, RenderSize.Height * .23), RenderSize.Width * .36, Color.FromArgb(48, 22, 71, 133));
        DrawNebula(dc, new Point(RenderSize.Width * .68, RenderSize.Height * .72), RenderSize.Width * .31, Color.FromArgb(32, 3, 72, 132));

        EnsureItems();
        foreach (var star in _stars)
        {
            var shimmer = .35 + (Math.Sin(_elapsed * star.Twinkle + star.Phase) + 1) * .17;
            var brush = new SolidColorBrush(Color.FromArgb((byte)(255 * shimmer), 140, 204, 255));
            dc.DrawEllipse(brush, null, new Point(star.X * RenderSize.Width, star.Y * RenderSize.Height), star.Size, star.Size);
        }

        foreach (var cube in _cubes)
            DrawCube(dc, cube);

        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(42, 101, 168, 240)), 1), new Rect(.5, .5, Math.Max(0, RenderSize.Width - 1), Math.Max(0, RenderSize.Height - 1)));
    }

    private void DrawNebula(DrawingContext dc, Point center, double radius, Color color)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(color, 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(color.A * .45), color.R, color.G, color.B), .3));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        dc.DrawEllipse(brush, null, center, radius, radius * .44);
    }

    private void EnsureItems()
    {
        if (_stars.Count == 0)
            for (var i = 0; i < 180; i++)
                _stars.Add(new Star(_random.NextDouble(), _random.NextDouble(), .35 + _random.NextDouble() * .7, 1 + _random.NextDouble() * 3, _random.NextDouble() * Math.PI * 2));

        if (_cubes.Count == 0)
        {
            var values = new[] { (.60, .20, 44d), (.76, .32, 34d), (.84, .66, 58d), (.64, .72, 33d), (.91, .49, 28d), (.72, .87, 54d), (.47, .17, 32d) };
            for (var i = 0; i < values.Length; i++)
                _cubes.Add(new Cube(values[i].Item1, values[i].Item2, values[i].Item3, i * .7, .24 + i * .035));
        }
    }

    private void DrawCube(DrawingContext dc, Cube cube)
    {
        var bob = Math.Sin(_elapsed * cube.Speed + cube.Phase) * 18;
        var angle = _elapsed * (.28 + cube.Speed * .12) + cube.Phase;
        var center = new Point(cube.X * RenderSize.Width + Math.Cos(_elapsed * cube.Speed + cube.Phase) * 18,
            cube.Y * RenderSize.Height + bob);
        var size = cube.Size * (1 + Math.Sin(_elapsed * .6 + cube.Phase) * .08);
        var a = MakePoint(center, size, angle, -1, -1);
        var b = MakePoint(center, size, angle, 1, -1);
        var c = MakePoint(center, size, angle, 1, 1);
        var d = MakePoint(center, size, angle, -1, 1);
        var depth = new Vector(Math.Cos(angle + .8) * size * .46, Math.Sin(angle + .8) * size * .46);
        var a2 = a + depth; var b2 = b + depth; var c2 = c + depth; var d2 = d + depth;

        var front = Polygon(a, b, c, d);
        var top = Polygon(a, b, b2, a2);
        var side = Polygon(b, c, c2, b2);
        var outline = new Pen(new SolidColorBrush(Color.FromArgb(155, 102, 208, 255)), 1.1);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(46, 76, 173, 255)), outline, front);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(72, 133, 223, 255)), outline, top);
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(37, 31, 116, 214)), outline, side);
        dc.DrawLine(outline, c, d);
        dc.DrawLine(outline, d, a);
    }

    private static Point MakePoint(Point c, double size, double angle, int x, int y)
    {
        var px = x * size * .5; var py = y * size * .5;
        return new Point(c.X + px * Math.Cos(angle) - py * Math.Sin(angle), c.Y + px * Math.Sin(angle) + py * Math.Cos(angle));
    }

    private static StreamGeometry Polygon(params Point[] points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(points[0], true, true);
        context.PolyLineTo(points.Skip(1).ToArray(), true, true);
        geometry.Freeze();
        return geometry;
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        if (e is RenderingEventArgs args)
        {
            if (_lastFrame != default)
                _elapsed += Math.Min(.05, (args.RenderingTime - _lastFrame).TotalSeconds);
            _lastFrame = args.RenderingTime;
        }
        InvalidateVisual();
    }

    private sealed record Star(double X, double Y, double Size, double Twinkle, double Phase);
    private sealed record Cube(double X, double Y, double Size, double Phase, double Speed);
}
