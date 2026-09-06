using System.Windows;
using System.Windows.Media;

namespace BBGameLauncher.Controls;

/// <summary>Procedural starfield with perspective-projected, glass-like menu cubes.</summary>
public sealed class SpaceScene : FrameworkElement
{
    private const double GlassIor = 1.350;
    private const double GlassF0 = ((1 - GlassIor) / (1 + GlassIor)) * ((1 - GlassIor) / (1 + GlassIor));
    private readonly List<Star> _stars = [];
    private readonly List<Cube> _cubes = [];
    private readonly Random _random = new(4821);
    private TimeSpan _lastFrame;
    private double _elapsed;
    private double _motionElapsed;
    private double _highlightStarted = -10;
    private int _selectedCube;
    private CubeMotion _motion;
    private bool _forwardTransition;

    public SpaceScene()
    {
        Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
        ClipToBounds = true;
        SetMenuCubes(4, 0);
    }

    public void SetMenuCubes(int menuItemCount, int selectedIndex)
    {
        menuItemCount = Math.Max(0, menuItemCount);
        var rebuilding = _cubes.Count != menuItemCount;
        if (selectedIndex != _selectedCube || rebuilding)
            _highlightStarted = _elapsed;
        _selectedCube = Math.Clamp(selectedIndex, 0, Math.Max(0, menuItemCount - 1));
        if (!rebuilding)
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

    public void BeginCubeExit(bool forward)
    {
        _motion = CubeMotion.Exiting;
        _motionElapsed = 0;
        _forwardTransition = forward;
    }

    public void BeginCubeEnter(bool forward)
    {
        _motion = CubeMotion.Entering;
        _motionElapsed = 0;
        _forwardTransition = forward;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(1, 4, 12), Color.FromRgb(2, 8, 19), 90), null, new Rect(RenderSize));
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
        var transition = Math.Clamp(_motionElapsed / .42, 0, 1);
        var eased = 1 - Math.Pow(1 - transition, 3);
        var travel = _motion switch
        {
            CubeMotion.Exiting => eased,
            CubeMotion.Entering => 1 - eased,
            _ => 0
        };
        var opacity = _motion switch
        {
            CubeMotion.Exiting => 1 - eased,
            CubeMotion.Entering => eased,
            _ => 1
        };
        var direction = _forwardTransition ? 1 : -1;
        var bob = Math.Sin(_elapsed * cube.Speed + cube.Phase) * 18;
        var center = new Point(
            cube.X * RenderSize.Width + Math.Cos(_elapsed * cube.Speed + cube.Phase) * 18 + direction * travel * RenderSize.Width * .23,
            cube.Y * RenderSize.Height + bob - travel * RenderSize.Height * .075);
        var size = cube.Size * (1 + Math.Sin(_elapsed * .6 + cube.Phase) * .08) * (highlighted ? 1.12 : 1) * (1 + travel * .55);

