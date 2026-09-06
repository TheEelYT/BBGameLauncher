using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BBGameLauncher.Effects;

namespace BBGameLauncher.Controls;

/// <summary>Procedural starfield with perspective-projected, glass-like menu cubes.</summary>
public sealed class SpaceScene : FrameworkElement
{
    private const double GlassIor = 1.350;
    private const double GlassF0 = ((1 - GlassIor) / (1 + GlassIor)) * ((1 - GlassIor) / (1 + GlassIor));
    private readonly List<Star> _stars = [];
    private readonly List<Cube> _cubes = [];
    private readonly List<CubeLayer> _cubeLayers = [];
    private readonly VisualCollection _cubeVisuals;
    private readonly Random _random = new(4821);
    private TimeSpan _lastFrame;
    private double _elapsed;
    private double _motionElapsed;
    private int _selectedCube;
    private int _pendingCubeCount;
    private int _pendingSelectedCube;
    private bool _hasPendingCubeSet;
    private CubeMotion _motion;
    private bool _forwardTransition;
    private RenderTargetBitmap? _backdropSnapshot;
    private double _lastBackdropSnapshotTime = -1;

    public SpaceScene()
    {
        _cubeVisuals = new VisualCollection(this);
        Loaded += (_, _) => CompositionTarget.Rendering += RenderFrame;
        Unloaded += (_, _) => CompositionTarget.Rendering -= RenderFrame;
        ClipToBounds = true;
        SetMenuCubes(4, 0);
    }

    public void SetMenuCubes(int menuItemCount, int selectedIndex)
    {
        menuItemCount = Math.Max(0, menuItemCount);
        var rebuilding = _cubes.Count != menuItemCount;
        var selectionChanged = selectedIndex != _selectedCube;
        _selectedCube = Math.Clamp(selectedIndex, 0, Math.Max(0, menuItemCount - 1));
        if (rebuilding)
            RebuildCubes(menuItemCount);
        if ((selectionChanged || rebuilding) && _cubes.Count > 0)
            AddFlick(_cubes[_selectedCube]);
        InvalidateVisual();
    }

    public void BeginCubeTransition(bool forward, int incomingCubeCount, int incomingSelectedIndex)
    {
        _forwardTransition = forward;
        _pendingCubeCount = Math.Max(0, incomingCubeCount);
        _pendingSelectedCube = Math.Clamp(incomingSelectedIndex, 0, Math.Max(0, _pendingCubeCount - 1));
        _hasPendingCubeSet = true;
        _motion = CubeMotion.Exiting;
        _motionElapsed = 0;
    }

    private void RebuildCubes(int menuItemCount)
    {
        _cubes.Clear();
        _cubeLayers.Clear();
        _cubeVisuals.Clear();
        var placements = new[]
        {
            (.66, .19, 46d), (.85, .36, 36d), (.78, .61, 44d), (.61, .76, 32d),
            (.90, .72, 54d), (.50, .22, 28d), (.72, .88, 31d)
        };
        for (var i = 0; i < menuItemCount; i++)
        {
            var p = placements[i % placements.Length];
            var cube = new Cube(p.Item1, p.Item2, p.Item3, i * .73, .24 + (i % 4) * .05);
            _cubes.Add(cube);
            var layer = new CubeLayer(this, cube);
            _cubeLayers.Add(layer);
            _cubeVisuals.Add(layer.Refraction);
            _cubeVisuals.Add(layer.Overlay);
        }
    }

