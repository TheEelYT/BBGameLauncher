using System.Windows;
using System.Windows.Media;

namespace BBGameLauncher.Controls;

/// <summary>Procedural starfield and one glass cube for every currently visible menu item.</summary>
public sealed class SpaceScene : FrameworkElement
{
    private readonly List<Star> _stars = [];
    private readonly List<Cube> _cubes = [];
    private readonly Random _random = new(4821);
    private TimeSpan _lastFrame;
    private double _elapsed;
    private int _selectedCube;

    public SpaceScene()
    {
        Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
        ClipToBounds = true;
        SetMenuCubes(4, 0);
    }

    /// <summary>Synchronizes the background cubes with the menu currently on screen.</summary>
    public void SetMenuCubes(int menuItemCount, int selectedIndex)
    {
        menuItemCount = Math.Max(0, menuItemCount);
        _selectedCube = Math.Clamp(selectedIndex, 0, Math.Max(0, menuItemCount - 1));
        if (_cubes.Count == menuItemCount)
            return;

        _cubes.Clear();
        var placements = new[]
        {
            (.66, .19, 46d), (.85, .36, 36d), (.78, .61, 44d), (.61, .76, 32d),
            (.90, .72, 54d), (.50, .22, 28d), (.72, .88, 31d)
        };
        for (var i = 0; i < menuItemCount; i++)
        {
            var p = placements[i % placements.Length];
            _cubes.Add(new Cube(p.Item1, p.Item2, p.Item3, i * .73, .24 + (i % 4) * .05));
        }
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var bounds = new Rect(RenderSize);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(1, 4, 12), Color.FromRgb(2, 8, 19), 90), null, bounds);
        DrawNebula(dc, new Point(RenderSize.Width * .2, RenderSize.Height * .46), RenderSize.Width * .48, Color.FromArgb(113, 67, 69, 195));
        DrawNebula(dc, new Point(RenderSize.Width * .79, RenderSize.Height * .23), RenderSize.Width * .36, Color.FromArgb(48, 22, 71, 133));
        DrawNebula(dc, new Point(RenderSize.Width * .68, RenderSize.Height * .72), RenderSize.Width * .31, Color.FromArgb(32, 3, 72, 132));

        EnsureStars();
        foreach (var star in _stars)
        {
            var shimmer = .35 + (Math.Sin(_elapsed * star.Twinkle + star.Phase) + 1) * .17;
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * shimmer), 140, 204, 255)), null,
                new Point(star.X * RenderSize.Width, star.Y * RenderSize.Height), star.Size, star.Size);
        }

        for (var i = 0; i < _cubes.Count; i++)
            DrawCube(dc, _cubes[i], i == _selectedCube);

        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(42, 101, 168, 240)), 1),
            new Rect(.5, .5, Math.Max(0, RenderSize.Width - 1), Math.Max(0, RenderSize.Height - 1)));
    }

    private void DrawNebula(DrawingContext dc, Point center, double radius, Color color)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(color, 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(color.A * .45), color.R, color.G, color.B), .3));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        dc.DrawEllipse(brush, null, center, radius, radius * .44);
    }

    private void EnsureStars()
    {
        if (_stars.Count != 0) return;
        for (var i = 0; i < 180; i++)
            _stars.Add(new Star(_random.NextDouble(), _random.NextDouble(), .35 + _random.NextDouble() * .7,
                1 + _random.NextDouble() * 3, _random.NextDouble() * Math.PI * 2));
    }

    private void DrawCube(DrawingContext dc, Cube cube, bool highlighted)
    {
        var bob = Math.Sin(_elapsed * cube.Speed + cube.Phase) * 18;
        var angle = _elapsed * (.28 + cube.Speed * .12) + cube.Phase;
        var center = new Point(cube.X * RenderSize.Width + Math.Cos(_elapsed * cube.Speed + cube.Phase) * 18,
            cube.Y * RenderSize.Height + bob);
        var size = cube.Size * (1 + Math.Sin(_elapsed * .6 + cube.Phase) * .08) * (highlighted ? 1.13 : 1);
        var a = MakePoint(center, size, angle, -1, -1);
        var b = MakePoint(center, size, angle, 1, -1);
        var c = MakePoint(center, size, angle, 1, 1);
        var d = MakePoint(center, size, angle, -1, 1);
        var depth = new Vector(Math.Cos(angle + .8) * size * .46, Math.Sin(angle + .8) * size * .46);
        var a2 = a + depth; var b2 = b + depth; var c2 = c + depth;

        if (highlighted)
        {
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(115, 46, 178, 255), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 46, 178, 255), 1));
            dc.DrawEllipse(glow, null, center, size * 1.75, size * 1.75);
        }

        var front = Polygon(a, b, c, d);
        var top = Polygon(a, b, b2, a2);
        var side = Polygon(b, c, c2, b2);
        var outline = new Pen(new SolidColorBrush(highlighted ? Color.FromArgb(235, 125, 226, 255) : Color.FromArgb(125, 102, 208, 255)), highlighted ? 1.65 : 1.05);
        dc.DrawGeometry(new SolidColorBrush(highlighted ? Color.FromArgb(100, 50, 165, 255) : Color.FromArgb(32, 76, 173, 255)), outline, front);
        dc.DrawGeometry(new SolidColorBrush(highlighted ? Color.FromArgb(145, 123, 223, 255) : Color.FromArgb(58, 133, 223, 255)), outline, top);
        dc.DrawGeometry(new SolidColorBrush(highlighted ? Color.FromArgb(82, 24, 117, 235) : Color.FromArgb(27, 31, 116, 214)), outline, side);
        dc.DrawLine(outline, c, d);
        dc.DrawLine(outline, d, a);
        if (highlighted)
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(200, 220, 250, 255)), 1.25), a, b);
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