        if (highlighted && opacity > 0)
        {
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(opacity * 110), 46, 178, 255), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 46, 178, 255), 1));
            dc.DrawEllipse(glow, null, center, size * 1.9, size * 1.9);
        }

        var flickAge = Math.Max(0, _elapsed - _highlightStarted);
        var flick = highlighted ? Math.Sin(flickAge * 10.5) * Math.Exp(-flickAge * 2.7) * .92 : 0;
        var ax = _elapsed * (.075 + cube.Speed * .045) + cube.Phase + flick * .72;
        var ay = _elapsed * (.052 + cube.Speed * .035) + cube.Phase * 1.7 + flick;
        var az = _elapsed * (.014 + cube.Speed * .012) + cube.Phase + flick * .18;
        var vertices = new[]
        {
            new Point3(-1, -1, -1), new Point3(1, -1, -1), new Point3(1, 1, -1), new Point3(-1, 1, -1),
            new Point3(-1, -1, 1), new Point3(1, -1, 1), new Point3(1, 1, 1), new Point3(-1, 1, 1)
        }.Select(v => Rotate(v, ax, ay, az)).ToArray();
        var projected = vertices.Select(v => Project(v, center, size)).ToArray();
        var faces = new[]
        {
            new Face(new[] { 0, 1, 2, 3 }, Color.FromRgb(28, 90, 160)),
            new Face(new[] { 4, 7, 6, 5 }, Color.FromRgb(75, 194, 255)),
            new Face(new[] { 0, 4, 5, 1 }, Color.FromRgb(110, 211, 255)),
            new Face(new[] { 1, 5, 6, 2 }, Color.FromRgb(38, 113, 209)),
            new Face(new[] { 2, 6, 7, 3 }, Color.FromRgb(51, 145, 234)),
            new Face(new[] { 3, 7, 4, 0 }, Color.FromRgb(20, 66, 131))
        };

        foreach (var face in faces.OrderBy(face => face.Indices.Average(index => vertices[index].Z)))
        {
            var normal = FaceNormal(vertices[face.Indices[0]], vertices[face.Indices[1]], vertices[face.Indices[2]]);
            var facing = Math.Clamp(Math.Abs(normal.Z), 0, 1);
            var fresnel = GlassF0 + (1 - GlassF0) * Math.Pow(1 - facing, 5);
            var transmission = 1 - fresnel;
            var alpha = (byte)(opacity * (highlighted ? 42 + fresnel * 125 : 20 + fresnel * 68));
            // Thin glass transmits a cool background tint; the Fresnel term brings bright edge reflections.
            var tint = Blend(face.Tint, Color.FromRgb(170, 236, 255), transmission * .22);
            var fill = new SolidColorBrush(Color.FromArgb(alpha, face.Tint.R, face.Tint.G, face.Tint.B));
            fill.Color = Color.FromArgb(alpha, tint.R, tint.G, tint.B);
            dc.DrawGeometry(fill, null, Polygon(face.Indices.Select(index => projected[index]).ToArray()));
        }

        var edgeAlpha = (byte)(opacity * (highlighted ? 235 : 145));
        var edge = new Pen(new SolidColorBrush(Color.FromArgb(edgeAlpha, highlighted ? (byte)135 : (byte)95, highlighted ? (byte)231 : (byte)191, 255)), highlighted ? 1.65 : 1.05);
        foreach (var (from, to) in Edges)
            dc.DrawLine(edge, projected[from], projected[to]);
    }

    private static readonly (int From, int To)[] Edges =
    {
        (0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7)
    };

    private static Point3 Rotate(Point3 p, double xAngle, double yAngle, double zAngle)
    {
        var x = p.X * Math.Cos(yAngle) + p.Z * Math.Sin(yAngle);
        var z = -p.X * Math.Sin(yAngle) + p.Z * Math.Cos(yAngle);
        var y = p.Y * Math.Cos(xAngle) - z * Math.Sin(xAngle);
        z = p.Y * Math.Sin(xAngle) + z * Math.Cos(xAngle);
        var finalX = x * Math.Cos(zAngle) - y * Math.Sin(zAngle);
        var finalY = x * Math.Sin(zAngle) + y * Math.Cos(zAngle);
        return new Point3(finalX, finalY, z);
    }

    private static Point Project(Point3 p, Point center, double size)
    {
        var perspective = size * 2.1 / (4.3 - p.Z);
        return new Point(center.X + p.X * perspective, center.Y + p.Y * perspective);
    }

    private static Point3 FaceNormal(Point3 a, Point3 b, Point3 c)
    {
        var u = new Point3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var v = new Point3(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var normal = new Point3(u.Y * v.Z - u.Z * v.Y, u.Z * v.X - u.X * v.Z, u.X * v.Y - u.Y * v.X);
        var length = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
        return length == 0 ? normal : new Point3(normal.X / length, normal.Y / length, normal.Z / length);
    }

    private static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)(from.R + (to.R - from.R) * amount),
        (byte)(from.G + (to.G - from.G) * amount),
        (byte)(from.B + (to.B - from.B) * amount));

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
            {
                var delta = Math.Min(.05, (args.RenderingTime - _lastFrame).TotalSeconds);
                _elapsed += delta;
                if (_motion != CubeMotion.None)
                {
                    _motionElapsed += delta;
                    if (_motionElapsed >= .42) _motion = CubeMotion.None;
                }
            }
            _lastFrame = args.RenderingTime;
        }
        InvalidateVisual();
    }

    private sealed record Star(double X, double Y, double Size, double Twinkle, double Phase);
    private sealed record Cube(double X, double Y, double Size, double Phase, double Speed);
    private sealed record Face(int[] Indices, Color Tint);
    private readonly record struct Point3(double X, double Y, double Z);
    private enum CubeMotion { None, Exiting, Entering }
}