    protected override int VisualChildrenCount => _cubeVisuals.Count;
    protected override Visual GetVisualChild(int index) => _cubeVisuals[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (CubeLayer layer in _cubeLayers)
            layer.Arrange(new Rect(finalSize));
        return finalSize;
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

        UpdateBackdropSnapshot();
        for (var i = 0; i < _cubeLayers.Count; i++)
            _cubeLayers[i].Update(i == _selectedCube);

        dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromArgb(42, 101, 168, 240)), 1),
            new Rect(.5, .5, Math.Max(0, RenderSize.Width - 1), Math.Max(0, RenderSize.Height - 1)));
    }

    private void UpdateBackdropSnapshot()
    {
        if (RenderSize.Width <= 0 || RenderSize.Height <= 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Math.Max(1, (int)Math.Ceiling(RenderSize.Width * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(RenderSize.Height * dpi.DpiScaleY));
        var currentSize = _backdropSnapshot?.PixelWidth == width && _backdropSnapshot.PixelHeight == height;
        if (currentSize && _elapsed - _lastBackdropSnapshotTime < 1d / 15d)
            return;

        _backdropSnapshot = new RenderTargetBitmap(width, height, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var snapshotContext = visual.RenderOpen())
        {
            snapshotContext.DrawRectangle(new LinearGradientBrush(Color.FromRgb(1, 4, 12), Color.FromRgb(2, 8, 19), 90), null, new Rect(RenderSize));
            DrawNebula(snapshotContext, new Point(RenderSize.Width * .2, RenderSize.Height * .46), RenderSize.Width * .48, Color.FromArgb(113, 67, 69, 195));
            DrawNebula(snapshotContext, new Point(RenderSize.Width * .79, RenderSize.Height * .23), RenderSize.Width * .36, Color.FromArgb(48, 22, 71, 133));
            DrawNebula(snapshotContext, new Point(RenderSize.Width * .68, RenderSize.Height * .72), RenderSize.Width * .31, Color.FromArgb(32, 3, 72, 132));
            foreach (var star in _stars)
            {
                var shimmer = .35 + (Math.Sin(_elapsed * star.Twinkle + star.Phase) + 1) * .17;
                snapshotContext.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(255 * shimmer), 140, 204, 255)), null,
                    new Point(star.X * RenderSize.Width, star.Y * RenderSize.Height), star.Size, star.Size);
            }
        }
        _backdropSnapshot.Render(visual);
        _lastBackdropSnapshotTime = _elapsed;
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
        var opacity = _motion switch
        {
            CubeMotion.Exiting => 1 - eased,
            CubeMotion.Entering => eased,
            _ => 1
        };
        var bob = Math.Sin(_elapsed * cube.Speed + cube.Phase) * 18;
        var baseCenter = new Point(cube.X * RenderSize.Width + Math.Cos(_elapsed * cube.Speed + cube.Phase) * 18,
            cube.Y * RenderSize.Height + bob);
        var center = baseCenter;
        var zoom = 1d;
        if (_motion == CubeMotion.Exiting && _forwardTransition)
        {
            zoom = 1 + eased * 7;
        }
        else if (_motion == CubeMotion.Exiting)
        {
            zoom = 1 - eased * .94;
        }
        else if (_motion == CubeMotion.Entering && _forwardTransition)
        {
            zoom = .045 + eased * .955;
        }
        else if (_motion == CubeMotion.Entering)
        {
            zoom = 7 * (1 - eased) + eased;
        }
        var size = cube.Size * (1 + Math.Sin(_elapsed * .6 + cube.Phase) * .08) * (highlighted ? 1.12 : 1) * zoom;

        if (highlighted && opacity > 0)
        {
            var glow = new RadialGradientBrush();
            glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(opacity * 110), 46, 178, 255), 0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, 46, 178, 255), 1));
            dc.DrawEllipse(glow, null, center, size * 1.9, size * 1.9);
        }

        var ax = cube.AngleX;
        var ay = cube.AngleY;
        var az = cube.AngleZ;
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
            // The shader below this overlay refracts the scene. These layered,
            // transparent face fills give that refraction a visible glass body
            // and internal reflections instead of leaving it as a wireframe.
            var alpha = (byte)(opacity * (highlighted ? 78 + fresnel * 82 : 50 + fresnel * 58));
            var tint = Blend(face.Tint, Color.FromRgb(170, 236, 255), transmission * .15);
            var fill = GlassFaceBrush(tint, alpha, fresnel);
            var facePoints = face.Indices.Select(index => projected[index]).ToArray();
            dc.DrawGeometry(fill, null, Polygon(facePoints));

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

    private (Point Center, double Size) GetCubeLayout(Cube cube, bool highlighted)
    {
        var transition = Math.Clamp(_motionElapsed / .42, 0, 1);
        var eased = 1 - Math.Pow(1 - transition, 3);
        var bob = Math.Sin(_elapsed * cube.Speed + cube.Phase) * 18;
        var center = new Point(cube.X * RenderSize.Width + Math.Cos(_elapsed * cube.Speed + cube.Phase) * 18,
            cube.Y * RenderSize.Height + bob);
        var zoom = _motion switch
        {
            CubeMotion.Exiting when _forwardTransition => 1 + eased * 7,
            CubeMotion.Exiting => 1 - eased * .94,
            CubeMotion.Entering when _forwardTransition => .045 + eased * .955,
            CubeMotion.Entering => 7 * (1 - eased) + eased,
            _ => 1
        };
        var size = cube.Size * (1 + Math.Sin(_elapsed * .6 + cube.Phase) * .08) * (highlighted ? 1.12 : 1) * zoom;
        return (center, size);
    }

    private Geometry GetCubeSilhouette(Cube cube, bool highlighted)
    {
        var (center, size) = GetCubeLayout(cube, highlighted);
        var points = new[]
        {
            new Point3(-1, -1, -1), new Point3(1, -1, -1), new Point3(1, 1, -1), new Point3(-1, 1, -1),
            new Point3(-1, -1, 1), new Point3(1, -1, 1), new Point3(1, 1, 1), new Point3(-1, 1, 1)
        }.Select(v => Project(Rotate(v, cube.AngleX, cube.AngleY, cube.AngleZ), center, size)).ToList();

        // The cube's projected outer hull is exactly the region that should
        // refract the starfield. Internal face borders are painted above it.
        var hull = points.OrderBy(p => p.X).ThenBy(p => p.Y).Aggregate(new List<Point>(), (half, point) =>
        {
            while (half.Count >= 2 && Cross(half[^2], half[^1], point) <= 0) half.RemoveAt(half.Count - 1);
            half.Add(point);
            return half;
        });
        var upper = points.OrderByDescending(p => p.X).ThenByDescending(p => p.Y).Aggregate(new List<Point>(), (half, point) =>
        {
            while (half.Count >= 2 && Cross(half[^2], half[^1], point) <= 0) half.RemoveAt(half.Count - 1);
            half.Add(point);
            return half;
        });
        hull.RemoveAt(hull.Count - 1);
        upper.RemoveAt(upper.Count - 1);
        hull.AddRange(upper);
        return Polygon(hull.ToArray());
    }

    private static double Cross(Point origin, Point a, Point b) =>
        (a.X - origin.X) * (b.Y - origin.Y) - (a.Y - origin.Y) * (b.X - origin.X);

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

    private static void AddFlick(Cube cube)
    {
        cube.SpinX += 2.4;
        cube.SpinY += 4.6;
        cube.SpinZ += .85;
    }

    private static void UpdateCubeRotations(IEnumerable<Cube> cubes, double delta)
    {
        var damping = Math.Exp(-delta * 1.4);
        foreach (var cube in cubes)
        {
            cube.AngleX += (.075 + cube.Speed * .045 + cube.SpinX) * delta;
            cube.AngleY += (.052 + cube.Speed * .035 + cube.SpinY) * delta;
            cube.AngleZ += (.014 + cube.Speed * .012 + cube.SpinZ) * delta;
            cube.SpinX *= damping;
            cube.SpinY *= damping;
            cube.SpinZ *= damping;
        }
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

    private static LinearGradientBrush GlassFaceBrush(Color tint, byte alpha, double fresnel)
    {
        static byte Scale(byte value, double amount) => (byte)Math.Clamp(value * amount, 0, 255);

        var highlight = Color.FromArgb(Scale(alpha, .68 + fresnel * .26), 185, 239, 255);
        var body = Color.FromArgb(Scale(alpha, .74), tint.R, tint.G, tint.B);
        var clearCore = Color.FromArgb(Scale(alpha, .24), tint.R, tint.G, tint.B);
        var returnReflection = Color.FromArgb(Scale(alpha, .54 + fresnel * .2), 120, 212, 255);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        brush.GradientStops.Add(new GradientStop(highlight, 0));
        brush.GradientStops.Add(new GradientStop(body, .24));
        brush.GradientStops.Add(new GradientStop(clearCore, .58));
        brush.GradientStops.Add(new GradientStop(returnReflection, 1));
        brush.Freeze();
        return brush;
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
            {
                var delta = Math.Min(.05, (args.RenderingTime - _lastFrame).TotalSeconds);
                _elapsed += delta;
                UpdateCubeRotations(_cubes, delta);
                if (_motion != CubeMotion.None)
                {
                    _motionElapsed += delta;
                    if (_motionElapsed >= .42 && _motion == CubeMotion.Exiting)
                    {
                        if (_hasPendingCubeSet)
                        {
                            _selectedCube = _pendingSelectedCube;
                            RebuildCubes(_pendingCubeCount);
                            if (_cubes.Count > 0) AddFlick(_cubes[_selectedCube]);
                            _hasPendingCubeSet = false;
                        }
                        _motion = CubeMotion.Entering;
                        _motionElapsed = 0;
                    }
                    else if (_motionElapsed >= .42)
                    {
                        _motion = CubeMotion.None;
                    }
                }
            }
            _lastFrame = args.RenderingTime;
        }
        InvalidateVisual();
    }

    private sealed record Star(double X, double Y, double Size, double Twinkle, double Phase);

    private sealed class CubeLayer
    {
        public CubeLayer(SpaceScene scene, Cube cube)
        {
            Refraction = new CubeRefractionVisual(scene, cube);
            Overlay = new CubeOverlayVisual(scene, cube);
        }

        public CubeRefractionVisual Refraction { get; }
        public CubeOverlayVisual Overlay { get; }

        public void Arrange(Rect bounds)
        {
            Refraction.Arrange(bounds);
            Overlay.Arrange(bounds);
        }

        public void Update(bool highlighted)
        {
            Refraction.Update(highlighted);
            Overlay.Highlighted = highlighted;
            Overlay.InvalidateVisual();
        }
    }

    private sealed class CubeRefractionVisual : FrameworkElement
    {
        private readonly SpaceScene _scene;
        private readonly Cube _cube;
        private readonly CubeLiquidGlassEffect _glass = new();

        public CubeRefractionVisual(SpaceScene scene, Cube cube)
        {
            _scene = scene;
            _cube = cube;
            IsHitTestVisible = false;
            Effect = _glass;
        }

        public void Update(bool highlighted)
        {
            var (center, size) = _scene.GetCubeLayout(_cube, highlighted);
            Clip = _scene.GetCubeSilhouette(_cube, highlighted);
            _glass.TextureSize = new Point(Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
            _glass.GlassCenter = center;
            _glass.GlassSize = new Point(Math.Max(1, size * 2.3), Math.Max(1, size * 2.3));
            _glass.BlurIntensity = highlighted ? 2.0f : 1.45f;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_scene._backdropSnapshot != null)
                drawingContext.DrawImage(_scene._backdropSnapshot, new Rect(RenderSize));
        }
    }

    private sealed class CubeOverlayVisual : FrameworkElement
    {
        private readonly SpaceScene _scene;
        private readonly Cube _cube;

        public CubeOverlayVisual(SpaceScene scene, Cube cube)
        {
            _scene = scene;
            _cube = cube;
            IsHitTestVisible = false;
        }

        public bool Highlighted { get; set; }

        protected override void OnRender(DrawingContext drawingContext) =>
            _scene.DrawCube(drawingContext, _cube, Highlighted);
    }

    private sealed class Cube
    {
        public Cube(double x, double y, double size, double phase, double speed)
        {
            X = x; Y = y; Size = size; Phase = phase; Speed = speed;
            AngleX = phase * .8;
            AngleY = phase * 1.7;
            AngleZ = phase * .25;
        }

        public double X { get; }
        public double Y { get; }
        public double Size { get; }
        public double Phase { get; }
        public double Speed { get; }
        public double AngleX { get; set; }
        public double AngleY { get; set; }
        public double AngleZ { get; set; }
        public double SpinX { get; set; }
        public double SpinY { get; set; }
        public double SpinZ { get; set; }
    }
    private sealed record Face(int[] Indices, Color Tint);
    private readonly record struct Point3(double X, double Y, double Z);
    private enum CubeMotion { None, Exiting, Entering }
}
